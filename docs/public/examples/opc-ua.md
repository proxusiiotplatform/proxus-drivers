---
id: public-examples-opcua
title: OPC UA recipes
audience: developer
status: maintained
since: 0.1.0
last_verified: 2026-08-13
---

# OPC UA recipes

## Discover and select an endpoint

```csharp
var discovered = await OpcUaTcpClient.DiscoverEndpointsAsync(
    new Uri("opc.tcp://server:4840"), cancellationToken);
if (!discovered.IsSuccess) return discovered.Error;

var selected = OpcUaEndpointSelector.Select(
    discovered.Value,
    OpcUaMessageSecurityMode.SignAndEncrypt,
    "http://opcfoundation.org/UA/SecurityPolicy#Aes256_Sha256_RsaPss");
if (!selected.IsSuccess) return selected.Error;
```

Do not select an endpoint by array position. Match the required security mode and policy, then validate
the server certificate identity before creating the client.

## Open a session

```csharp
await using var client = new OpcUaTcpClient(new OpcUaTcpOptions
{
    EndpointUrl = new Uri(selected.Value.EndpointUrl),
    SecurityMode = selected.Value.SecurityMode,
    SecurityPolicyUri = selected.Value.SecurityPolicyUri,
    EndpointServerCertificate = X509CertificateLoader.LoadCertificate(selected.Value.ServerCertificate!),
    ApplicationUri = "urn:example:production-client",
    ApplicationName = "Example production client",
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(10),
    PublishRequestTimeout = TimeSpan.FromSeconds(30)
});

var connected = await client.ConnectAsync(cancellationToken);
if (!connected.IsSuccess) return connected.Error;
var session = await client.OpenAnonymousSessionAsync(cancellationToken);
if (!session.IsSuccess) return session.Error;
```

Secure endpoints additionally require an application certificate and explicit `CertificateTrust`.
Never implement “trust all certificates” in production. UserName and X509 user-token sessions use
`OpenUserNameSessionAsync` and `OpenCertificateSessionAsync` after endpoint policy selection.

## Read values with quality

```csharp
var node = new OpcUaNodeDescriptor(IndustrialNodeId.String(2, "Machine.Speed"));
var read = await client.ReadAsync(node, cancellationToken);
if (!read.IsSuccess) return read.Error;

var sample = read.Value;
if (!sample.Quality.IsGood)
{
    logger.LogWarning("OPC UA quality is {Quality}", sample.Quality);
    return;
}

Console.WriteLine($"{sample.Value.AsFloat32()} at {sample.SourceTimestampUtc:O}");
```

A successful service response can contain a bad per-node StatusCode. Use `DriverSample.Quality`; do not
treat transport success as good process data. Batch `ReadAsync` preserves one result slot per node.

## Browse safely

```csharp
var objects = new OpcUaBrowseDescription(
    IndustrialNodeId.Numeric(0, 85),
    NodeClassMask: OpcUaNodeClass.Object | OpcUaNodeClass.Variable);

var references = await client.BrowseAllAsync(objects, requestedMaxReferencesPerNode: 100, cancellationToken);
if (!references.IsSuccess) return references.Error;
```

`BrowseAllAsync` consumes continuation points within configured page/reference limits. Use `BrowseAsync`
and `BrowseNextAsync` directly only when the application needs page-by-page control or early release.

## Write a value

```csharp
var write = await client.WriteAsync(
    new OpcUaNodeDescriptor(IndustrialNodeId.String(2, "Machine.Setpoint")),
    DriverValue.FromFloat32(12.5f),
    cancellationToken);
if (!write.IsSuccess) return write.Error;
```

For multi-node writes use `OpcUaWriteItem[]`. Inspect the returned structured error for per-service and
per-node rejection. Do not replay a timed-out write without reconciling its final value.

## Managed subscriptions

```csharp
var plan = new OpcUaManagedSubscriptionPlan(
    new OpcUaSubscriptionRequest(
        RequestedPublishingInterval: 500,
        RequestedLifetimeCount: 60,
        RequestedMaxKeepAliveCount: 10),
    [
        new OpcUaMonitoredItemRequest(
            new OpcUaNodeDescriptor(IndustrialNodeId.String(2, "Machine.Speed")),
            ClientHandle: 101,
            SamplingInterval: 250,
            QueueSize: 10,
            DiscardOldest: true)
    ]);

var created = await client.CreateManagedSubscriptionAsync(plan,
    new OpcUaSubscriptionRuntimeOptions
    {
        NotificationBufferCapacity = 256,
        MaximumRepublishGap = 32,
        MaximumReconnectAttempts = 5
    }, cancellationToken);
if (!created.IsSuccess) return created.Error;

await using var subscription = created.Value;
await foreach (var notification in subscription.ReadNotificationsAsync(cancellationToken))
{
    foreach (var monitored in notification.Values)
        Consume(monitored.ClientHandle, monitored.Sample);
}
```

The managed runtime owns Publish, acknowledgements, sequence-gap Republish, bounded buffering, and
recreation. A full notification buffer is an application backpressure problem; increase capacity only
after measuring consumer latency and memory.

Use raw `CreateSubscriptionAsync`, `CreateMonitoredItemsAsync`, `PublishAsync`, `RepublishAsync`, and
delete methods when the application intentionally owns the complete subscription state machine.

## Warnings

- Validate certificate application URI, hostname, trust chain, and selected endpoint together.
- Keep `PublishRequestTimeout` longer than normal service timeout; Publish is a long-poll service.
- NodeId namespace indexes can change between servers; use stable namespace URI mapping in configuration.
- `UnknownNotifications` must not be silently discarded when lossless forwarding matters.
- The full Alarms and Conditions state model (branches, Confirm, Shelving) is not a current public capability.

See the complete [OPC UA API](../api/opcua.md) and [OPC UA errors](../errors/opc-ua.md).
