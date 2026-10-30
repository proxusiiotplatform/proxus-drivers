#!/usr/bin/env python3
"""Checks the NuGet packages of ADR 0042 before anyone can publish them.

Usage: check_packages.py <directory> <version> [--open]

--open checks packages built in the public proxus-drivers repository (ADR 0044): there the nuspec must name that
repository and the commit, and the PDB must carry Source Link to it. Everything else is the same.

For every expected package id the directory must hold <id>.<version>.nupkg and <id>.<version>.snupkg, and:
- the .nupkg holds exactly the assembly, its XML documentation, README.md, icon.png and NuGet's own files;
- the .snupkg holds exactly the portable PDB;
- the nuspec declares the Apache-2.0 licence expression without licence acceptance (ADR 0043), a readme and a
  description, publishes no repository,
  and depends on its sibling packages at exactly the same version;
- no file is source code, and neither the assembly nor the PDB carries embedded sources, Source Link, or a path of
  the build machine.
Any other .nupkg in the directory fails the check: only the packages ADR 0042 lists may exist.
"""
from __future__ import annotations

import sys
import uuid
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

# Package id -> sibling packages it must depend on (ADR 0042).
PACKAGES = {
    "Proxus.Drivers.Abstractions": [],
    "Proxus.Drivers.Core": ["Proxus.Drivers.Abstractions"],
    "Proxus.Drivers.Modbus": ["Proxus.Drivers.Abstractions", "Proxus.Drivers.Core"],
    "Proxus.Drivers.OpcUa": ["Proxus.Drivers.Abstractions", "Proxus.Drivers.Core"],
}
NUGET_FILES = ("_rels/.rels", "[Content_Types].xml")
SOURCE_SUFFIXES = (".cs", ".csproj", ".props", ".targets", ".py", ".sh", ".c", ".h", ".json")
# Portable PDB custom debug information kinds (System.Reflection.Metadata).
EMBEDDED_SOURCE = uuid.UUID("0E8A571B-6926-466E-B4AD-8AB04611F5FE")
SOURCE_LINK = uuid.UUID("CC110556-A091-4D38-9FEC-25AB9A351A6A")
ROOT = Path(__file__).resolve().parents[1]
OPEN_REPOSITORY = "https://github.com/proxusiiotplatform/proxus-drivers"
LOCAL_PATHS = [str(ROOT).encode(), b"/Users/", b"/home/", b"C:\\Users\\", b"/runner/work/", b"D:\\a\\"]


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def check_binary(errors: list[str], name: str, data: bytes, open_repository: bool) -> None:
    for marker in LOCAL_PATHS:
        if marker in data:
            fail(errors, f"{name}: contains the build path {marker.decode(errors='replace')!r}")
    if name.endswith(".pdb"):
        if EMBEDDED_SOURCE.bytes_le in data:
            fail(errors, f"{name}: carries embedded sources")
        has_source_link = SOURCE_LINK.bytes_le in data
        if open_repository and not has_source_link:
            fail(errors, f"{name}: has no Source Link to the public repository")
        if not open_repository and has_source_link:
            fail(errors, f"{name}: carries Source Link")


