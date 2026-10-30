---
id: protocol-opc-ua
title: OPC UA
audience: developer
status: maintained
since: 0.1.0
last_verified: 2026-10-20
---

# OPC UA

The native OPC UA client implements UA-TCP, UA Binary, SecureChannel, Session, Read, Write, Browse,
BrowseNext, subscriptions, monitored items, Publish/Republish, managed recovery, and typed data-type
discovery. The [capability matrix](../../reference/capability-matrix.md) records the evidence level of
each service separately.

## Evidence by capability

- `vendor-and-independently-verified` (asyncua 2.0.1 independent stack and the Prosys OPC UA
  Simulation Server vendor simulator): SecureChannel, Session, Read, Browse, subscriptions with
  Publish, and `Aes256_Sha256_RsaPss` Sign and SignAndEncrypt.
- `independently-verified`: Write and UserName identity tokens (asyncua, plus open62541 v1.4.14 and the
  OPC Foundation UA-.NETStandard 1.5.378.176 reference server in the software-qualification lab);
  `Basic256Sha256` Sign and SignAndEncrypt (open62541 and the OPC Foundation reference server, no
  vendor simulator); recovery after the server drops the Session (idle Session timeout on both lab
  servers, server-side Session close on open62541): the next service returns `BadSessionIdInvalid`
  as `ReconnectAndRetry`/`Broken` and the documented reconnect restores reads. `BadSessionClosed`
  itself is `deterministically-tested` only.
- `independently-verified` by one stack only: Republish, managed recovery after a server restart, and
  X509 identity tokens (asyncua); BrowseNext continuation (Prosys vendor simulator).
- `deterministically-tested`: X509 user-token signatures under a token SecurityPolicy that differs
  from the channel policy.
- `blocked`: the authenticated `Basic256Sha256` Session against Kepware Server 7 (installation-specific
  credential).

## Security

Select a policy with the constants in `OpcUaSecurityPolicyUris` (`None`, `Basic256Sha256`, `Aes256Sha256RsaPss`)
rather than typing the URIs, both for `OpcUaTcpOptions.SecurityPolicyUri` and when matching the endpoints a server
advertises.

SecurityPolicy None and `Aes256_Sha256_RsaPss` with Sign or SignAndEncrypt have independent and
vendor-simulator evidence. `Basic256Sha256` Sign and SignAndEncrypt are independently verified against
open62541 and the OPC Foundation reference server, with no vendor-simulator Session; the authenticated
Session against Kepware Server 7 is blocked. Anonymous, UserName, and X509 identity tokens are
available only under the documented endpoint and certificate-validation rules. An X509 user token is
signed under its UserTokenPolicy `SecurityPolicyUri`, or under the endpoint policy when that field is
empty, which can differ from the SecureChannel policy (`deterministically-tested`). X509 activation
requires a Sign or SignAndEncrypt channel. Application-certificate RSA key sizes and AES inputs are
checked against the configured policy. Trust is explicit and
fail-closed; never copy a simulator trust decision into production automatically.

## Response decoding

Every array length in a server response is checked against the bytes left in the message before
anything is allocated, and Variant nesting is limited to 32 levels. A malformed, truncated or oversized
response returns a structured `UA_*_DECODE` error and never throws. A Variant that holds a DataValue or
DiagnosticInfo has no `DriverValue` form. It is still decoded, and only that node's sample comes back
as a Null value with status `Bad_DataEncodingUnsupported` (`0x80390000`), unless the server already
reported a Bad status for it; the other nodes in the same Read decode normally. These rules are covered
by the recorded open62541 and python-opcua responses and the fuzz reproducers in `tests/corpus`
(`deterministically-tested`).

## Reconnect and Session lifetime

One `OpcUaTcpClient` accepts concurrent calls: Reads, Writes, Browse and Publish can be issued at the same time
and are pipelined on the SecureChannel, each answered by its own RequestId.

A lost SecureChannel (timeout, reset, network loss) no longer orphans the Session: the next `Open*SessionAsync` on a
new channel activates the same Session again (OPC 10000-4 5.6.3, `ReactivateSessionOnReconnect`, default true) and
reports `SessionReactivated`; this includes a Session whose ActivateSession response was lost. A server that no
longer knows the Session gets a new one. Subscriptions without a running
pump are deleted after a reactivation, because their notifications could not be routed. `DisconnectAsync` closes the
Session; `DisposeAsync` closes only the socket, so call `DisconnectAsync` first. See
[ADR 0037](../../engineering/adr/0037-opcua-session-reactivation.md).

## Long-lived subscriptions

A single receive dispatcher correlates responses by RequestId, so a long-poll Publish does not block
Read, Write, or SecureChannel renewal. The managed runtime tracks sequence gaps, acknowledgements,
Republish, bounded backpressure, and recreation after reconnect.

Read the full [native OPC UA client scope](../../OPCUA_NATIVE_CLIENT.md),
schema importer, and
independent interoperability evidence. Method Call,
TranslateBrowsePathsToNodeIds, HistoryRead (raw, modified and at-time), event subscriptions with
EventFilter, ConditionRefresh and Acknowledge are available; the Alarms and Conditions state model,
processed (aggregate) and event history, and HistoryUpdate are not supported.
