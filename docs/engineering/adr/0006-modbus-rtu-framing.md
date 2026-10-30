# ADR 0006: Modbus RTU as a framing option with caller-supplied streams

- Status: accepted
- Date: 2026-10-20

## Context

Modbus RTU is common in the field, both on serial lines (RS-485) and through serial-to-Ethernet
gateways in transparent mode ("RTU over TCP"). The SDK only spoke Modbus TCP (MBAP). Every Modbus
request in `ModbusTcpClient` already passes through one transport method, so the PDU-level behaviour
(typed codecs, read and write planners, services, structured errors) does not depend on the framing.

Serial ports in .NET need the `System.IO.Ports` package, which ships a native helper library per
runtime identifier. The SDK is distributed as direct binaries without package dependencies
(ADR 0002) and is Native AOT first.

Modbus RTU marks frame boundaries with line silence (3.5 character times). Gateways, USB adapters and
operating-system buffers do not preserve that silence, so silence-based framing is unreliable outside
a dedicated serial driver.

## Decision

- `ModbusTcpOptions.Framing` selects `Mbap` (default, unchanged) or `Rtu`. RTU replaces only the
  transport step: Unit ID, PDU and CRC-16 (low byte first) per Modbus over Serial Line V1.02.
- RTU runs over the TCP socket (RTU over TCP) or over a stream returned by
  `ModbusTcpOptions.RtuStreamFactory`. The caller opens and configures the serial port (for example
  `SerialPort.BaseStream`); the driver owns and disposes the stream and calls the factory again after a
  failure. The SDK takes no dependency on `System.IO.Ports`.
- Response length comes from the function code and its length fields (byte count, FIFO count, MEI
  Read Device Identification objects), as libmodbus and PyModbus do, not from silence.
  `RtuInterFrameDelay` enforces the idle time before the next request on serial lines.
- Any CRC, Unit ID or length error, timeout or transport failure closes the transport, because a byte
  stream cannot be resynchronised reliably. A timeout disposes the stream, since some serial streams
  ignore cancellation.
- Not supported: broadcast (Unit ID 0, no response to validate) and custom or vendor function codes,
  which have no response length rule; both return structured errors.
- Errors from an RTU client report `Protocol` "Modbus RTU"; the driver capabilities name is unchanged.

## Consequences

Every Modbus API works unchanged over RTU. Serial use needs a few lines of caller code to open the
port, and serial timing (baud rate, parity, inter-frame delay) is the caller's configuration. Rejected
alternatives: a bundled `System.IO.Ports` dependency (package and native library per platform), a
separate RTU client class (duplicated API surface), and silence-based framing (unreliable through
gateways).

## Verification

Deterministic self-tests: specification CRC examples (`01 03 00 00 00 0A C5 CD` and
`01 03 00 00 00 01 84 0A`) checked against the exact bytes the client sends; nine existing Modbus suites
rerun over RTU framing; CRC, Unit ID, length and broadcast faults; the stream transport with reopen after
failure; option validation. Independent stack: the lab target `modbus-pymodbus-rtu` (PyModbus RTU framer
over TCP) in the differential, fault and soak stages. No physical serial device has been tested.
