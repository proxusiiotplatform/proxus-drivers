# Native OPC UA client checkpoint

## Implemented

This list states what the source implements. The evidence level of each service is recorded separately
in the [capability matrix](reference/capability-matrix.md) and the checkpoints below.

- UA-TCP Hello/Acknowledge framing.
- UA Binary codec for NodeId, ExpandedNodeId, QualifiedName, LocalizedText, Variant, DataValue and ExtensionObject.
- `SecurityPolicy#None` OpenSecureChannel, token renewal, CreateSession, Anonymous ActivateSession, CloseSession and CloseSecureChannel.
- Batch/single Read and Write with lossless OPC UA StatusCode, source/server timestamps and picoseconds mapped into `DriverSample`.
- Browse and BrowseAll.
- BrowseNext request/response path, including generic OPC UA ServiceFault handling. A Prosys OPC UA Simulation Server interop run forced `requestedMaxReferencesPerNode=1`, received a non-empty continuation point, consumed the next page explicitly, and verified BrowseAll assembly.
- DataTypeDefinition attribute decode for StructureDefinition and EnumDefinition.
- Live custom DataType discovery into `IndustrialSourceDocument`, then compilation through the generic `IndustrialSchemaCompiler`.
- AOT-safe registry construction.
- Concurrent request dispatcher: one receive loop routes final responses by RequestId, so a long-running Publish does not block Read, Write, Browse or SecureChannel renewal.
- CreateSubscription, Create/Modify/DeleteMonitoredItems, SetMonitoringMode, Publish, Republish and DeleteSubscriptions for `SecurityPolicy#None` anonymous Sessions.
- Publish DataChangeNotification mapping into `DriverSample`, with unknown notification ExtensionObjects retained losslessly. Publish acknowledgements remain caller-controlled, so acknowledgement can occur after downstream handling.
- `StartSubscriptionPumpAsync` provides a bounded `Channel`-backed runtime. `ReadNotificationsAsync` acknowledges a sequence only after the consumer advances past it, naturally applying backpressure when the configured notification buffer is full.
- The runtime suppresses duplicate sequence numbers, detects bounded forward gaps, retrieves missing notifications with Republish and handles UInt32 sequence wrap-around.
- `CreateManagedSubscriptionAsync` retains the subscription and monitored-item plan. A Publish transport failure with reconnect advice reconnects the SecureChannel, opens a new anonymous Session, recreates the subscription and monitored items, clears obsolete acknowledgements, and resumes the pump with the revised SubscriptionId.
- Multiple subscription runtimes on one Session are registered by SubscriptionId. Because OPC UA may satisfy any outstanding Publish request with any Subscription's notification, mismatched responses are routed to the owning runtime while each runtime keeps one Publish outstanding. Routed responses are consumed without cancelling the original Publish request.
- Automatic SecureChannel renewal at 75 percent of the revised lifetime; service-response validation is tied to the token used by the request, so in-flight requests remain valid across renewal.
- UA-TCP final-response assembly for multi-chunk `MSG/C` -> `MSG/F` and `OPN/C` -> `OPN/F` responses, bounded by negotiated/configured chunk and message limits.
- `Aes256_Sha256_RsaPss` security-policy foundation: exact nonce/key/IV/RSA-size parameters, OPC UA P_SHA256 directional key derivation, RSA-PSS/SHA-256 signing, RSA-OAEP-SHA-256 block processing, HMAC-SHA-256 verification and AES-256-CBC primitives. These primitives are covered independently from the wire layer and clear temporary key material where ownership permits.
- `Basic256Sha256` security-policy implementation: P_SHA256 directional keys, RSA-SHA256 PKCS#1 v1.5 signatures, RSA-OAEP-SHA1 block processing, HMAC-SHA256, AES-256-CBC, asymmetric OPN and symmetric MSG/CLO framing. Deterministic round-trip and tamper tests pass. On 2026-10-20 the software-qualification lab (`lab/`) completed authenticated Sessions with Read and Write against two independent servers, open62541 v1.4.14 and the OPC Foundation UA-.NETStandard 1.5.378.176 reference server, for SignAndEncrypt (Anonymous and UserName) and Sign (UserName), with zero divergences from asyncua 1.1.8 (see Independent interoperability results). No vendor-simulator Session has completed with this policy.
- Application-certificate and server-trust validation: client private-key/RSA requirements, certificate time validity, KeyUsage/EKU, ApplicationUri and DNS/IP SubjectAltName matching, direct peer trust and custom-root chain validation. Trust is explicit; an empty trust configuration does not auto-accept a peer.
- `Aes256_Sha256_RsaPss` asymmetric OpenSecureChannel framing for Issue and Renew: sender certificate and SHA-1 receiver-certificate thumbprint fields required by UA SecureConversation, RSA-PSS/SHA-256 signing, RSA-OAEP-SHA-256 encryption, 2048/3072-bit padding forms, response decrypt/verify, nonce validation and client/server symmetric-key derivation.
- Symmetric SecureConversation `MSG/CLO`: HMAC-SHA-256 signatures, AES-256-CBC encryption for SignAndEncrypt, strict padding/HMAC checks, per-chunk decrypt-before-assembly and previous-token acceptance bounded by the old token lifetime.
- Secure CreateSession and anonymous ActivateSession: client/server nonces and certificates, server CreateSession signature verification and RSA-PSS client activation signature.
- GetEndpoints discovery with lossless EndpointDescription, ApplicationDescription and UserTokenPolicy models, plus deterministic endpoint selection by message mode, security policy and identity token type.
- Anonymous, UserName and X509 ActivateSession. UserName passwords are accepted unencrypted only inside a SignAndEncrypt channel; Sign mode encrypts the password and server nonce with RSA-OAEP-SHA-256. X509 tokens sign the server certificate and nonce with RSA-PSS/SHA-256.

