---
id: public-api-modbus
title: Proxus.Drivers.Modbus
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# Proxus.Drivers.Modbus

Public API for `Proxus.Drivers.Modbus`. This page is generated from the compiled assembly.

Use the [Modbus recipes](../examples/modbus.md) for complete workflows, recommendations, and warnings.

## ModbusAddress

```csharp
public struct ModbusAddress
```

Compiled zero-based Modbus address. Parsing belongs to configuration time, not the polling hot path.

### Members

#### `ModbusAddress`

```csharp
public ModbusAddress(ModbusObjectType ObjectType, ushort Offset, byte UnitId = 1)
```

Compiled zero-based Modbus address. Parsing belongs to configuration time, not the polling hot path.

#### `Parse`

```csharp
public static DriverResult<ModbusAddress> Parse(string text, byte defaultUnitId = 1)
```

*Undocumented: no XML summary; this text is generated from the name.* Parses the requested value and returns structured validation when the input is invalid.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `TryParse`

```csharp
public static bool TryParse(string text, byte defaultUnitId, out ModbusAddress address, out string error)
```

*Undocumented: no XML summary; this text is generated from the name.* Attempts to parse the requested value without throwing for an invalid value.

#### `ObjectType`

```csharp
public ModbusObjectType ObjectType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets object Type.

#### `Offset`

```csharp
public ushort Offset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets offset.

#### `UnitId`

```csharp
public byte UnitId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets unit Id.

## ModbusByteLane

```csharp
public enum ModbusByteLane
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported modbus Byte Lane choices. Select only values documented by the target protocol or device profile.

### Members

#### `Low`

```csharp
Low = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the low option.

#### `High`

```csharp
High = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the high option.

## ModbusByteWriteBehavior

```csharp
public enum ModbusByteWriteBehavior
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported modbus Byte Write Behavior choices. Select only values documented by the target protocol or device profile.

### Members

#### `Reject`

```csharp
Reject = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the reject option.

#### `ZeroOtherLane`

```csharp
ZeroOtherLane = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the zero Other Lane option.

#### `PreserveOtherLane`

```csharp
PreserveOtherLane = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the preserve Other Lane option.

## ModbusCommEventCounter

