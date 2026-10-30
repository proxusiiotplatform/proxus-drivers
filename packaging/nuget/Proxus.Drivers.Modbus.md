# Proxus.Drivers.Modbus

A Modbus master for .NET 10: Modbus TCP, and RTU or ASCII framing over a serial-to-Ethernet gateway or a stream you
open. Reads and writes coils, discrete inputs, holding and input registers, typed values, records and device
identification. Every call returns a result with a structured error instead of throwing; no reflection, Native AOT
compatible.

```csharp
using Proxus.Drivers.Modbus;

await using var client = new ModbusTcpClient(new ModbusTcpOptions { Host = "192.168.0.10", Port = 502, UnitId = 1 });
var connected = await client.ConnectAsync();
if (!connected.IsSuccess)
    throw new InvalidOperationException(connected.Error.ToUserMessage());

var registers = new ushort[10];
var read = await client.ReadHoldingRegistersAsync(0, registers);
Console.WriteLine(read.IsSuccess ? string.Join(", ", registers) : read.Error.ToUserMessage());
```

Set `Framing = ModbusFraming.Rtu` or `ModbusFraming.Ascii` for gateways in transparent mode.

The package depends on `Proxus.Drivers.Abstractions` and `Proxus.Drivers.Core` at exactly the same version. Open
source under the Apache License 2.0.

## About Proxus

Built by [Proxus](https://proxus.io/?utm_source=nuget&utm_medium=readme&utm_campaign=proxus-drivers), the on-premise
industrial data platform: device connectivity, a Unified Namespace, historian, dashboards and governed AI access in one
product. Siemens S7, EtherNet/IP, CNC controls and the other protocols ship with the platform.
Source: [github.com/proxusiiotplatform/proxus-drivers](https://github.com/proxusiiotplatform/proxus-drivers).
