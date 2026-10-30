---
id: public-errors-modbus
title: Modbus TCP errors
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# Modbus TCP errors

The message is a concise explanation of the condition. At runtime, use the complete structured
error for retry, connection health, native status, cause, and suggested operator action.

| Code | Explanation |
|---|---|
| `MODBUS_ADDRESS_RANGE` | Start address {startAddress} plus count {count} exceeds the UInt16 Modbus address space. |
| `MODBUS_AREA` | Unknown Modbus object area. |
| `MODBUS_ASCII_FRAME` | The Modbus ASCII response is malformed: {detail}. |
| `MODBUS_ASCII_LRC` | The Modbus ASCII response LRC does not match its contents. |
| `MODBUS_ASCII_UNIT_ID` | Modbus ASCII UnitId must be between 1 and {ModbusRtu.MaximumUnitId}; broadcast is not supported. |
| `MODBUS_ASCII_UNIT_ID_MISMATCH` | Expected Unit ID {unitId}, received {frame[0]}. |
| `MODBUS_BASELINE_SHORT` | Baseline has {baseline.Length} registers; {width.Value} are required. |
| `MODBUS_BIT_DESCRIPTOR` | Bit descriptors cannot define register-layout, byte-lane, string, temporal, decimal, Guid or register-bool options. |
| `MODBUS_BIT_TYPE` | Bit areas support Bool values only. |
| `MODBUS_BOOL_OPTIONS` | RegisterBoolTrueValue is valid only for Bool values. |
| `MODBUS_BOOL_TRUE` | RegisterBoolTrueValue must be non-zero. |
| `MODBUS_BYTE_BASELINE` | PreserveOtherLane requires the current physical register image as a baseline. |
| `MODBUS_BYTE_COUNT` | Expected {expectedBytes} read/write response bytes, received |
| `MODBUS_BYTE_LENGTH` | ByteLength is valid only for String and Bytes. |
| `MODBUS_BYTE_OPTIONS` | ByteLane and ByteWriteBehavior are valid only for 8-bit scalar types. |
| `MODBUS_BYTE_WRITE` | Int8/UInt8 encoding requires an explicit byte write behavior. |
| `MODBUS_BYTES_LENGTH` | Bytes requires a positive ByteLength. |
| `MODBUS_CANCELLED` | The Modbus operation was cancelled by the caller. |
| `MODBUS_CODEC_AREA` | Bit areas are decoded by the bit API. |
| `MODBUS_CONNECT` | RtuStreamFactory returned no readable and writable stream. |
| `MODBUS_CONNECT_CANCELLED` | Connection was cancelled by the caller. |
| `MODBUS_CONNECT_TIMEOUT` | Connection exceeded {_options.ConnectTimeout}. |
| `MODBUS_COUNT` | {itemName} count must be between 1 and {maximumCount}. |
| `MODBUS_DATA_SHORT` | Expected {width.Value} registers but received {registers.Length}. |
| `MODBUS_DATA_TYPE` | Unsupported data type {descriptor.DataType}. |
| `MODBUS_DECIMAL_FORMAT` | Decimal requires an explicit fixed-point storage format. |
| `MODBUS_DECIMAL_SCALE` | DecimalScale must be between 0 and 28. |
| `MODBUS_DESTINATION_SHORT` | Destination has {destination.Length} registers; {width.Value} are required. |
| `MODBUS_DEVICE_ID_CODE` | Read Device ID code must be Basic, Regular, Extended, or Specific. |
| `MODBUS_DISPOSED` | The Modbus client has been disposed. |
| `MODBUS_DNS` | No IP address was found for '{_host}'. |
| `MODBUS_ELEMENT_COUNT` | ElementCount must be greater than zero. |
| `MODBUS_ELEMENT_TOO_WIDE` | One {descriptor.DataType} element requires {width} registers; a Modbus read request can carry at most {ModbusProtocol.MaximumReadRegisters}. |
| `MODBUS_EMPTY_PDU` | Response PDU is empty. |
| `MODBUS_ENUM_STORAGE` | EnumerationStorageType is valid only for Enumeration values. |
| `MODBUS_EXCEPTION` | Device rejected the Modbus request with exception 0x{code:X2}. |
| `MODBUS_EXCEPTION_LENGTH` | Exception response must contain exactly 2 bytes, received {pdu.Length}. |
| `MODBUS_FC07_LENGTH` | FC07 response must contain 2 bytes, received {response.Length}. |
| `MODBUS_FC08_ECHO` | FC08 response did not echo the requested diagnostic sub-function. |
| `MODBUS_FC0B_LENGTH` | FC0B response must contain 5 bytes, received {response.Length}. |
| `MODBUS_FC0C_LENGTH` | FC0C byte count is invalid or contains more than the standard maximum of 64 event bytes. |
| `MODBUS_FC11_LENGTH` | FC11 byte count does not match the response or the Server ID is empty. |
| `MODBUS_FC14_LENGTH` | FC14 sub-response length/reference type is invalid. |
| `MODBUS_FC14_SUBRESPONSE` | FC14 sub-response {r} length/reference type is invalid. |
| `MODBUS_FC14_TRAILING` | FC14 response contains unexpected trailing bytes. |
| `MODBUS_FC15_ECHO` | FC15 response did not echo the validated file-record request. |
| `MODBUS_FC18_COUNT` | FC18 FIFO byte count/item count is invalid. |
| `MODBUS_FC18_LENGTH` | FC18 response is truncated. |
| `MODBUS_FIFO_DESTINATION` | FIFO response contains {count} registers; destination has {destination.Length}. |
| `MODBUS_FILE_PDU` | Combined FC15 sub-requests exceed the MODBUS 253-byte PDU / 0xFB request-data limits. Split the request into multiple calls. |
| `MODBUS_FILE_RANGE` | Each file-record write sub-request requires FileNumber 1..65535 and a non-empty sequential range within RecordNumber 0..9999. |
| `MODBUS_FILE_REQUESTS` | At least one file-record read sub-request is required. |
| `MODBUS_FRAMING` | Unknown Modbus framing {(byte)Framing}. |
| `MODBUS_FUNCTION` | Expected function 0x{expectedFunction:X2}, received 0x{pdu[0]:X2}. |
| `MODBUS_GUID_FORMAT` | GuidFormat is valid only for Guid. |
| `MODBUS_HOST` | Host is required. |
| `MODBUS_LENGTH` | Invalid MBAP length {length}. |
| `MODBUS_MBAP_SIZE` | MBAP header must contain 7 bytes. |
| `MODBUS_MEI_DESTINATION` | MEI response contains {length} type-specific bytes; destination has {responseData.Length}. |
| `MODBUS_MEI_ECHO` | Encapsulated Interface response did not echo the requested MEI type. |
| `MODBUS_MEI_HEADER` | MEI Read Device Identification response header/read-code/conformity/more-follows fields are invalid. |
| `MODBUS_MEI_LENGTH` | MEI type-specific request data exceeds the MODBUS PDU limit. |
| `MODBUS_MEI_LOOP` | Device Identification pagination did not advance Next Object ID. |
| `MODBUS_MEI_OBJECT` | Device identification object header is truncated. |
| `MODBUS_MEI_PAGES` | Device Identification exceeded 256 pages. |
| `MODBUS_MEI_TRAILING` | Device identification response contains unexpected trailing bytes. |
| `MODBUS_PLAN_DESTINATION` | DestinationOffset cannot be negative. |
| `MODBUS_PLAN_DESTINATION_OVERLAP` | Destination slot {destination} is assigned by more than one planned tag. |
| `MODBUS_PLAN_DESTINATION_SHORT` | Destination has {destination.Length} slots; the compiled plan requires {plan.ValueCount}. |
| `MODBUS_PLAN_ELEMENT_TOO_WIDE` | One {descriptor.DataType} element requires {physicalWidth} registers, above the {ModbusProtocol.MaximumReadRegisters}-register read limit. |
| `MODBUS_PLAN_EMPTY` | At least one register is required to compile a read plan. |
| `MODBUS_PLAN_GAP_BITS` | MaximumGapBits cannot exceed {ModbusProtocol.MaximumReadBits}. |
| `MODBUS_PLAN_GAP_REGISTERS` | MaximumGapRegisters cannot exceed {ModbusProtocol.MaximumReadRegisters}. |
| `MODBUS_PLAN_RANGE` | A planned register range exceeds the UInt16 Modbus address space. |
| `MODBUS_PORT` | Port must be between 1 and 65535. |
| `MODBUS_PROTOCOL_ID` | Protocol identifier must be 0, received {protocolId}. |
| `MODBUS_QUEUE_CANCELLED` | The operation was cancelled while waiting for the connection request gate. |
| `MODBUS_RAW_DESTINATION` | Raw response contains {response.Length} bytes; destination has {responsePdu.Length}. |
| `MODBUS_RAW_PDU` | Raw PDU requires a nonzero function code, 1..253 request bytes, and a non-empty response destination. |
| `MODBUS_READ_ONLY` | Only coils and holding registers are writable. |
| `MODBUS_REQUEST_TIMEOUT` | RequestTimeout must be positive, infinite, or no greater than {TimeoutTokenScope.MaximumSupportedTimeout}. |
| `MODBUS_RTU_CRC` | The Modbus RTU response CRC does not match its contents. |
| `MODBUS_RTU_INTER_FRAME_DELAY` | RtuInterFrameDelay must be between zero and one second. |
| `MODBUS_RTU_LENGTH` | Modbus RTU response would exceed {ModbusRtu.MaximumFrameLength} bytes. |
| `MODBUS_RTU_STREAM_FRAMING` | RtuStreamFactory requires Framing Rtu or Ascii. |
| `MODBUS_RTU_UNIT_ID` | Modbus RTU UnitId must be between 1 and {ModbusRtu.MaximumUnitId}; broadcast is not supported. |
| `MODBUS_RTU_UNIT_ID_MISMATCH` | Expected Unit ID {unitId}, received {frame[0]}. |
| `MODBUS_SCALAR_REQUIRED` | This operation requires ElementCount=1. Use the typed-array API for multiple elements. |
| `MODBUS_STRING_LENGTH` | String requires a positive ByteLength. |
| `MODBUS_STRING_OPTIONS` | String encoding/termination options are valid only for String values. |
| `MODBUS_TEMPORAL` | TemporalFormat is valid only for temporal semantic values. |
| `MODBUS_TIMEOUT` | The Modbus operation exceeded {_options.RequestTimeout}. |
| `MODBUS_TRANSACTION` | Expected transaction {expectedTransactionId}, received {transactionId}. |
| `MODBUS_UNIT` | Expected unit {expectedUnitId}, received {header[6]}. |
| `MODBUS_VALUE_TYPE` | Element {i} is {values[i].Type}; expected {descriptor.DataType}. |
| `MODBUS_WRITE_ECHO` | Mask-write response did not echo the request. |
| `MODBUS_WRITE_PLAN_ELEMENT_TOO_WIDE` | One {descriptor.DataType} element requires {physicalWidth} registers, above the {ModbusProtocol.MaximumWriteRegisters}-register write limit. |
| `MODBUS_WRITE_PLAN_EMPTY` | At least one writable item is required. |
| `MODBUS_WRITE_PLAN_OVERLAP` | Two batch items write overlapping physical addresses. |
| `MODBUS_WRITE_PLAN_RANGE` | A planned write exceeds the UInt16 Modbus address space. |
| `MODBUS_WRITE_PLAN_READ_ONLY` | {descriptor.Address.ObjectType} is read-only. |
| `MODBUS_WRITE_PLAN_SOURCE` | SourceOffset cannot be negative. |
| `MODBUS_WRITE_PLAN_SOURCE_SHORT` | Source has {source.Length} values; the compiled write plan requires {plan.SourceValueCount}. |
