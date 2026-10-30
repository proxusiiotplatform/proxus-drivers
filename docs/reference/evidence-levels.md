---
id: evidence-levels
title: Evidence levels
audience: all
status: maintained
since: 0.1.0
last_verified: 2026-10-19
---

# Evidence levels

Capability statements use the following controlled vocabulary, ordered by what each level proves
rather than by a universal quality score.

| Status | Meaning |
|---|---|
| `implemented` | Production source contains the capability; runtime behavior is not implied. |
| `deterministically-tested` | Byte-exact, lifecycle, fault, or socket regression tests pass in this repository. |
| `independently-verified` | The capability passed against an independently implemented protocol stack. |
| `vendor-and-independently-verified` | Independent and named vendor implementation evidence both exist for the stated matrix. |
| `native-aot-verified` | A published native executable ran the stated scenario; this is orthogonal to peer type. |
| `physical-device-verified` | Exact physical hardware, firmware, network context, and operation were recorded. |
| `blocked` | The planned qualification could not execute because the target or prerequisite was unavailable. |
| `not-supported` | The public product deliberately does not expose the capability. |
| `deferred` | Work is intentionally outside the current release scope. |

A higher-sounding label must not be inferred. Independent simulators do not prove physical firmware;
Native AOT against an in-process fixture does not prove independent interoperability; a physical
read-only observation does not qualify write behavior.

## Evidence sources

Name the source of every result with one of these labels, and use the same label everywhere the
result is cited.

| Label | Meaning | Examples recorded so far |
|---|---|---|
| Deterministic fixture | An in-repository fake peer, byte-exact oracle, or socket fixture. | Self-test suites, Native AOT probes against loopback fixtures |
| Independent stack | Protocol software implemented outside this repository by an open-source project. | libmodbus, PyModbus, ModbusPal, python-snap7, asyncua, cpppo, OpENer |
| Vendor simulator | Server or controller software distributed by a named commercial vendor, running without the physical device it represents. | Prosys OPC UA Simulation Server, Kepware Server, S7-PLCSIM Advanced, FactoryTalk Logix Echo |
| Physical device | Real hardware reached over a recorded network path. | Rows in the device qualification matrix |

A vendor simulator is also independently implemented. A capability exercised only by a vendor
simulator is therefore recorded as `independently-verified`, with the vendor simulator named in the
evidence. `vendor-and-independently-verified` requires both an independent stack and a vendor
simulator (or vendor device) for the same stated matrix. Neither label is physical-device evidence.

## Physical-device observations

`physical-device-verified` requires the exact hardware, firmware, network context, and operation.
A physical run that passed but did not capture the manufacturer, order number, or firmware is a
physical-device observation. It is recorded in the device qualification matrix with its test outcome,
but it does not change any catalog status and must not be described as physical-device verification.

## Test outcomes

Validation logs, interoperability reports, and qualification tables report individual runs with these
upper-case outcomes. An outcome describes one run of one stated scenario; it is not a capability status.

| Outcome | Meaning |
|---|---|
| `PASS` | The stated operation completed and its result matched the expected value or behavior. |
| `FAIL` | The target was reached and the operation produced a repeatable incorrect result. |
| `BLOCKED` | The operation could not start because the target or a prerequisite (host, licence, credential, route) was unavailable. This is not a driver failure. |
| `UNAVAILABLE` | The peer reached was correct but does not implement the requested function, so the run cannot test it (for example ModbusPal FC04). |
| `NOT RUN` | No attempt was made. |
| `NOT SUPPORTED` | The product deliberately does not expose the operation. |

## Document lifecycle statuses

The `status` field in page front matter describes the page itself, not the capability it documents.
Capability evidence belongs only in the [capability catalog](capability-matrix.md).

| Status | Meaning |
|---|---|
| `maintained` | Durable guidance that must be updated in the same change as the behavior it describes. |
| `generated` | Produced by a tool from source or the catalog; edit the generator input, not the page. |
| `development` | Tracks unreleased work, such as the next-release notes. |
| `released` | An immutable record of a published version. Correct factual errors only. |

The `since` field names the first version that contains the page's subject. `next` means the subject
is unreleased and tracked in the next-release notes.
