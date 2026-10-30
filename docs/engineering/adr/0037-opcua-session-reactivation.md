# ADR 0037: Activate the OPC UA Session again after a lost SecureChannel

- Status: accepted
- Date: 2026-10-23

## Context

When the SecureChannel of an active Session broke (a timeout, a reset, a lost network), `OpcUaTcpClient` reconnected
and created a new Session. The old Session stayed active on the server until its timeout (60 s by default). With
several clients on one server this filled the server's Session limit. In the lab, two clients on node-opcua (10
Sessions) could not reconnect within 45 s after a blackhole (`lab/results/2026-10-23-euromap-robustness`). Devices
often allow even fewer Sessions.

OPC 10000-4 5.6.3 lets a client activate an existing Session on a new SecureChannel: ActivateSession with the
Session's authentication token, a client signature over the server certificate and the last server nonce, and the
user identity token again.

## Decision

- `OpcUaTcpOptions.ReactivateSessionOnReconnect`, default true.
- When the socket closes while a Session is active (a transport failure, a request timeout, or a DisconnectAsync
  whose CloseSession did not reach the server), the client keeps that Session and its last server nonce. It also
  keeps a Session whose ActivateSession was on the wire, because the server may have activated it although the
  response was lost.
  Disposing the client keeps nothing.
- On the next `Open*SessionAsync` on a new channel, the client first sends ActivateSession for the kept Session with
  the identity the caller passes. Credentials are not stored.
  - Success sets `SessionReactivated` and deletes the Session's subscriptions that no running pump owns. Their
    notifications could not be routed and would fault the next subscription.
  - If the server no longer knows the Session (BadSessionIdInvalid, BadSessionClosed and so on) on a working channel,
    the client creates a new Session as before.
  - If the new channel breaks during the attempt, the Session is kept for the next connection.
- `DisposeAsync` still closes only the socket; `DisconnectAsync` closes the Session. The lab's OPC UA client now calls
  DisconnectAsync before it disposes.

## Consequences

Network faults no longer leave a Session per reconnect on the server. Subscriptions with a running pump survive a
reactivation; their runtime continues on the server's subscription. Callers that create subscriptions without a pump
and expect them to survive a lost channel lose them on reactivation, which is documented.

## Verification

- Self-test with a test server that accepts further connections:
  - the reactivation request (ActivateSession only, no CreateSession), the orphaned subscription deleted;
  - the fallback to CreateSession after BadSessionIdInvalid;
  - a Session whose ActivateSession response was lost, activated on the next connection;
  - the option turned off.
- Lab session stage, new scenario `channel-loss-reactivate`: open62541 (None/Anonymous and
  SignAndEncrypt/Basic256Sha256/UserName) and UA-.NETStandard (None/Anonymous and SignAndEncrypt/Basic256Sha256/
  Anonymous) activate the same Session again and serve correct reads. Results: 4/4.
- Lab fault matrix: 18/18 on open62541 and UA-.NETStandard, and 18/18 for the two EUROMAP targets on one node-opcua
  server together (previously 17/18). The server logged each reactivation as a Session transferred to the new
  channel, and no Session timed out during or after that run (about 15 per run before).
