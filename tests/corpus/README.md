# External golden-vector corpus

Real protocol traffic recorded by other people's devices and software, used as an external test
oracle under ADR 0005. The bytes
in this corpus did not originate from Proxus code or from an in-house fixture.

| Path | Contents | Committed |
|---|---|---|
| `sources.json` | Every capture: commit-pinned URL, SHA-256 pin, license, upstream origin, redistribution decision | yes |
| `fixtures/*.pdus` | Application PDUs extracted from captures whose terms allow redistribution | yes |
| `GoldenCorpus.cs` | Fixture loader, outcome ledger and loopback replay server, linked into every `*.SelfTests` project and the fuzz tool | yes |
| `THIRD_PARTY_NOTICES.md` | License texts and attributions required by the committed fixtures | yes |
| `artifacts/golden-corpus/captures/` | The captures themselves (fetched, never committed) | no |
| `artifacts/golden-corpus/fixtures/` | Fixtures from fetch-only captures | no |

## Using it

```bash
python3 tools/golden_corpus/fetch_corpus.py      # download and SHA-256-verify every capture (about 64 MB)
python3 tools/golden_corpus/extract_corpus.py    # needs tshark; regenerates both fixture directories
```

The self-tests use `tests/corpus/fixtures` on every run. They also pick up
`artifacts/golden-corpus/fixtures` when it exists, so a full local run covers the fetch-only captures.
Set `CORPUS_VERBOSE=1` to print every structured error with its capture and frame.

A fetch fails if a capture's SHA-256 differs from its pin. To add a capture, add an entry with
`"sha256": null`, review the source and its terms, then run `fetch_corpus.py --pin`.

## Redistribution policy

A capture's fixtures are committed only when both conditions hold:

1. The repository that publishes the capture states a license that allows redistribution (BSD-3-Clause
   or CC-BY-4.0 here).
2. Nothing indicates that the capture came from a third party whose own terms are unknown.

When an aggregating repository republishes a capture taken from somewhere else, its license cannot
grant more than the original publisher did. Those captures stay fetch-only, as do sources with no stated
license. Raw captures are never committed, because they also contain unrelated traffic (ARP, DNS,
HTTP and host names).

## Sources

Licenses were checked on 2026-10-19 in each repository's license file or project page.

| Source (pinned commit) | License | Captures | Upstream origin and recorded devices | Fixtures |
|---|---|---|---|---|
| [cisagov/icsnpp-modbus](https://github.com/cisagov/icsnpp-modbus) `6dd3dab` | BSD-3-Clause, Battelle Energy Alliance | 1 | Synthetic test traffic (loopback, zeroed MACs) | committed |
| [cisagov/icsnpp-s7comm](https://github.com/cisagov/icsnpp-s7comm) `7ebeb03` | BSD-3-Clause | 1 + 1 | `s7ident.pcap`: synthetic SZL identification traffic. `snap7.pcap` is byte-identical to gymgit `snap7_s300_everything` (Snap7 software stack) | committed / fetch-only |
| [cisagov/icsnpp-enip](https://github.com/cisagov/icsnpp-enip) `3fbd32e` | BSD-3-Clause | 3 | `enip_cip_example`: Rockwell Automation responders (MAC OUI), ListIdentity reports `1756-ENBT/A`; includes Class 1 I/O | committed |
| [cisagov/icsnpp-opcua-binary](https://github.com/cisagov/icsnpp-opcua-binary) `d7fd889` | BSD-3-Clause | 29 | open62541 and python-opcua software stacks (no physical device), including crafted DiagnosticInfo and gap vectors | committed |
| [zeek/zeek](https://github.com/zeek/zeek) `048d191` `testing/btest/Traces/modbus` | BSD-3-Clause, Regents of the University of California | 6 + 1 | Zeek parser test traces, mostly synthetic; `fuzz-*.pcap` are fuzzer outputs. `4SICS-GeekLounge-151022-min` is cut from the 4SICS capture | committed / fetch-only |
| [ITI/ICS-Security-Tools](https://github.com/ITI/ICS-Security-Tools) `9b82609` `pcaps/` | CC-BY-4.0 | 23 | Captures with no other recorded origin: Rockwell (`cip_only`, `cip_unclean`), a Moxa Modbus gateway (`modbus_test*`), Siemens PLCs (`s7comm_clean`, `mb2`, mixed Siemens and Rockwell traffic) | committed |
| ITI/ICS-Security-Tools, republished | CC-BY-4.0 on the aggregate; originals unlicensed | 42 | Wireshark wiki S7-300 samples, [gymgit/s7-pcaps](https://github.com/gymgit/s7-pcaps) (S7-300/S7-400 with STEP 7, TIA Portal, WinCC), Digital Bond Quickdraw ControlLogix 5000 captures, a CloudShark capture with Rockwell traffic, OpenICS and Bro test data | fetch-only |
| [Netresec 4SICS 2015 Geek Lounge](https://www.netresec.com/?page=PCAP4SICS) `4SICS-GeekLounge-151020.pcap` | None stated; Netresec asks for credit to CS3Sthlm and a link back | 1 | ICS lab capture; S7comm from a Siemens S7-1200 (MAC OUI Siemens) | fetch-only |

`sources.json` lists every file. Captures that hold no supported PDUs (for example S7comm-plus from
S7-1200/S7-1500 firmware) are kept in the manifest, but the extractor writes no fixtures for them.

## Coverage and gaps

- **Modbus TCP**: FC1–FC6, FC8, FC15–FC17, FC22, FC23, FC43/14 and exception replies; physical-device
  replies come from a Moxa gateway.
- **S7comm classic**: COTP CR/CC, Setup Communication, Read/Write Var with S7ANY and DBREAD items, SZL
  userdata, block functions, and PLC stop and run, from S7-300, S7-400 and S7-1200 PLCs (Siemens MAC
  OUIs). S7comm-plus is out of scope.
- **EtherNet/IP and CIP**: RegisterSession, ListIdentity (`1756-ENBT/A`), SendRRData and SendUnitData,
  Forward Open and Forward Close, Multiple Service Packet, PCCC (0x4B), and Class 1 cyclic I/O, from
  Rockwell devices.
- **Gap: Logix symbolic tag services.** No capture in this corpus contains a symbolic Read Tag or
  Write Tag (service 0x4C or 0x4D with an ANSI extended symbol segment). The recorded 0x4C/0xCC traffic
  addresses class 0x72 and returns raw data. Logix tag decoding therefore still has no external golden
  vector.
- **OPC UA Binary**: HEL/ACK, OPN with SecurityPolicy None, GetEndpoints, CreateSession,
  ActivateSession, Read, Browse, BrowseNext, CreateSubscription and CreateMonitoredItems, plus
  Basic256Sha256 chunks, which are only checked for framing. All of it comes from open62541 and
  python-opcua software servers. **Gap:** no OPC UA traffic recorded from a physical device.

This corpus is external golden-vector evidence for decoding and request encoding. It is not
physical-device qualification and does not change any capability status. See
[evidence levels](../../docs/reference/evidence-levels.md).
