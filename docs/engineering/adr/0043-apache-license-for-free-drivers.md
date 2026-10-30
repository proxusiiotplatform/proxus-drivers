# ADR 0043: License the free drivers as open source under Apache-2.0

- Status: accepted
- Date: 2026-10-30
- Supersedes: the licensing model of ADR 0042 (packaging, versions and publishing in ADR 0042 stay)

## Context

ADR 0042 made `Proxus.Drivers.Abstractions`, `Proxus.Drivers.Core`, `Proxus.Drivers.Modbus` and
`Proxus.Drivers.OpcUa` free of charge but not open source, under a custom Proxus Drivers Free License that was
still a draft awaiting legal review.

Proxus now publishes open-source tools on GitHub, starting with the Proxus Industrial MCP Server. Those tools are
built on these drivers. A tool whose protocol layer is closed and may not be modified is not credibly open
source, and a custom licence slows adoption: OEMs and integrators already accept Apache-2.0 and MIT without legal
review.

ADR 0042 already concluded that Modbus and OPC UA implement public specifications and hold no secret worth
protecting.

## Decision

- **Apache-2.0 for exactly four projects:** `Proxus.Drivers.Abstractions`, `Proxus.Drivers.Core`,
  `Proxus.Drivers.Modbus` and `Proxus.Drivers.OpcUa` (source and packages). The licence text is
  `packaging/LICENSE-APACHE-2.0.txt`, the unmodified text from apache.org.
- **Everything else stays proprietary and is the paid offer.** This includes S7, EtherNet/IP and Logix, the CNC
  drivers, EUROMAP, BACnet, DNP3, IEC 104, ADS, Mitsubishi, Omron, MTConnect, the schema projects, the registry,
  tools and lab material. `LICENSE` at the repository root says so.
- **Package metadata:** the packages declare `PackageLicenseExpression` `Apache-2.0` and no longer require
  licence acceptance. The copyright line is `Copyright (c) Proxus.`
- **This repository stays private and remains the source of truth.** The open source is published through a
  separate public repository that holds only the four projects, the tests among theirs that need no proprietary
  code, and the Apache licence. The owner names and creates that repository; the export is a separate change.
- **Until the public repository exists,** the packages keep publishing no repository URL and no Source Link, and
  `tools/check_packages.py` keeps enforcing that. Once it exists, Source Link and the repository URL point to it
  and the check changes with it.
- **Contributions** to the public repository are accepted under the same licence (inbound = outbound, Apache-2.0
  section 5), with a DCO sign-off. Apache-2.0 grants no trademark rights; "Proxus" stays a mark of Proxus.

## Consequences

- The release workflow no longer waits for counsel to finish a custom licence. Instead it checks that the
  licence file is the unmodified Apache-2.0 text (SHA-256 `c71d239d…0ab4`).
- Adding a driver to the open set needs a new ADR, because it narrows the paid offer. Moving a driver back out
  is not possible for versions already released: published Apache-2.0 versions stay Apache-2.0.
- Third parties may fork, modify and ship the four projects, including in competing products. This is accepted.
  Proxus competes on the platform, the commercial drivers and support.
- **Rejected alternative: open S7 and EtherNet/IP as well.** They are part of the paid offer and their
  implementation, not the public specifications, carries the value.
- **Rejected alternative: keep the free proprietary licence.** It needs legal work, adds a licence-acceptance
  step, and undermines the open-source tools built on top.

## Verification

- `tools/check_packages.py` requires the `Apache-2.0` licence expression, no licence acceptance, and no licence
  file in the package.
- The release workflow refuses to publish when `packaging/LICENSE-APACHE-2.0.txt` differs from the apache.org
  text.
- `scripts/verify.sh` packs and checks the four packages on every run.