def check(directory: Path, version: str, open_repository: bool = False) -> list[str]:
    errors: list[str] = []
    found = {p.name for p in directory.glob("*.nupkg")}
    expected = {f"{package}.{version}.nupkg" for package in PACKAGES}
    for extra in sorted(found - expected):
        fail(errors, f"{extra}: not a package ADR 0042 allows")
    for package, siblings in PACKAGES.items():
        nupkg = directory / f"{package}.{version}.nupkg"
        snupkg = directory / f"{package}.{version}.snupkg"
        if not nupkg.is_file() or not snupkg.is_file():
            fail(errors, f"{package}: {nupkg.name} or {snupkg.name} is missing")
            continue
        with zipfile.ZipFile(nupkg) as z:
            names = [n for n in z.namelist() if not n.startswith("package/services/metadata/")]
            allowed = {f"{package}.nuspec", "README.md", "icon.png", f"lib/net10.0/{package}.dll",
                       f"lib/net10.0/{package}.xml", *NUGET_FILES}
            for name in names:
                if name not in allowed:
                    fail(errors, f"{nupkg.name}: unexpected file {name}")
                if name.endswith(SOURCE_SUFFIXES):
                    fail(errors, f"{nupkg.name}: source file {name}")
            for name in allowed - set(names):
                fail(errors, f"{nupkg.name}: missing {name}")
            if f"lib/net10.0/{package}.dll" in names:
                check_binary(errors, f"{nupkg.name}/{package}.dll", z.read(f"lib/net10.0/{package}.dll"), open_repository)
            if "README.md" in names and not z.read("README.md").decode("utf-8").startswith(f"# {package}"):
                fail(errors, f"{nupkg.name}: README.md is not this package's readme")
            root = ET.fromstring(z.read(f"{package}.nuspec"))
            # The nuspec schema namespace differs by the features a package uses; take it from the root element.
            ns = {"n": root.tag[1:].split("}")[0]}
            metadata = root.find("n:metadata", ns)
            text = lambda tag: (metadata.findtext(f"n:{tag}", default="", namespaces=ns) or "").strip()  # noqa: E731
            if text("id") != package or text("version") != version:
                fail(errors, f"{nupkg.name}: id or version {text('id')} {text('version')}")
            license_element = metadata.find("n:license", ns)
            if license_element is None or license_element.get("type") != "expression" or license_element.text != "Apache-2.0":
                fail(errors, f"{nupkg.name}: the licence must be the expression Apache-2.0 (ADR 0043)")
            if text("requireLicenseAcceptance") == "true":
                fail(errors, f"{nupkg.name}: an Apache-2.0 package must not require licence acceptance")
            if text("readme") != "README.md" or not text("description"):
                fail(errors, f"{nupkg.name}: readme or description missing")
            repository = metadata.find("n:repository", ns)
            if open_repository:
                commit = repository.get("commit", "") if repository is not None else ""
                if repository is None or repository.get("url") != OPEN_REPOSITORY or len(commit) != 40:
                    fail(errors, f"{nupkg.name}: must name {OPEN_REPOSITORY} and the commit it was built from")
                if not text("projectUrl").startswith("https://proxus.io/"):
                    fail(errors, f"{nupkg.name}: the project URL must point at https://proxus.io/")
            elif repository is not None or text("projectUrl"):
                fail(errors, f"{nupkg.name}: publishes a repository or project URL")
            dependencies = {d.get("id"): d.get("version") for d in metadata.iter(f"{{{ns['n']}}}dependency")}
            if dependencies != {sibling: f"[{version}]" for sibling in siblings}:
                fail(errors, f"{nupkg.name}: dependencies {dependencies}, expected exactly {siblings} at [{version}]")
        with zipfile.ZipFile(snupkg) as z:
            names = [n for n in z.namelist() if not n.startswith("package/services/metadata/")]
            allowed = {f"{package}.nuspec", f"lib/net10.0/{package}.pdb", *NUGET_FILES}
            if set(names) != allowed:
                fail(errors, f"{snupkg.name}: files {sorted(names)}, expected {sorted(allowed)}")
            if f"lib/net10.0/{package}.pdb" in names:
                check_binary(errors, f"{snupkg.name}/{package}.pdb", z.read(f"lib/net10.0/{package}.pdb"), open_repository)
    return errors


def main() -> int:
    arguments = [a for a in sys.argv[1:] if a != "--open"]
    open_repository = "--open" in sys.argv[1:]
    if len(arguments) != 2:
        print(__doc__)
        return 2
    errors = check(Path(arguments[0]), arguments[1], open_repository)
    for error in errors:
        print(f"FAIL {error}")
    if errors:
        return 1
    source = f"Source Link to {OPEN_REPOSITORY}" if open_repository else "no source or build paths"
    print(f"NuGet packages verified: {len(PACKAGES)} packages {arguments[1]} with symbols, {source}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
