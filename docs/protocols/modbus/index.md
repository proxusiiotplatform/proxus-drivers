---
id: protocol-modbus
title: Modbus TCP
audience: developer
status: maintained
since: 0.1.0
last_verified: 2026-10-22
---

# Modbus TCP

The native Modbus TCP client implements coils, discrete inputs, holding and input registers, typed
scalars and arrays, contiguous planners, record plans, and the advanced services listed in the
[capability matrix](../../reference/capability-matrix.md).

## Addressing

Compile addresses before polling. Explicit syntax is zero-based:

- `coil:0`
- `di:0`
- `hr:0`
- `ir:0`
- `unit=2;hr:10`

Reference notation such as `40001` is accepted and converted to a zero-based holding-register offset.
Prefer explicit area syntax in stored configuration because it avoids one-based reference ambiguity.

## Modbus RTU

Set `Framing = ModbusFraming.Rtu` to use Modbus RTU framing (Unit ID, PDU, CRC-16) with the same API.
Over a serial-to-Ethernet gateway in transparent mode, keep `Host` and `Port`:

```csharp
await using var client = new ModbusTcpClient(new ModbusTcpOptions
{
    Host = "10.0.0.50", Port = 4001, UnitId = 3, Framing = ModbusFraming.Rtu
});
```

On a serial port, open and configure the port yourself and hand the driver its stream. The SDK has no
`System.IO.Ports` dependency; the driver disposes the stream and calls the factory again after a
failure. Set `RtuInterFrameDelay` to at least 3.5 character times (about 4 ms at 9600 baud, 1.75 ms
above 19200 baud):

```csharp
var options = new ModbusTcpOptions
{
    Host = "COM3", // label used in diagnostics only
    UnitId = 3,
    Framing = ModbusFraming.Rtu,
    RtuInterFrameDelay = TimeSpan.FromMilliseconds(4),
    RtuStreamFactory = _ =>
    {
        var port = new SerialPort("COM3", 9600, Parity.Even, 8, StopBits.One);
        port.Open();
        return ValueTask.FromResult(port.BaseStream);
    }
};
```

Response boundaries come from the function code and its length fields, not from line silence, so
standard functions work through gateways that merge or split frames. RTU does not support broadcast
(Unit ID 0; valid Unit IDs are 1 to 247) or custom function codes through `ExecuteRawPduAsync`, which
return `MODBUS_RTU_UNIT_ID` and `MODBUS_RTU_LENGTH`. A CRC error (`MODBUS_RTU_CRC`), a Unit ID mismatch
(`MODBUS_RTU_UNIT_ID_MISMATCH`) or a timeout closes the transport; the next call reconnects. Errors from
an RTU client report `Protocol` "Modbus RTU". See [ADR 0006](../../engineering/adr/0006-modbus-rtu-framing.md).

## Modbus ASCII

Set `Framing = ModbusFraming.Ascii` for Modbus ASCII: a colon, the Unit ID, PDU and LRC as hexadecimal
characters, then CR LF. It works over a gateway that passes ASCII frames through (keep `Host` and `Port`),
or over a serial port you open and hand over with `RtuStreamFactory`, which accepts both serial framings:

```csharp
var options = new ModbusTcpOptions
{
    Host = "COM4", // label used in diagnostics only
    UnitId = 2,
    Framing = ModbusFraming.Ascii,
    RtuStreamFactory = _ =>
    {
        var port = new SerialPort("COM4", 9600, Parity.Even, 7, StopBits.One); // ASCII devices often use 7E1
        port.Open();
        return ValueTask.FromResult(port.BaseStream);
    }
};
```

- Requests are sent in upper-case hexadecimal; responses are accepted in either case, and characters
  before the colon are skipped.
- Standard functions are checked against their response length, so a frame whose CR LF is missing or
  misplaced fails at once. Custom functions through `ExecuteRawPduAsync` work over ASCII, read up to CR LF.
- Unit IDs are 1 to 247; broadcast returns `MODBUS_ASCII_UNIT_ID`.
- An LRC error (`MODBUS_ASCII_LRC`), a malformed frame (`MODBUS_ASCII_FRAME`), a Unit ID mismatch
  (`MODBUS_ASCII_UNIT_ID_MISMATCH`) or a timeout closes the transport; the next call reconnects.
- `RtuInterFrameDelay` does not apply, and a changed end-of-message character (Diagnostics 0x03) is not
  supported. Errors report `Protocol` "Modbus ASCII".

Evidence: frames and decoding match PyModbus 3.12.0 and proconX modpoll (20 golden vectors), and the lab
compares Proxus, PyModbus and modpoll against a PyModbus ASCII server and proconX diagslave in ASCII mode.
No physical ASCII device or serial port has been tested. See
[ADR 0014](../../engineering/adr/0014-modbus-ascii-framing.md).

## Safety and compatibility

Register byte/word layout belongs to `ModbusRegisterDescriptor`; it is not inferred from the PLC
model. Verify layout using known read-only values before enabling writes. Protocol exception responses
are device errors and normally leave a valid TCP connection usable.

`ReportServerIdAsync` (FC17) returns the whole device-specific payload in `ModbusServerId.Data`. The
Modbus specification lets the Server ID be any length, with the Run Indicator Status after it. The
driver therefore fills `ServerId`, `RunIndicator` and `AdditionalData` only when the second payload byte
is a valid Run Indicator (0x00 or 0xFF), which assumes a one-byte Server ID. Otherwise
`HasOneByteServerIdLayout` is `false`, and the vendor's layout must be read from `Data`.

Start with the [read sample](../../../samples/Proxus.Drivers.Modbus.Read/Program.cs). Exact data types
are documented in Data Type Support; independent libmodbus, PyModbus, and
ModbusPal evidence is recorded in interop results. The
[capability matrix](../../reference/capability-matrix.md) records the evidence level of each capability.