Secure mode accepts the leaf certificate returned by GetEndpoints separately from trust configuration. The leaf can be pinned directly or validated through a custom root chain; the client still requires that discovered leaf public key before encrypting the initial OPN request. There is no silent fallback to `SecurityPolicy#None`.

## Secure OPN interoperability checkpoint

The .NET encoder's OPN request was decoded and signature-verified by `asyncua 2.0.1`; asyncua then emitted a response which the .NET decoder decrypted and verified. A separate live server fixture accepted real `Aes256_Sha256_RsaPss` Sign and SignAndEncrypt OpenSecureChannel Issue and Renew requests from both the JIT client and a published `osx-arm64` Native AOT binary.

```text
.NET OPN -> asyncua decode/verify       PASS
asyncua OPN -> .NET decrypt/verify      PASS
Live OpenSecureChannel Issue            PASS (JIT + Native AOT)
Live OpenSecureChannel Renew            PASS (JIT + Native AOT)
Secure Session signatures               PASS (JIT + Native AOT)
Secure Read / Write / Browse            PASS (JIT + Native AOT)
Secure Subscription / Publish           PASS (JIT + Native AOT)
Secure CloseSession / CLO               PASS (JIT + Native AOT)
GetEndpoints + secure selection         PASS (JIT + Native AOT)
UserName token (Sign + SignAndEncrypt)  PASS (JIT + Native AOT)
X509 token (Sign + SignAndEncrypt)      PASS (JIT + Native AOT)
```

Repeatable commands: `scripts/verify-opcua-opn-differential.sh` and `scripts/verify-opcua-secure-opn-interop.sh`.

## Prosys vendor-simulator checkpoint

Vendor simulator: `Prosys OPC UA Simulation Server 2026.1.0-16`. After the generated `Proxus.Drivers Vendor Interop` application certificate was explicitly moved from Rejected to Trusted, both the JIT client and a published `osx-arm64` Native AOT executable passed the following matrix:

```text
GetEndpoints + exact secure selection        PASS
Aes256_Sha256_RsaPss / Sign                  PASS (JIT + Native AOT)
Aes256_Sha256_RsaPss / SignAndEncrypt        PASS (JIT + Native AOT)
Secure Session + anonymous activation        PASS (JIT + Native AOT)
Read                                         PASS (JIT + Native AOT)
Positive BrowseNext + BrowseAll assembly     PASS (JIT + Native AOT)
SecureChannel renewal                        PASS (JIT + Native AOT)
Subscription + MonitoredItem + Publish       PASS (JIT + Native AOT)
```