```csharp
public struct ModbusCommEventCounter
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Comm Event Counter in the public driver contract.

### Members

#### `ModbusCommEventCounter`

```csharp
public ModbusCommEventCounter(ushort Status, ushort EventCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusCommEventCounter` instance from the supplied values.

#### `EventCount`

```csharp
public ushort EventCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets event Count.

#### `IsBusy`

```csharp
public bool IsBusy { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Busy.

#### `Status`

```csharp
public ushort Status { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets status.

## ModbusCommEventLog

```csharp
public sealed class ModbusCommEventLog
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Comm Event Log in the public driver contract.

### Members

#### `ModbusCommEventLog`

```csharp
public ModbusCommEventLog(ushort Status, ushort EventCount, ushort MessageCount, byte[] Events)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusCommEventLog` instance from the supplied values.

#### `EventCount`

```csharp
public ushort EventCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets event Count.

#### `Events`

```csharp
public byte[] Events { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets events.

#### `IsBusy`

```csharp
public bool IsBusy { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Busy.

#### `MessageCount`

```csharp
public ushort MessageCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets message Count.

#### `Status`

```csharp
public ushort Status { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets status.

## ModbusCompiledReadPlan

```csharp
public sealed class ModbusCompiledReadPlan
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Compiled Read Plan in the public driver contract.

### Members

#### `Bindings`

```csharp
public ModbusReadPlanBinding[] Bindings { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets bindings.

#### `Blocks`

```csharp
public ModbusReadBlock[] Blocks { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets blocks.

#### `ValueCount`

```csharp
public int ValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value Count.

## ModbusCompiledReadRecord

```csharp
public sealed class ModbusCompiledReadRecord
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Compiled Read Record in the public driver contract.

### Members

#### `Plan`

```csharp
public ModbusCompiledReadPlan Plan { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets plan.

#### `Schema`

```csharp
public DriverRecordSchema<ModbusRegisterDescriptor> Schema { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets schema.

#### `Statistics`

```csharp
public DriverRecordPlanStatistics Statistics { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets statistics.

## ModbusCompiledWritePlan

```csharp
public sealed class ModbusCompiledWritePlan
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Compiled Write Plan in the public driver contract.

### Members

#### `Bindings`

```csharp
public ModbusWritePlanBinding[] Bindings { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets bindings.

#### `Blocks`

```csharp
public ModbusWriteBlock[] Blocks { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets blocks.

#### `SourceValueCount`

```csharp
public int SourceValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets source Value Count.

## ModbusCompiledWriteRecord

```csharp
public sealed class ModbusCompiledWriteRecord
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Compiled Write Record in the public driver contract.

### Members

#### `Plan`

```csharp
public ModbusCompiledWritePlan Plan { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets plan.

#### `Schema`

```csharp
public DriverRecordSchema<ModbusRegisterDescriptor> Schema { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets schema.

#### `Statistics`

```csharp
public DriverRecordPlanStatistics Statistics { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets statistics.

## ModbusDataCodec

```csharp
public static class ModbusDataCodec
```

Canonical codec for contiguous 16-bit Modbus registers. Scalar layout transforms are applied independently to each array element. Temporary wire buffers use stack storage; only String and Bytes results allocate their natural managed representation.

### Members

#### `Decode`

```csharp
public static DriverResult<DriverValue> Decode(ModbusRegisterDescriptor descriptor, ReadOnlySpan<ushort> registers)
```

*Undocumented: no XML summary; this text is generated from the name.* Decodes the requested value from its wire representation.

#### `DecodeMany`

```csharp
public static DriverResult<int> DecodeMany(ModbusRegisterDescriptor descriptor, ReadOnlySpan<ushort> registers, Span<DriverValue> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Decodes many from its wire representation.

#### `Encode`

```csharp
public static DriverResult<int> Encode(ModbusRegisterDescriptor descriptor, DriverValue value, Span<ushort> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Encodes the requested value into its wire representation.

#### `Encode`

```csharp
public static DriverResult<int> Encode(ModbusRegisterDescriptor descriptor, DriverValue value, ReadOnlySpan<ushort> baseline, Span<ushort> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Encodes the requested value into its wire representation.

#### `EncodeMany`

```csharp
public static DriverResult<int> EncodeMany(ModbusRegisterDescriptor descriptor, ReadOnlySpan<DriverValue> values, Span<ushort> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Encodes many into its wire representation.

#### `EncodeMany`

```csharp
public static DriverResult<int> EncodeMany(ModbusRegisterDescriptor descriptor, ReadOnlySpan<DriverValue> values, ReadOnlySpan<ushort> baseline, Span<ushort> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Encodes many into its wire representation.

## ModbusDecimalFormat

```csharp
public enum ModbusDecimalFormat
```

Explicit fixed-point representation for Decimal because Modbus defines no decimal scalar type.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `ScaledInt16`

```csharp
ScaledInt16 = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled Int16 option.

#### `ScaledUInt16`

```csharp
ScaledUInt16 = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled UInt16 option.

#### `ScaledInt32`

```csharp
ScaledInt32 = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled Int32 option.

#### `ScaledUInt32`

```csharp
ScaledUInt32 = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled UInt32 option.

#### `ScaledInt64`

```csharp
ScaledInt64 = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled Int64 option.

#### `ScaledUInt64`

```csharp
ScaledUInt64 = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the scaled UInt64 option.

#### `PackedBcd16`

```csharp
PackedBcd16 = 7
```

Unsigned packed BCD digits; DecimalScale places the decimal point logically.

#### `PackedBcd32`

```csharp
PackedBcd32 = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the packed Bcd32 option.

#### `PackedBcd64`

```csharp
PackedBcd64 = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the packed Bcd64 option.

## ModbusDeviceIdConformity

```csharp
public enum ModbusDeviceIdConformity
```

Device Identification conformity level, including individual-access support.

### Members

#### `Basic`

```csharp
Basic = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the basic option.

#### `Regular`

```csharp
Regular = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the regular option.

#### `Extended`

```csharp
Extended = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the extended option.

#### `BasicWithIndividualAccess`

```csharp
BasicWithIndividualAccess = 129
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the basic With Individual Access option.

#### `RegularWithIndividualAccess`

```csharp
RegularWithIndividualAccess = 130
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the regular With Individual Access option.

#### `ExtendedWithIndividualAccess`

```csharp
ExtendedWithIndividualAccess = 131
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the extended With Individual Access option.

## ModbusDeviceIdentification

```csharp
public sealed class ModbusDeviceIdentification
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Device Identification in the public driver contract.

### Members

#### `ModbusDeviceIdentification`

```csharp
public ModbusDeviceIdentification(ModbusDeviceIdReadCode ReadDeviceIdCode, ModbusDeviceIdConformity ConformityLevel, bool MoreFollows, byte NextObjectId, ModbusDeviceIdObject[] Objects)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusDeviceIdentification` instance from the supplied values.

#### `ConformityLevel`

```csharp
public ModbusDeviceIdConformity ConformityLevel { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets conformity Level.

#### `MoreFollows`

```csharp
public bool MoreFollows { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets more Follows.

#### `NextObjectId`

```csharp
public byte NextObjectId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets next Object Id.

#### `Objects`

```csharp
public ModbusDeviceIdObject[] Objects { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets objects.

#### `ReadDeviceIdCode`

```csharp
public ModbusDeviceIdReadCode ReadDeviceIdCode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets read Device Id Code.

## ModbusDeviceIdObject

```csharp
public struct ModbusDeviceIdObject
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Device Id Object in the public driver contract.

### Members

#### `ModbusDeviceIdObject`

```csharp
public ModbusDeviceIdObject(byte ObjectId, byte[] Value)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusDeviceIdObject` instance from the supplied values.

#### `AsAscii`

```csharp
public string AsAscii()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Ascii.

#### `ObjectId`

```csharp
public byte ObjectId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets object Id.

#### `Value`

```csharp
public byte[] Value { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value.

## ModbusDeviceIdReadCode

```csharp
public enum ModbusDeviceIdReadCode
```

Standard Read Device Identification access level.

### Members

#### `Basic`

```csharp
Basic = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the basic option.

#### `Regular`

```csharp
Regular = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the regular option.

#### `Extended`

```csharp
Extended = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the extended option.

#### `Specific`

```csharp
Specific = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the specific option.

## ModbusDiagnosticSubFunction

```csharp
public enum ModbusDiagnosticSubFunction
```

Standard MODBUS diagnostics (FC08) sub-functions.

### Members

#### `ReturnQueryData`

```csharp
ReturnQueryData = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Query Data option.

#### `RestartCommunicationsOption`

```csharp
RestartCommunicationsOption = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the restart Communications Option option.

#### `ReturnDiagnosticRegister`

```csharp
ReturnDiagnosticRegister = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Diagnostic Register option.

#### `ChangeAsciiInputDelimiter`

```csharp
ChangeAsciiInputDelimiter = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the change Ascii Input Delimiter option.

#### `ForceListenOnlyMode`

```csharp
ForceListenOnlyMode = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the force Listen Only Mode option.

#### `ClearCountersAndDiagnosticRegister`

```csharp
ClearCountersAndDiagnosticRegister = 10
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the clear Counters And Diagnostic Register option.

#### `ReturnBusMessageCount`

```csharp
ReturnBusMessageCount = 11
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Bus Message Count option.

#### `ReturnBusCommunicationErrorCount`

```csharp
ReturnBusCommunicationErrorCount = 12
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Bus Communication Error Count option.

#### `ReturnBusExceptionErrorCount`

```csharp
ReturnBusExceptionErrorCount = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Bus Exception Error Count option.

#### `ReturnServerMessageCount`

```csharp
ReturnServerMessageCount = 14
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Server Message Count option.

#### `ReturnServerNoResponseCount`

```csharp
ReturnServerNoResponseCount = 15
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Server No Response Count option.

#### `ReturnServerNakCount`

```csharp
ReturnServerNakCount = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Server Nak Count option.

#### `ReturnServerBusyCount`

```csharp
ReturnServerBusyCount = 17
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Server Busy Count option.

#### `ReturnBusCharacterOverrunCount`

```csharp
ReturnBusCharacterOverrunCount = 18
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the return Bus Character Overrun Count option.

#### `ClearOverrunCounterAndFlag`

```csharp
ClearOverrunCounterAndFlag = 20
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the clear Overrun Counter And Flag option.

## ModbusFileRecordReadRequest

```csharp
public struct ModbusFileRecordReadRequest
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the request parameters for modbus File Record Read.

### Members

#### `ModbusFileRecordReadRequest`

```csharp
public ModbusFileRecordReadRequest(ushort FileNumber, ushort RecordNumber, ushort RecordLength)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusFileRecordReadRequest` instance from the supplied values.

#### `FileNumber`

```csharp
public ushort FileNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets file Number.

#### `RecordLength`

```csharp
public ushort RecordLength { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets record Length.

#### `RecordNumber`

```csharp
public ushort RecordNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets record Number.

## ModbusFileRecordReadResult

```csharp
public sealed class ModbusFileRecordReadResult
```

*Undocumented: no XML summary; this text is generated from the name.* Carries the structured result of modbus File Record Read, including protocol status where applicable.

### Members

#### `ModbusFileRecordReadResult`

```csharp
public ModbusFileRecordReadResult(ushort FileNumber, ushort RecordNumber, ushort[] Values)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusFileRecordReadResult` instance from the supplied values.

#### `FileNumber`

```csharp
public ushort FileNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets file Number.

#### `RecordNumber`

```csharp
public ushort RecordNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets record Number.

#### `Values`

```csharp
public ushort[] Values { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets values.

## ModbusFileRecordWriteRequest

```csharp
public struct ModbusFileRecordWriteRequest
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the request parameters for modbus File Record Write.

### Members

#### `ModbusFileRecordWriteRequest`

```csharp
public ModbusFileRecordWriteRequest(ushort FileNumber, ushort RecordNumber, ReadOnlyMemory<ushort> Values)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusFileRecordWriteRequest` instance from the supplied values.

#### `FileNumber`

```csharp
public ushort FileNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets file Number.

#### `RecordNumber`

```csharp
public ushort RecordNumber { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets record Number.

#### `Values`

```csharp
public ReadOnlyMemory<ushort> Values { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets values.

## ModbusFraming

```csharp
public enum ModbusFraming
```

How Modbus PDUs are framed on the wire.

### Members

#### `Mbap`

```csharp
Mbap = 0
```

Modbus TCP: MBAP header with transaction identifier (Modbus Messaging on TCP/IP).

#### `Rtu`

```csharp
Rtu = 1
```

Modbus RTU: Unit ID, PDU and CRC-16 (Modbus over Serial Line). Used over a TCP socket for transparent serial gateways ("RTU over TCP") or over a caller-supplied stream such as a serial port.

#### `Ascii`

```csharp
Ascii = 2
```

Modbus ASCII: ":", Unit ID, PDU and LRC as hexadecimal characters, then CR LF (Modbus over Serial Line). Used over a caller-supplied stream such as a serial port, or over a TCP socket for serial gateways that pass ASCII frames through.

## ModbusGuidFormat

```csharp
public enum ModbusGuidFormat
```

Guid byte layout before the normal Modbus register-layout transform is applied.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `Rfc4122NetworkOrder`

```csharp
Rfc4122NetworkOrder = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the rfc4122 Network Order option.

#### `DotNetMixedEndian`

```csharp
DotNetMixedEndian = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the dot Net Mixed Endian option.

## ModbusMeiType

```csharp
public enum ModbusMeiType
```

MODBUS MEI (Encapsulated Interface Transport, FC2B) type.

### Members

#### `CanOpenGeneralReference`

```csharp
CanOpenGeneralReference = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the can Open General Reference option.

#### `ReadDeviceIdentification`

```csharp
ReadDeviceIdentification = 14
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Device Identification option.

## ModbusObjectType

```csharp
public enum ModbusObjectType
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported modbus Object Type choices. Select only values documented by the target protocol or device profile.

### Members

#### `Coil`

```csharp
Coil = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the coil option.

#### `DiscreteInput`

```csharp
DiscreteInput = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the discrete Input option.

#### `HoldingRegister`

```csharp
HoldingRegister = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the holding Register option.

#### `InputRegister`

```csharp
InputRegister = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the input Register option.

## ModbusReadBlock

```csharp
public struct ModbusReadBlock
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Read Block in the public driver contract.

### Members

#### `ModbusReadBlock`

```csharp
public ModbusReadBlock(ModbusObjectType ObjectType, byte UnitId, ushort StartOffset, ushort Quantity, int BindingOffset, ushort BindingCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusReadBlock` instance from the supplied values.

#### `BindingCount`

```csharp
public ushort BindingCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets binding Count.

#### `BindingOffset`

```csharp
public int BindingOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets binding Offset.

#### `ObjectType`

```csharp
public ModbusObjectType ObjectType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets object Type.

#### `Quantity`

```csharp
public ushort Quantity { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets quantity.

#### `StartOffset`

```csharp
public ushort StartOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets start Offset.

#### `UnitId`

```csharp
public byte UnitId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets unit Id.

## ModbusReadPlanBinding

```csharp
public struct ModbusReadPlanBinding
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Read Plan Binding in the public driver contract.

### Members

#### `ModbusReadPlanBinding`

```csharp
public ModbusReadPlanBinding(int DestinationOffset, ushort RelativeOffset, ModbusRegisterDescriptor Descriptor)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusReadPlanBinding` instance from the supplied values.

#### `Descriptor`

```csharp
public ModbusRegisterDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `DestinationOffset`

```csharp
public int DestinationOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets destination Offset.

#### `RelativeOffset`

```csharp
public ushort RelativeOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets relative Offset.

## ModbusReadPlanItem

```csharp
public struct ModbusReadPlanItem
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Read Plan Item in the public driver contract.

### Members

#### `ModbusReadPlanItem`

```csharp
public ModbusReadPlanItem(int DestinationOffset, ModbusRegisterDescriptor Descriptor)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusReadPlanItem` instance from the supplied values.

#### `Descriptor`

```csharp
public ModbusRegisterDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `DestinationOffset`

```csharp
public int DestinationOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets destination Offset.

## ModbusReadPlanner

```csharp
public static class ModbusReadPlanner
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Read Planner in the public driver contract.

### Members

#### `Compile`

```csharp
public static DriverResult<ModbusCompiledReadPlan> Compile(ReadOnlySpan<ModbusReadPlanItem> items, ModbusReadPlannerOptions options = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Compiles the requested value into a reusable configuration-time representation.

## ModbusReadPlannerOptions

```csharp
public sealed class ModbusReadPlannerOptions
```

*Undocumented: no XML summary; this text is generated from the name.* Configures modbus Read Planner. Create and validate it once for each endpoint.

### Members

#### `ModbusReadPlannerOptions`

```csharp
public ModbusReadPlannerOptions()
```

Creates an instance with default member values.

#### `MaximumGapBits`

```csharp
public ushort MaximumGapBits { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Gap Bits.

#### `MaximumGapRegisters`

```csharp
public ushort MaximumGapRegisters { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Gap Registers.

## ModbusRecordCompiler

```csharp
public static class ModbusRecordCompiler
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Record Compiler in the public driver contract.

### Members

#### `CompileRead`

```csharp
public static DriverResult<ModbusCompiledReadRecord> CompileRead(DriverRecordSchema<ModbusRegisterDescriptor> schema, ModbusReadPlannerOptions options = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Compiles read into a reusable configuration-time representation.

#### `CompileWrite`

```csharp
public static DriverResult<ModbusCompiledWriteRecord> CompileWrite(DriverRecordSchema<ModbusRegisterDescriptor> schema)
```

*Undocumented: no XML summary; this text is generated from the name.* Compiles write into a reusable configuration-time representation.

## ModbusRecordExecutionExtensions

```csharp
public static class ModbusRecordExecutionExtensions
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Record Execution Extensions in the public driver contract.

### Members

#### `ReadRecordAsync`

```csharp
public static ValueTask<DriverResult<int>> ReadRecordAsync(ModbusTcpClient client, ModbusCompiledReadRecord record, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads record asynchronously.

#### `WriteRecordAsync`

```csharp
public static ValueTask<DriverResult<int>> WriteRecordAsync(ModbusTcpClient client, ModbusCompiledWriteRecord record, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes record asynchronously.

## ModbusRegisterDescriptor

```csharp
public struct ModbusRegisterDescriptor
```

Compiled typed Modbus description. Modbus standardizes bits and 16-bit registers, not application scalar types, so semantic values that require vendor conventions (Decimal, Guid, temporal values, strings and byte lanes) always carry explicit wire metadata.

### Members

#### `ModbusRegisterDescriptor`

```csharp
public ModbusRegisterDescriptor(ModbusAddress Address, IndustrialDataType DataType, ushort ElementCount = 1, ushort ByteLength = 0, ModbusRegisterLayout Layout = default, ModbusByteLane ByteLane = Low, ModbusByteWriteBehavior ByteWriteBehavior = Reject, ModbusStringEncoding StringEncoding = Ascii, ModbusStringTermination StringTermination = FixedWidth, ModbusTemporalFormat TemporalFormat = None, ushort RegisterBoolTrueValue = 1, bool TrimTrailingNulls = true, ModbusDecimalFormat DecimalFormat = None, byte DecimalScale = 0, ModbusGuidFormat GuidFormat = None, IndustrialDataType EnumerationStorageType = Int16)
```

Compiled typed Modbus description. Modbus standardizes bits and 16-bit registers, not application scalar types, so semantic values that require vendor conventions (Decimal, Guid, temporal values, strings and byte lanes) always carry explicit wire metadata.

#### `GetElementRegisterWidth`

```csharp
public DriverResult<ushort> GetElementRegisterWidth()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets element Register Width.

#### `GetRequiredRegisterCount`

```csharp
public DriverResult<ushort> GetRequiredRegisterCount()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets required Register Count.

#### `GetRequiredWriteRegisterCount`

```csharp
public DriverResult<ushort> GetRequiredWriteRegisterCount()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets required Write Register Count.

#### `ValidateArray`

```csharp
public DriverResult ValidateArray()
```

*Undocumented: no XML summary; this text is generated from the name.* Validates array without performing network I/O.

#### `ValidateScalar`

```csharp
public DriverResult ValidateScalar()
```

*Undocumented: no XML summary; this text is generated from the name.* Validates scalar without performing network I/O.

#### `Address`

```csharp
public ModbusAddress Address { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets address.

#### `ByteLane`

```csharp
public ModbusByteLane ByteLane { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Lane.

#### `ByteLength`

```csharp
public ushort ByteLength { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Length.

#### `ByteWriteBehavior`

```csharp
public ModbusByteWriteBehavior ByteWriteBehavior { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Write Behavior.

#### `DataType`

```csharp
public IndustrialDataType DataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Type.

#### `DecimalFormat`

```csharp
public ModbusDecimalFormat DecimalFormat { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets decimal Format.

#### `DecimalScale`

```csharp
public byte DecimalScale { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets decimal Scale.

#### `ElementCount`

```csharp
public ushort ElementCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets element Count.

#### `EnumerationStorageType`

```csharp
public IndustrialDataType EnumerationStorageType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets enumeration Storage Type.

#### `GuidFormat`

```csharp
public ModbusGuidFormat GuidFormat { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets guid Format.

#### `Layout`

```csharp
public ModbusRegisterLayout Layout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets layout.

#### `RegisterBoolTrueValue`

```csharp
public ushort RegisterBoolTrueValue { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets register Bool True Value.

#### `StringEncoding`

```csharp
public ModbusStringEncoding StringEncoding { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets string Encoding.

#### `StringTermination`

```csharp
public ModbusStringTermination StringTermination { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets string Termination.

#### `TemporalFormat`

```csharp
public ModbusTemporalFormat TemporalFormat { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets temporal Format.

#### `TrimTrailingNulls`

```csharp
public bool TrimTrailingNulls { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets trim Trailing Nulls.

## ModbusRegisterLayout

```csharp
public struct ModbusRegisterLayout
```

Describes vendor-specific ordering inside one scalar value. For arrays the layout is applied independently to each element and never reverses the whole array.

### Members

#### `ModbusRegisterLayout`

```csharp
public ModbusRegisterLayout(bool SwapBytesInWord, bool ReverseWordOrder)
```

Describes vendor-specific ordering inside one scalar value. For arrays the layout is applied independently to each element and never reverses the whole array.

#### `ByteSwapped`

```csharp
public static ModbusRegisterLayout ByteSwapped { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets byte Swapped.

#### `LittleEndian`

```csharp
public static ModbusRegisterLayout LittleEndian { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets little Endian.

#### `ReverseWordOrder`

```csharp
public bool ReverseWordOrder { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets reverse Word Order.

#### `Standard`

```csharp
public static ModbusRegisterLayout Standard { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets standard.

#### `SwapBytesInWord`

```csharp
public bool SwapBytesInWord { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets swap Bytes In Word.

#### `WordSwapped`

```csharp
public static ModbusRegisterLayout WordSwapped { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets word Swapped.

## ModbusServerId

```csharp
public sealed class ModbusServerId
```

FC17 (0x11) Report Server ID response. Modbus Application Protocol V1.1b3 section 6.13 makes the Server ID length device specific; the Run Indicator Status (0x00 OFF, 0xFF ON) follows it.

- `ServerId`: First payload byte, the Server ID under the common one-byte layout.
- `RunIndicator`: Run Indicator Status under the one-byte layout; `false` when `ModbusServerId.HasOneByteServerIdLayout` is `false`.
- `AdditionalData`: Bytes after the Run Indicator under the one-byte layout; otherwise empty.

### Members

#### `ModbusServerId`

```csharp
public ModbusServerId(byte ServerId, bool RunIndicator, byte[] AdditionalData)
```

FC17 (0x11) Report Server ID response. Modbus Application Protocol V1.1b3 section 6.13 makes the Server ID length device specific; the Run Indicator Status (0x00 OFF, 0xFF ON) follows it.

- `ServerId`: First payload byte, the Server ID under the common one-byte layout.
- `RunIndicator`: Run Indicator Status under the one-byte layout; `false` when `ModbusServerId.HasOneByteServerIdLayout` is `false`.
- `AdditionalData`: Bytes after the Run Indicator under the one-byte layout; otherwise empty.

#### `AdditionalData`

```csharp
public byte[] AdditionalData { get; init;  }
```

Bytes after the Run Indicator under the one-byte layout; otherwise empty.

#### `Data`

```csharp
public byte[] Data { get; init;  }
```

The complete device-specific payload that follows the byte count.

#### `HasOneByteServerIdLayout`

```csharp
public bool HasOneByteServerIdLayout { get; init;  }
```

`true` when the second payload byte is a valid Run Indicator (0x00 or 0xFF), so the reply is interpreted with a one-byte Server ID. `false` when the device uses a longer Server ID; read the vendor-defined layout from `ModbusServerId.Data`.

#### `RunIndicator`

```csharp
public bool RunIndicator { get; init;  }
```

Run Indicator Status under the one-byte layout; `false` when `ModbusServerId.HasOneByteServerIdLayout` is `false`.

#### `ServerId`

```csharp
public byte ServerId { get; init;  }
```

First payload byte, the Server ID under the common one-byte layout.

## ModbusStringEncoding

```csharp
public enum ModbusStringEncoding
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported modbus String Encoding choices. Select only values documented by the target protocol or device profile.

### Members

#### `Ascii`

```csharp
Ascii = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the ascii option.

#### `Utf8`

```csharp
Utf8 = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the utf8 option.

#### `Utf16LittleEndian`

```csharp
Utf16LittleEndian = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the utf16 Little Endian option.

#### `Utf16BigEndian`

```csharp
Utf16BigEndian = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the utf16 Big Endian option.

## ModbusStringTermination

```csharp
public enum ModbusStringTermination
```

String framing/padding convention inside the fixed Modbus register span.

### Members

#### `FixedWidth`

```csharp
FixedWidth = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the fixed Width option.

#### `NullTerminated`

```csharp
NullTerminated = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the null Terminated option.

#### `SpacePadded`

```csharp
SpacePadded = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the space Padded option.

#### `ByteLengthPrefixed8`

```csharp
ByteLengthPrefixed8 = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the byte Length Prefixed8 option.

#### `ByteLengthPrefixed16BigEndian`

```csharp
ByteLengthPrefixed16BigEndian = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the byte Length Prefixed16 Big Endian option.

## ModbusTcpClient

```csharp
public sealed class ModbusTcpClient
```

Native-async Modbus client. A physical connection processes one request at a time. This prevents request interleaving and permits reusable internal buffers. Framing is Modbus TCP (MBAP) by default, or Modbus RTU or Modbus ASCII over TCP or over a caller-supplied stream (see `ModbusTcpOptions.Framing`).

### Members

#### `ModbusTcpClient`

```csharp
public ModbusTcpClient(ModbusTcpOptions options)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusTcpClient` instance from the supplied values.

#### `ConnectAsync`

```csharp
public ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Opens and initializes the protocol connection asynchronously.

#### `DiagnosticsAsync`

```csharp
public ValueTask<DriverResult<ushort>> DiagnosticsAsync(ModbusDiagnosticSubFunction subFunction, ushort data = 0, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs diagnostics Async.

#### `DiagnosticsRawAsync`

```csharp
public ValueTask<DriverResult<ushort>> DiagnosticsRawAsync(ushort subFunction, ushort data, byte? unitId = null, CancellationToken cancellationToken = default)
```

Executes a raw FC08 sub-function for vendor-specific diagnostic extensions.

#### `DisconnectAsync`

```csharp
public ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Closes the protocol connection gracefully and releases connection state.

#### `DisposeAsync`

```csharp
public ValueTask DisposeAsync()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs dispose Async.

#### `ExecuteEncapsulatedInterfaceAsync`

```csharp
public ValueTask<DriverResult<int>> ExecuteEncapsulatedInterfaceAsync(ModbusMeiType meiType, ReadOnlyMemory<byte> requestData, Memory<byte> responseData, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs execute Encapsulated Interface Async.

#### `ExecuteEncapsulatedInterfaceAsync`

```csharp
public ValueTask<DriverResult<int>> ExecuteEncapsulatedInterfaceAsync(byte meiType, ReadOnlyMemory<byte> requestData, Memory<byte> responseData, byte? unitId = null, CancellationToken cancellationToken = default)
```

Executes an arbitrary MEI type for vendor/private extensions.

#### `ExecuteRawPduAsync`

```csharp
public ValueTask<DriverResult<int>> ExecuteRawPduAsync(ReadOnlyMemory<byte> requestPdu, Memory<byte> responsePdu, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs execute Raw Pdu Async.

#### `GetCommEventCounterAsync`

```csharp
public ValueTask<DriverResult<ModbusCommEventCounter>> GetCommEventCounterAsync(byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Gets comm Event Counter.

#### `GetCommEventLogAsync`

```csharp
public ValueTask<DriverResult<ModbusCommEventLog>> GetCommEventLogAsync(byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Gets comm Event Log.

#### `MaskWriteRegisterAsync`

```csharp
public ValueTask<DriverResult> MaskWriteRegisterAsync(ushort address, ushort andMask, ushort orMask, byte? unitId = null, CancellationToken cancellationToken = default)
```

Atomically modifies selected bits in one holding register using Modbus function 0x16. The resulting value is `(current AND andMask) OR (orMask AND NOT andMask)`.

#### `ReadAllDeviceIdentificationAsync`

```csharp
public ValueTask<DriverResult<ModbusDeviceIdentification>> ReadAllDeviceIdentificationAsync(ModbusDeviceIdReadCode readDeviceIdCode = Extended, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads all Device Identification asynchronously.

#### `ReadBatchAsync`

```csharp
public ValueTask<DriverResult<int>> ReadBatchAsync(ReadOnlyMemory<DriverReadBatchItem<ModbusRegisterDescriptor>> items, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

Executes a heterogeneous one-shot read batch. The tag set is compiled into the same coalesced block planner used by long-lived polling plans, so callers get correct arbitrary-batch behavior without forfeiting contiguous-read optimization.

#### `ReadCoilsAsync`

```csharp
public ValueTask<DriverResult<int>> ReadCoilsAsync(ushort startAddress, Memory<bool> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads coils asynchronously.

#### `ReadDeviceIdentificationAsync`

```csharp
public ValueTask<DriverResult<ModbusDeviceIdentification>> ReadDeviceIdentificationAsync(ModbusDeviceIdReadCode readDeviceIdCode = Basic, byte objectId = 0, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads device Identification asynchronously.

#### `ReadDiscreteInputsAsync`

```csharp
public ValueTask<DriverResult<int>> ReadDiscreteInputsAsync(ushort startAddress, Memory<bool> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads discrete Inputs asynchronously.

#### `ReadExceptionStatusAsync`

```csharp
public ValueTask<DriverResult<byte>> ReadExceptionStatusAsync(byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads exception Status asynchronously.

#### `ReadFifoQueueAsync`

```csharp
public ValueTask<DriverResult<int>> ReadFifoQueueAsync(ushort fifoPointerAddress, Memory<ushort> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads fifo Queue asynchronously.

#### `ReadFileRecordAsync`

```csharp
public ValueTask<DriverResult<int>> ReadFileRecordAsync(ushort fileNumber, ushort recordNumber, Memory<ushort> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads file Record asynchronously.

#### `ReadFileRecordsAsync`

```csharp
public ValueTask<DriverResult<ModbusFileRecordReadResult[]>> ReadFileRecordsAsync(ReadOnlyMemory<ModbusFileRecordReadRequest> requests, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads file Records asynchronously.

#### `ReadHoldingRegistersAsync`

```csharp
public ValueTask<DriverResult<int>> ReadHoldingRegistersAsync(ushort startAddress, Memory<ushort> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads holding Registers asynchronously.

#### `ReadInputRegistersAsync`

```csharp
public ValueTask<DriverResult<int>> ReadInputRegistersAsync(ushort startAddress, Memory<ushort> destination, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads input Registers asynchronously.

#### `ReadPlanAsync`

```csharp
public ValueTask<DriverResult<int>> ReadPlanAsync(ModbusCompiledReadPlan plan, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

Executes a configuration-time compiled Modbus read plan.

#### `ReadValueAsync`

```csharp
public ValueTask<DriverResult<DriverValue>> ReadValueAsync(ModbusRegisterDescriptor descriptor, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads value asynchronously.

#### `ReadValuesAsync`

```csharp
public ValueTask<DriverResult<int>> ReadValuesAsync(ModbusRegisterDescriptor descriptor, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads values asynchronously.

#### `ReadWriteMultipleRegistersAsync`

```csharp
public ValueTask<DriverResult<int>> ReadWriteMultipleRegistersAsync(ushort readStartAddress, Memory<ushort> readDestination, ushort writeStartAddress, ReadOnlyMemory<ushort> writeValues, byte? unitId = null, CancellationToken cancellationToken = default)
```

Executes Modbus function 0x17. The device applies the write portion and returns a contiguous holding-register range in the same transaction.

#### `ReportServerIdAsync`

```csharp
public ValueTask<DriverResult<ModbusServerId>> ReportServerIdAsync(byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs report Server Id Async.

#### `WriteBatchAsync`

```csharp
public ValueTask<DriverResult<int>> WriteBatchAsync(ReadOnlyMemory<DriverWriteBatchItem<ModbusRegisterDescriptor>> items, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

Compiles and executes an arbitrary heterogeneous write batch.

#### `WriteFileRecordAsync`

```csharp
public ValueTask<DriverResult> WriteFileRecordAsync(ushort fileNumber, ushort recordNumber, ReadOnlyMemory<ushort> values, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes file Record asynchronously.

#### `WriteFileRecordsAsync`

```csharp
public ValueTask<DriverResult> WriteFileRecordsAsync(ReadOnlyMemory<ModbusFileRecordWriteRequest> requests, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes file Records asynchronously.

#### `WriteMultipleCoilsAsync`

```csharp
public ValueTask<DriverResult> WriteMultipleCoilsAsync(ushort startAddress, ReadOnlyMemory<bool> values, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes multiple Coils asynchronously.

#### `WriteMultipleRegistersAsync`

```csharp
public ValueTask<DriverResult> WriteMultipleRegistersAsync(ushort startAddress, ReadOnlyMemory<ushort> values, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes multiple Registers asynchronously.

#### `WritePlanAsync`

```csharp
public ValueTask<DriverResult<int>> WritePlanAsync(ModbusCompiledWritePlan plan, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

Executes a precompiled heterogeneous write plan under one connection gate.

#### `WriteSingleCoilAsync`

```csharp
public ValueTask<DriverResult> WriteSingleCoilAsync(ushort address, bool value, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes single Coil asynchronously.

#### `WriteSingleRegisterAsync`

```csharp
public ValueTask<DriverResult> WriteSingleRegisterAsync(ushort address, ushort value, byte? unitId = null, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes single Register asynchronously.

#### `WriteValueAsync`

```csharp
public ValueTask<DriverResult> WriteValueAsync(ModbusRegisterDescriptor descriptor, DriverValue value, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes value asynchronously.

#### `WriteValuesAsync`

```csharp
public ValueTask<DriverResult> WriteValuesAsync(ModbusRegisterDescriptor descriptor, ReadOnlyMemory<DriverValue> values, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes values asynchronously.

#### `Capabilities`

```csharp
public DriverCapabilities Capabilities { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets capabilities.

#### `IsConnected`

```csharp
public bool IsConnected { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Connected.

## ModbusTcpOptions

```csharp
public sealed class ModbusTcpOptions
```

Connection options for `ModbusTcpClient`.

### Members

#### `ModbusTcpOptions`

```csharp
public ModbusTcpOptions()
```

Creates an instance with default member values.

#### `ConnectTimeout`

```csharp
public TimeSpan ConnectTimeout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets connect Timeout.

#### `EnableTcpKeepAlive`

```csharp
public bool EnableTcpKeepAlive { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets enable Tcp Keep Alive.

#### `Framing`

```csharp
public ModbusFraming Framing { get; init;  }
```

Wire framing. `ModbusFraming.Mbap` (Modbus TCP) is the default.

#### `Host`

```csharp
public string Host { get; init;  }
```

Device or gateway host name or IP address. With `ModbusTcpOptions.RtuStreamFactory` it is only a label used in diagnostics (for example the serial port name) and may be empty.

#### `NoDelay`

```csharp
public bool NoDelay { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets no Delay.

#### `Port`

```csharp
public int Port { get; init;  }
```

TCP port; ignored when `ModbusTcpOptions.RtuStreamFactory` is set.

#### `RequestTimeout`

```csharp
public TimeSpan RequestTimeout { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets request Timeout.

#### `RtuInterFrameDelay`

```csharp
public TimeSpan RtuInterFrameDelay { get; init;  }
```

Minimum idle time between the end of one RTU frame and the start of the next request. On a serial line set it to at least 3.5 character times (about 4 ms at 9600 baud; the specification fixes 1.75 ms above 19200 baud). Zero (default) suits RTU over TCP. At most one second. Not used by Modbus ASCII, whose frames are delimited by ":" and CR LF.

#### `RtuStreamFactory`

```csharp
public Func<CancellationToken, ValueTask<Stream>> RtuStreamFactory { get; init;  }
```

Opens the byte stream for Modbus RTU or ASCII over a non-TCP transport, for example `SerialPort.BaseStream` after the caller configured baud rate, data bits, parity and stop bits (ASCII devices often use 7 data bits). The driver owns and disposes the returned stream and calls the factory again after a transport failure. Requires `ModbusTcpOptions.Framing``ModbusFraming.Rtu` or `ModbusFraming.Ascii`.

#### `UnitId`

```csharp
public byte UnitId { get; init;  }
```

Default Unit ID (slave address). Modbus RTU and ASCII require 1 to 247; broadcast (0) is not supported because it has no response to validate.

## ModbusTemporalFormat

```csharp
public enum ModbusTemporalFormat
```

Explicit vendor-selected wire width/epoch for semantic temporal values.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `UnixSecondsInt32`

```csharp
UnixSecondsInt32 = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unix Seconds Int32 option.

#### `UnixSecondsUInt32`

```csharp
UnixSecondsUInt32 = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unix Seconds UInt32 option.

#### `UnixSecondsInt64`

```csharp
UnixSecondsInt64 = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unix Seconds Int64 option.

#### `UnixMillisecondsInt64`

```csharp
UnixMillisecondsInt64 = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unix Milliseconds Int64 option.

#### `DotNetTicksInt64`

```csharp
DotNetTicksInt64 = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the dot Net Ticks Int64 option.

#### `DurationMillisecondsInt32`

```csharp
DurationMillisecondsInt32 = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the duration Milliseconds Int32 option.

#### `DurationMillisecondsInt64`

```csharp
DurationMillisecondsInt64 = 7
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the duration Milliseconds Int64 option.

#### `DurationTicksInt64`

```csharp
DurationTicksInt64 = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the duration Ticks Int64 option.

#### `DateDaysSinceUnixInt32`

```csharp
DateDaysSinceUnixInt32 = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the date Days Since Unix Int32 option.

#### `DateDaysSince1990UInt16`

```csharp
DateDaysSince1990UInt16 = 10
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the date Days Since1990 UInt16 option.

#### `TimeOfDayMillisecondsUInt32`

```csharp
TimeOfDayMillisecondsUInt32 = 11
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the time Of Day Milliseconds UInt32 option.

#### `TimeOfDaySecondsUInt32`

```csharp
TimeOfDaySecondsUInt32 = 12
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the time Of Day Seconds UInt32 option.

#### `TimeOfDayNanosecondsUInt64`

```csharp
TimeOfDayNanosecondsUInt64 = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the time Of Day Nanoseconds UInt64 option.

## ModbusWriteBlock

```csharp
public struct ModbusWriteBlock
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Write Block in the public driver contract.

### Members

#### `ModbusWriteBlock`

```csharp
public ModbusWriteBlock(ModbusObjectType ObjectType, byte UnitId, ushort StartOffset, ushort Quantity, int BindingOffset, ushort BindingCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusWriteBlock` instance from the supplied values.

#### `BindingCount`

```csharp
public ushort BindingCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets binding Count.

#### `BindingOffset`

```csharp
public int BindingOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets binding Offset.

#### `ObjectType`

```csharp
public ModbusObjectType ObjectType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets object Type.

#### `Quantity`

```csharp
public ushort Quantity { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets quantity.

#### `StartOffset`

```csharp
public ushort StartOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets start Offset.

#### `UnitId`

```csharp
public byte UnitId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets unit Id.

## ModbusWritePlanBinding

```csharp
public struct ModbusWritePlanBinding
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Write Plan Binding in the public driver contract.

### Members

#### `ModbusWritePlanBinding`

```csharp
public ModbusWritePlanBinding(int SourceOffset, ushort RelativeOffset, ModbusRegisterDescriptor Descriptor)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusWritePlanBinding` instance from the supplied values.

#### `Descriptor`

```csharp
public ModbusRegisterDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `RelativeOffset`

```csharp
public ushort RelativeOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets relative Offset.

#### `SourceOffset`

```csharp
public int SourceOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets source Offset.

## ModbusWritePlanItem

```csharp
public struct ModbusWritePlanItem
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Write Plan Item in the public driver contract.

### Members

#### `ModbusWritePlanItem`

```csharp
public ModbusWritePlanItem(int SourceOffset, ModbusRegisterDescriptor Descriptor)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `ModbusWritePlanItem` instance from the supplied values.

#### `Descriptor`

```csharp
public ModbusRegisterDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `SourceOffset`

```csharp
public int SourceOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets source Offset.

## ModbusWritePlanner

```csharp
public static class ModbusWritePlanner
```

*Undocumented: no XML summary; this text is generated from the name.* Represents modbus Write Planner in the public driver contract.

### Members

#### `Compile`

```csharp
public static DriverResult<ModbusCompiledWritePlan> Compile(ReadOnlySpan<ModbusWritePlanItem> items)
```

*Undocumented: no XML summary; this text is generated from the name.* Compiles the requested value into a reusable configuration-time representation.

