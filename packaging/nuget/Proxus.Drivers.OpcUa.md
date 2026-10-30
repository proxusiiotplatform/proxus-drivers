# Proxus.Drivers.OpcUa

A native OPC UA client for .NET 10, with no external OPC UA stack: secure channels (None, Basic256Sha256,
Aes128_Sha256_RsaOaep, Aes256_Sha256_RsaPss with Sign or SignAndEncrypt), anonymous, user name and certificate sessions that are activated
again after a lost channel, and Read, Write, Browse, subscriptions, events and alarms, history and method calls. Every
call returns a result with a structured error instead of throwing; no reflection, Native AOT compatible.

```csharp
using Proxus.Drivers;
using Proxus.Drivers.OpcUa;

await using var client = new OpcUaTcpClient(new OpcUaTcpOptions { EndpointUrl = new Uri("opc.tcp://192.168.0.20:4840") });
var connected = await client.ConnectAsync();
var session = connected.IsSuccess ? await client.OpenAnonymousSessionAsync() : connected;
if (!session.IsSuccess)
    throw new InvalidOperationException(session.Error.ToUserMessage());

var time = await client.ReadAsync(new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(0, 2258)));   // Server CurrentTime
Console.WriteLine(time.IsSuccess ? time.Value.Value.ToString() : time.Error.ToUserMessage());
await client.DisconnectAsync();
```

The package depends on `Proxus.Drivers.Abstractions` and `Proxus.Drivers.Core` at exactly the same version. Open
source under the Apache License 2.0.

## About Proxus

Built by [Proxus](https://proxus.io/?utm_source=nuget&utm_medium=readme&utm_campaign=proxus-drivers), the on-premise
industrial data platform: device connectivity, a Unified Namespace, historian, dashboards and governed AI access in one
product. Siemens S7, EtherNet/IP, CNC controls and the other protocols ship with the platform.
Source: [github.com/proxusiiotplatform/proxus-drivers](https://github.com/proxusiiotplatform/proxus-drivers).
