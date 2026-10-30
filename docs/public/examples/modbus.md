---
id: public-examples-modbus
title: Modbus TCP recipes
audience: developer
status: maintained
since: 0.1.0
last_verified: 2026-08-13
---

# Modbus TCP recipes

## Connect and read raw registers

```csharp
await using var client = new ModbusTcpClient(new ModbusTcpOptions
{
    Host = "192.168.1.20",
    Port = 502,
    UnitId = 1,
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(2)
});

var connected = await client.ConnectAsync(cancellationToken);
if (!connected.IsSuccess) return connected.Error;

var raw = new ushort[8];
var read = await client.ReadHoldingRegistersAsync(0, raw, cancellationToken: cancellationToken);
if (!read.IsSuccess) return read.Error;
```

Use `ReadCoilsAsync`, `ReadDiscreteInputsAsync`, `ReadHoldingRegistersAsync`, and
`ReadInputRegistersAsync` when the application deliberately wants protocol-native bits or words.

## Read a typed scalar

```csharp
var descriptor = new ModbusRegisterDescriptor(
    ModbusAddress.Parse("unit=1;hr:100").Value,
    IndustrialDataType.Float32,
    Layout: ModbusRegisterLayout.WordSwapped);

var validation = descriptor.ValidateScalar();
if (!validation.IsSuccess) return validation.Error;

var temperature = await client.ReadValueAsync(descriptor, cancellationToken);
if (!temperature.IsSuccess) return temperature.Error;
Console.WriteLine(temperature.Value.AsFloat32());
```

`ModbusRegisterLayout.Standard`, `WordSwapped`, and `ByteSwapped` are wire-layout choices, not
endianness guesses. Confirm them against a known device value before enabling writes.

## Strings, decimals, and byte lanes

```csharp
var serialNumber = new ModbusRegisterDescriptor(
    ModbusAddress.Parse("hr:200").Value,
    IndustrialDataType.String,
    ByteLength: 20,
    StringEncoding: ModbusStringEncoding.Ascii,
    StringTermination: ModbusStringTermination.NullTerminated);

var pressure = new ModbusRegisterDescriptor(
    ModbusAddress.Parse("hr:220").Value,
    IndustrialDataType.Decimal,
    DecimalFormat: ModbusDecimalFormat.ScaledInt32,
    DecimalScale: 2);
```

For 8-bit values inside a register, select `ByteLane`. A write must also declare
`ModbusByteWriteBehavior`. `PreserveOtherLane` requires a read-modify-write and is not atomic against
another writer. `ZeroOtherLane` is deterministic but destroys the other byte.

## Read arrays

```csharp
var descriptor = new ModbusRegisterDescriptor(
    ModbusAddress.Parse("hr:300").Value,
    IndustrialDataType.Int32,
    ElementCount: 16,
    Layout: ModbusRegisterLayout.Standard);

var values = new DriverValue[descriptor.ElementCount];
var result = await client.ReadValuesAsync(descriptor, values, cancellationToken);
if (!result.IsSuccess) return result.Error;

foreach (var value in values)
    Console.WriteLine(value.AsInt32());
```

Large arrays are chunked according to Modbus quantity limits. The returned count is the number of
logical values written into the destination, not the number of registers transferred.

## Batch unrelated tags

```csharp
var items = new[]
{
    new DriverReadBatchItem<ModbusRegisterDescriptor>(0,
        new(ModbusAddress.Parse("hr:0").Value, IndustrialDataType.UInt16)),
    new DriverReadBatchItem<ModbusRegisterDescriptor>(1,
        new(ModbusAddress.Parse("hr:10").Value, IndustrialDataType.Float32)),
    new DriverReadBatchItem<ModbusRegisterDescriptor>(2,
        new(ModbusAddress.Parse("coil:0").Value, IndustrialDataType.Bool))
};

var destination = new DriverValue[3];
var batch = await client.ReadBatchAsync(items, destination, cancellationToken);
```

`ReadBatchAsync` compiles a one-shot plan. For a stable polling set, call `ModbusReadPlanner.Compile`
once and reuse `ReadPlanAsync`; this avoids repeated sorting and coalescing.

## Write with explicit intent

```csharp
var command = new ModbusRegisterDescriptor(
    ModbusAddress.Parse("hr:400").Value,
    IndustrialDataType.UInt16);

var write = await client.WriteValueAsync(command, DriverValue.FromUInt16(7), cancellationToken);
if (!write.IsSuccess) return write.Error;

var readBack = await client.ReadValueAsync(command, cancellationToken);
if (!readBack.IsSuccess || readBack.Value.AsUInt16() != 7)
    throw new InvalidOperationException("Write could not be confirmed.");
```

Use `MaskWriteRegisterAsync` for device-supported atomic bit modification inside one holding register.
Use `ReadWriteMultipleRegistersAsync` only when the target documents function 0x17 semantics.

## Diagnostics and identification

`ReadDeviceIdentificationAsync` and `ReadAllDeviceIdentificationAsync` are appropriate during
commissioning. `DiagnosticsAsync`, file-record services, FIFO, raw PDU, and MEI methods are advanced
operations; call them only when the device manual explicitly defines support.

## Warnings

- `40001` notation is one-based reference notation; `hr:0` is an explicit zero-based offset.
- Unit ID matters behind TCP-to-serial gateways even when a direct TCP device ignores it.
- Input registers and discrete inputs are read-only areas.
- A Modbus exception normally leaves TCP healthy; MBAP mismatch or framing corruption does not.
- Never assume register order from the .NET type name.

See the complete [Modbus API](../api/modbus.md) and [Modbus errors](../errors/modbus.md).