This is vendor-simulator evidence. Write, Republish, managed recovery, and UserName/X509 identity tokens were not exercised against Prosys; their evidence comes from asyncua alone. It is not a substitute for qualification against each target PLC/device, firmware and network environment.

## Kepware Server 7 vendor-simulator checkpoint

Windows 11 ARM64 under Parallels was snapshotted before vendor work. The Windows-native vendor probe was built with Microsoft .NET SDK 10.0.302 and produced zero compiler warnings/errors. Kepware Server 7.0.236.0 advertised exactly one legacy OPC UA endpoint in the observed configuration:

```text
SignAndEncrypt / Basic256Sha256 / UserName
```

That discovery result drove the `Basic256Sha256` implementation and its permanent regression test. The secure authenticated Session is **BLOCKED**, not failed or passed: the server requires the installation-specific Kepware username/password and does not advertise Anonymous. No server authentication, anonymous-login, certificate-trust, firewall, or project setting was changed during this checkpoint.

## Independent interoperability checkpoint

Independent server/client stack: `asyncua 2.0.1` running locally as a separate process. The native client performed:

```text
HEL/ACK                         PASS
OpenSecureChannel              PASS
SecureChannel token renewal    PASS
CreateSession                  PASS
Anonymous ActivateSession      PASS
Read Double                    PASS
Write Double                   PASS
Read-back                      PASS
Browse                         PASS
ServiceFault decode            PASS
StructureDefinition decode     PASS
EnumDefinition decode          PASS
Live schema discovery/compile  PASS
Explicit CloseSession          PASS
Graceful disconnect            PASS
```

Native-AOT linux-x64 interop executable also passed against the same server. An independent asyncua client then read back the value written by Proxus (`77.25`).

## Subscription interoperability checkpoint

Independent local stack: `asyncua 2.0.1` server fixture at `tools/asyncua_subscription_fixture.py` and the native client in `tools/Proxus.Drivers.OpcUa.SubscriptionInterop`.

```text
CreateSubscription                 PASS
CreateMonitoredItems               PASS
Publish DataChangeNotification     PASS
Republish                          PASS
DeleteMonitoredItems               PASS
DeleteSubscriptions                PASS
```

The in-process OPC UA protocol self-test additionally verifies all monitored-item service encodings, automatic SecureChannel renewal, and a Read response dispatched while a Publish request is intentionally held open.

The same subscription fixture also passed with the `osx-arm64` Native AOT executable.

The managed-recovery fixture in `scripts/verify-opcua-subscription-recovery.sh` additionally terminates the independent server process without a graceful close, restarts it on the same endpoint, and verifies that the client opens a new SecureChannel and Session, recreates the Subscription and MonitoredItem, and resumes DataChange delivery. This is a repeatable local fault-injection check; it is not a substitute for representative device, network and firmware qualification.

`scripts/verify-opcua-subscription-soak.sh` runs the long subscription-runtime gate separately from the normal fast verification. The executed 1,000,000-notification checkpoint injected nine reconnect-class failures, recreated the managed subscription each time, preserved post-consumption acknowledgement ownership, rejected obsolete SubscriptionId acknowledgements, and completed without a runtime fault.

The fixture exposed and permanently fixed two important interoperability defects during development:

1. WriteValue was initially encoded with the ReadValueId `DataEncoding` field, causing `BadTypeMismatch`. WriteValue now has its own correct encoder.
2. Generic service dispatch initially rejected ServiceFault because it expected only the requested response TypeId. ServiceFault is now handled centrally for every OPC UA service.

## Not yet claimed

The following are deliberately not claimed in this checkpoint:

- TranslateBrowsePathsToNodeIds;
- Method Call;
- HistoryRead / HistoryUpdate;
- an authenticated `Basic256Sha256` Session against any independent stack or vendor simulator;
- representative physical-device and firmware qualification beyond the asyncua independent-stack and Prosys vendor-simulator checkpoints.

These remain separate milestones rather than silent fallbacks or fake capability flags.
