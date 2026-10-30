# ADR 0014: Modbus ASCII as a third framing option

- Status: accepted
- Date: 2026-10-22

## Context

Modbus ASCII (Modbus over Serial Line V1.02 §2.5.2) is the older serial transmission mode. It is still
found on meters, drives and legacy PLCs that use 7 data bits, and some serial-to-Ethernet gateways pass its
frames through. A frame is:

- a colon (`:`);
- the Unit ID, the PDU and an LRC, each byte written as two hexadecimal characters;
- CR LF.

The LRC is the two's complement of the 8-bit sum of the Unit ID and PDU bytes. Unlike RTU, frames are
delimited by characters rather than by line silence, and gaps of up to one second between characters are
allowed.

[ADR 0006](0006-modbus-rtu-framing.md) added RTU as a framing option of `ModbusTcpClient`, with the transport
step as the only difference. ASCII is the same PDU behind a different transport step.

## Decision

- `ModbusFraming.Ascii` is a third framing value. It runs over the TCP socket or over the stream returned by
  `ModbusTcpOptions.RtuStreamFactory`, which now accepts RTU or ASCII; the property keeps its name for
  compatibility.
- Requests are sent in upper-case hexadecimal, as the specification and both reference masters do.
  Responses are accepted in either case.
- Characters before the colon are discarded, as the specification says a colon starts a new message.
- The response length uses the same per-function rules as RTU, applied to the decoded bytes. The client then
  requires CR LF exactly where the frame ends. Functions without a rule (custom or vendor functions through
  `ExecuteRawPduAsync`) are read pair by pair up to CR LF. ASCII therefore supports custom functions, which RTU
  cannot.
- The longest frame is 513 characters.
- Any LRC, character, delimiter, Unit ID or length error, timeout or transport failure closes the transport, as
  for RTU. The errors are `MODBUS_ASCII_LRC`, `MODBUS_ASCII_FRAME` and `MODBUS_ASCII_UNIT_ID_MISMATCH`.
- Unit IDs are 1 to 247; broadcast is not supported (`MODBUS_ASCII_UNIT_ID`). Errors report `Protocol`
  "Modbus ASCII".
- `RtuInterFrameDelay` does not apply to ASCII.
- Changing the end-of-message character (Diagnostics sub-function 0x03) is not supported; LF ends every
  frame.

## Consequences

Every Modbus API works unchanged over ASCII, through gateways and on serial ports opened by the caller (often
7 data bits with even parity). The RTU length rules now serve both serial framings.

Rejected alternatives:

- a separate ASCII client class, which would duplicate the API surface;
- reading to CR LF for every function, which would lose the early length check that rejects a frame whose
  delimiter is missing or misplaced.

## Verification

- Deterministic self-tests:
  - the specification's read example `:1103006B00037E` and other hand-computed frames;
  - eight existing Modbus suites rerun over ASCII framing;
  - LRC, Unit ID, non-hexadecimal character and delimiter faults;
  - lower-case responses and line noise before the colon;
  - the stream transport with reopen after failure, and option validation.
- Golden vectors (`tests/corpus/modbus-ascii`): request lines and server responses recorded from PyModbus
  3.12.0 and proconX modpoll by `tools/modbus_ascii_record_vectors.py`.
- Lab targets `modbus-pymodbus-ascii` and `modbus-diagslave-ascii`, in the differential, fault and soak stages.
- No physical ASCII device has been tested.
