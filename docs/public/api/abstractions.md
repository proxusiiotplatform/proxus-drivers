---
id: public-api-abstractions
title: Proxus.Drivers.Abstractions
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# Proxus.Drivers.Abstractions

Public API for `Proxus.Drivers.Abstractions`. This page is generated from the compiled assembly.

Use [Common SDK patterns](../examples/common-patterns.md) for lifecycle, result handling, cancellation, and ownership guidance.

## DriverArrayShape

```csharp
public sealed class DriverArrayShape
```

Immutable row-major rank-N array shape used by PLC arrays, record arrays and importers.

### Members

#### `Create`

```csharp
public static DriverResult<DriverArrayShape> Create(ReadOnlySpan<int> dimensions)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `CreateRuntime`

```csharp
public static DriverResult<DriverArrayShape> CreateRuntime(ReadOnlySpan<int> dimensions)
```

Creates a runtime array shape. OPC UA and other dynamic protocols may legally return empty arrays.

#### `FormatIndices`

```csharp
public string FormatIndices(int linearIndex)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs format Indices.

#### `TryGetIndices`

```csharp
public bool TryGetIndices(int linearIndex, Span<int> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Indices.

#### `TryGetLinearIndex`

```csharp
public bool TryGetLinearIndex(ReadOnlySpan<int> indices, out int linearIndex)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Linear Index.

#### `Dimensions`

```csharp
public ReadOnlyMemory<int> Dimensions { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets dimensions.

#### `ElementCount`

```csharp
public int ElementCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets element Count.

#### `Rank`

```csharp
public int Rank { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets rank.

## DriverByteOrder

```csharp
public enum DriverByteOrder
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Byte Order choices. Select only values documented by the target protocol or device profile.

### Members

#### `BigEndian`

```csharp
BigEndian = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the big Endian option.

#### `LittleEndian`

```csharp
LittleEndian = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the little Endian option.

## DriverCapabilities

```csharp
public struct DriverCapabilities
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Capabilities in the public driver contract.

### Members

#### `DriverCapabilities`

```csharp
public DriverCapabilities(string Protocol, DriverFeature Features, ushort MaximumReadRegisters, ushort MaximumWriteRegisters, ushort MaximumReadBits, ushort MaximumWriteBits, bool SupportsConcurrentRequests)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverCapabilities` instance from the supplied values.

#### `Supports`

```csharp
public bool Supports(DriverFeature feature)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs supports.

#### `Features`

```csharp
public DriverFeature Features { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets features.

#### `MaximumReadBits`

```csharp
public ushort MaximumReadBits { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Read Bits.

#### `MaximumReadRegisters`

```csharp
public ushort MaximumReadRegisters { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Read Registers.

#### `MaximumWriteBits`

```csharp
public ushort MaximumWriteBits { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Write Bits.

#### `MaximumWriteRegisters`

```csharp
public ushort MaximumWriteRegisters { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets maximum Write Registers.

#### `Protocol`

```csharp
public string Protocol { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets protocol.

#### `SupportsConcurrentRequests`

```csharp
public bool SupportsConcurrentRequests { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets supports Concurrent Requests.

## DriverConnectionDisposition

```csharp
public enum DriverConnectionDisposition
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Connection Disposition choices. Select only values documented by the target protocol or device profile.

### Members

#### `Unknown`

```csharp
Unknown = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unknown option.

#### `Healthy`

```csharp
Healthy = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the healthy option.

#### `Unchanged`

```csharp
Unchanged = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unchanged option.

#### `Broken`

```csharp
Broken = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the broken option.

## DriverDynamicArray

```csharp
public sealed class DriverDynamicArray
```

Runtime-sized homogeneous array with explicit rank/dimensions.

### Members

#### `Create`

```csharp
public static DriverResult<DriverDynamicArray> Create(IndustrialDataType elementType, ReadOnlySpan<int> dimensions, ReadOnlySpan<DriverValue> values)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `Equals`

```csharp
public bool Equals(DriverDynamicArray other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `Count`

```csharp
public int Count { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets count.

#### `ElementType`

```csharp
public IndustrialDataType ElementType { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets element Type.

#### `Shape`

```csharp
public DriverArrayShape Shape { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets shape.

#### `Values`

```csharp
public ReadOnlyMemory<DriverValue> Values { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets values.

## DriverEnumerationMember

```csharp
public struct DriverEnumerationMember
```

One symbolic member of a PLC/industrial enumeration.

### Members

#### `DriverEnumerationMember`

```csharp
public DriverEnumerationMember(string Name, DriverValue Value)
```

One symbolic member of a PLC/industrial enumeration.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Value`

```csharp
public DriverValue Value { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value.

## DriverEnumerationSchema

```csharp
public sealed class DriverEnumerationSchema
```

Immutable, AOT-safe symbolic enumeration metadata. It deliberately stays separate from protocol wire descriptors: the schema owns names, while Modbus/S7 descriptors own the signedness and storage width. Construction is a configuration-path operation; lookup is allocation-free and optimized for the small member counts typical of controller enums.

### Members

#### `Create`

```csharp
public static DriverResult<DriverEnumerationSchema> Create(string name, ReadOnlySpan<DriverEnumerationMember> members)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `TryGetName`

```csharp
public bool TryGetName(DriverValue value, out string name)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Name.

#### `TryGetValue`

```csharp
public bool TryGetValue(string name, out DriverValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Value.

#### `Members`

```csharp
public ReadOnlyMemory<DriverEnumerationMember> Members { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets members.

#### `Name`

```csharp
public string Name { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets name.

## DriverError

```csharp
public struct DriverError
```

Structured, protocol-neutral diagnostics. Error construction is off the healthy hot path; user-facing prose is materialized only when `DriverError.ToUserMessage` is requested.

### Members

#### `DriverError`

```csharp
public DriverError(DriverErrorKind Kind, string Code, string Message, int? NativeCode = null, DriverOperation Operation = Unknown, string Protocol = null, string Endpoint = null, string Address = null, IndustrialDataType? DataType = null, string NativeMeaning = null, string Cause = null, string SuggestedAction = null, DriverRetryDisposition Retry = Unknown, DriverConnectionDisposition Connection = Unknown)
```

Structured, protocol-neutral diagnostics. Error construction is off the healthy hot path; user-facing prose is materialized only when `DriverError.ToUserMessage` is requested.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `ToUserMessage`

```csharp
public string ToUserMessage()
```

Creates concise field-engineer-facing guidance without changing the structured error.

#### `WithContext`

```csharp
public DriverError WithContext(string protocol, DriverOperation operation, string endpoint = null, string address = null, IndustrialDataType? dataType = null, DriverRetryDisposition? retry = null, DriverConnectionDisposition? connection = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs with Context.

#### `Address`

```csharp
public string Address { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets address.

#### `Cause`

```csharp
public string Cause { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets cause.

#### `Code`

```csharp
public string Code { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets code.

#### `Connection`

```csharp
public DriverConnectionDisposition Connection { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets connection.

#### `DataType`

```csharp
public IndustrialDataType? DataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Type.

#### `Endpoint`

```csharp
public string Endpoint { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets endpoint.

#### `IsNone`

```csharp
public bool IsNone { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is None.

#### `Kind`

```csharp
public DriverErrorKind Kind { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets kind.

#### `Message`

```csharp
public string Message { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets message.

#### `NativeCode`

```csharp
public int? NativeCode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets native Code.

#### `NativeMeaning`

```csharp
public string NativeMeaning { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets native Meaning.

#### `None`

```csharp
public static DriverError None { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets none.

#### `Operation`

```csharp
public DriverOperation Operation { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets operation.

#### `Protocol`

```csharp
public string Protocol { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets protocol.

#### `Retry`

```csharp
public DriverRetryDisposition Retry { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets retry.

#### `SuggestedAction`

```csharp
public string SuggestedAction { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets suggested Action.

## DriverErrorKind

```csharp
public enum DriverErrorKind
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Error Kind choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `Configuration`

```csharp
Configuration = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the configuration option.

#### `NotSupported`

```csharp
NotSupported = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the not Supported option.

#### `Cancelled`

```csharp
Cancelled = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the cancelled option.

#### `Timeout`

```csharp
Timeout = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the timeout option.

#### `Transport`

```csharp
Transport = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the transport option.

#### `Protocol`

```csharp
Protocol = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the protocol option.

#### `Device`

```csharp
Device = 7
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the device option.

#### `Data`

```csharp
Data = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the data option.

#### `Disposed`

```csharp
Disposed = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the disposed option.

## DriverExtensionObject

```csharp
public sealed class DriverExtensionObject
```

Lossless ExtensionObject envelope: original encoded body plus optional canonical decoded value.

### Members

#### `DriverExtensionObject`

```csharp
public DriverExtensionObject(IndustrialExpandedNodeId typeId, DriverExtensionObjectEncoding encoding, ReadOnlySpan<byte> encodedBody, DriverValue? decodedValue = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverExtensionObject` instance from the supplied values.

#### `Equals`

```csharp
public bool Equals(DriverExtensionObject other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `DecodedValue`

```csharp
public DriverValue? DecodedValue { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets decoded Value.

#### `EncodedBody`

```csharp
public ReadOnlyMemory<byte> EncodedBody { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets encoded Body.

#### `Encoding`

```csharp
public DriverExtensionObjectEncoding Encoding { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets encoding.

#### `HasDecodedValue`

```csharp
public bool HasDecodedValue { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets has Decoded Value.

#### `TypeId`

```csharp
public IndustrialExpandedNodeId TypeId { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets type Id.

## DriverExtensionObjectEncoding

```csharp
public enum DriverExtensionObjectEncoding
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Extension Object Encoding choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `Binary`

```csharp
Binary = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the binary option.

#### `Xml`

```csharp
Xml = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the xml option.

#### `Json`

```csharp
Json = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the json option.

## DriverFeature

```csharp
public enum DriverFeature
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Feature choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `ReadBits`

```csharp
ReadBits = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Bits option.

#### `ReadRegisters`

```csharp
ReadRegisters = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Registers option.

#### `WriteBits`

```csharp
WriteBits = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Bits option.

#### `WriteRegisters`

```csharp
WriteRegisters = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Registers option.

#### `ReadMultipleContiguous`

```csharp
ReadMultipleContiguous = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Multiple Contiguous option.

#### `WriteMultipleContiguous`

```csharp
WriteMultipleContiguous = 32
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Multiple Contiguous option.

#### `PersistentConnection`

```csharp
PersistentConnection = 64
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the persistent Connection option.

#### `NativeAsyncIo`

```csharp
NativeAsyncIo = 128
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the native Async Io option.

#### `Cancellation`

```csharp
Cancellation = 256
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the cancellation option.

#### `AddressCompilation`

```csharp
AddressCompilation = 512
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the address Compilation option.

#### `TypedArrays`

```csharp
TypedArrays = 1024
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the typed Arrays option.

#### `ArbitraryBatchRead`

```csharp
ArbitraryBatchRead = 2048
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the arbitrary Batch Read option.

#### `ArbitraryBatchWrite`

```csharp
ArbitraryBatchWrite = 4096
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the arbitrary Batch Write option.

#### `MaskWriteRegister`

```csharp
MaskWriteRegister = 8192
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the mask Write Register option.

#### `ReadWriteMultipleRegisters`

```csharp
ReadWriteMultipleRegisters = 16384
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Write Multiple Registers option.

#### `CompiledReadPlans`

```csharp
CompiledReadPlans = 32768
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the compiled Read Plans option.

#### `CompiledWritePlans`

```csharp
CompiledWritePlans = 65536
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the compiled Write Plans option.

#### `DiagnosticsServices`

```csharp
DiagnosticsServices = 131072
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the diagnostics Services option.

#### `FileRecordServices`

```csharp
FileRecordServices = 262144
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the file Record Services option.

#### `FifoQueue`

```csharp
FifoQueue = 524288
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the fifo Queue option.

#### `DeviceIdentification`

```csharp
DeviceIdentification = 1048576
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the device Identification option.

#### `RawProtocolPdu`

```csharp
RawProtocolPdu = 2097152
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the raw Protocol Pdu option.

#### `EncapsulatedInterfaceTransport`

```csharp
EncapsulatedInterfaceTransport = 4194304
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the encapsulated Interface Transport option.

## DriverOperation

```csharp
public enum DriverOperation
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Operation choices. Select only values documented by the target protocol or device profile.

### Members

#### `Unknown`

```csharp
Unknown = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unknown option.

#### `Validate`

```csharp
Validate = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the validate option.

#### `Connect`

```csharp
Connect = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the connect option.

#### `Disconnect`

```csharp
Disconnect = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the disconnect option.

#### `Handshake`

```csharp
Handshake = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the handshake option.

#### `Negotiate`

```csharp
Negotiate = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the negotiate option.

#### `Read`

```csharp
Read = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read option.

#### `ReadBlock`

```csharp
ReadBlock = 7
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Block option.

#### `ReadBatch`

```csharp
ReadBatch = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the read Batch option.

#### `Write`

```csharp
Write = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write option.

#### `WriteBlock`

```csharp
WriteBlock = 10
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Block option.

#### `WriteBatch`

```csharp
WriteBatch = 11
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the write Batch option.

#### `Encode`

```csharp
Encode = 12
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the encode option.

#### `Decode`

```csharp
Decode = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the decode option.

#### `Diagnose`

```csharp
Diagnose = 14
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the diagnose option.

#### `Identify`

```csharp
Identify = 15
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the identify option.

#### `Execute`

```csharp
Execute = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the execute option.

#### `Browse`

```csharp
Browse = 17
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the browse option.

#### `Discover`

```csharp
Discover = 18
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the discover option.

#### `Subscribe`

```csharp
Subscribe = 19
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the subscribe option.

#### `Publish`

```csharp
Publish = 20
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the publish option.

#### `Republish`

```csharp
Republish = 21
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the republish option.

## DriverPackedField

```csharp
public struct DriverPackedField
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Packed Field in the public driver contract.

### Members

#### `DriverPackedField`

```csharp
public DriverPackedField(string Name, IndustrialDataType DataType, int ValueOffset, int ValueCount, int ByteOffset, int ByteLength, DriverByteOrder ByteOrder, DriverArrayShape Shape = null)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverPackedField` instance from the supplied values.

#### `ByteLength`

```csharp
public int ByteLength { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Length.

#### `ByteOffset`

```csharp
public int ByteOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Offset.

#### `ByteOrder`

```csharp
public DriverByteOrder ByteOrder { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets byte Order.

#### `DataType`

```csharp
public IndustrialDataType DataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Type.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Shape`

```csharp
public DriverArrayShape Shape { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets shape.

#### `ValueCount`

```csharp
public int ValueCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Count.

#### `ValueOffset`

```csharp
public int ValueOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Offset.

## DriverPackedLayout

```csharp
public sealed class DriverPackedLayout
```

Explicit byte layout for packed UDTs, telegram payloads and vendor-defined register blocks. Padding/alignment is fixed at configuration time and can be projected to protocol descriptors.

### Members

#### `Project`

```csharp
public DriverResult<DriverRecordSchema<TDescriptor>> Project<TDescriptor>(Func<DriverPackedField, TDescriptor> descriptorMapper)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs project.

#### `ByteLength`

```csharp
public int ByteLength { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets byte Length.

#### `Fields`

```csharp
public ReadOnlyMemory<DriverPackedField> Fields { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets fields.

#### `PaddingByteCount`

```csharp
public int PaddingByteCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets padding Byte Count.

#### `ValueCount`

```csharp
public int ValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value Count.

## DriverPackedLayoutBuilder

```csharp
public sealed class DriverPackedLayoutBuilder
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Packed Layout Builder in the public driver contract.

### Members

#### `DriverPackedLayoutBuilder`

```csharp
public DriverPackedLayoutBuilder()
```

Creates an instance with default member values.

#### `AddArrayField`

```csharp
public DriverPackedLayoutBuilder AddArrayField(string name, IndustrialDataType dataType, DriverArrayShape shape, DriverByteOrder byteOrder = BigEndian)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs add Array Field.

#### `AddField`

```csharp
public DriverPackedLayoutBuilder AddField(string name, IndustrialDataType dataType, DriverByteOrder byteOrder = BigEndian, int elementCount = 1)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs add Field.

#### `Align`

```csharp
public DriverPackedLayoutBuilder Align(int alignment)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs align.

#### `Build`

```csharp
public DriverResult<DriverPackedLayout> Build()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs build.

#### `Pad`

```csharp
public DriverPackedLayoutBuilder Pad(int byteCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs pad.

#### `CurrentByteOffset`

```csharp
public int CurrentByteOffset { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets current Byte Offset.

#### `CurrentValueOffset`

```csharp
public int CurrentValueOffset { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets current Value Offset.

## DriverPackedLayoutCodec

```csharp
public static class DriverPackedLayoutCodec
```

Canonical fixed-width packed-layout codec. It is intentionally protocol-neutral: the layout explicitly selects byte order. Protocol-native formats such as S7 DTL or Modbus BCD remain in their protocol codecs.

### Members

#### `Decode`

```csharp
public static DriverResult<int> Decode(DriverPackedLayout layout, ReadOnlySpan<byte> source, Span<DriverValue> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Decodes the requested value from its wire representation.

#### `Encode`

```csharp
public static DriverResult<int> Encode(DriverPackedLayout layout, ReadOnlySpan<DriverValue> source, Span<byte> destination)
```

*Undocumented: no XML summary; this text is generated from the name.* Encodes the requested value into its wire representation.

## DriverQuality

```csharp
public struct DriverQuality
```

Protocol-neutral quality plus an optional native status code.

### Members

#### `DriverQuality`

```csharp
public DriverQuality(DriverQualityLevel Level, DriverQualityFlags Flags = None, uint NativeStatusCode = 0)
```

Protocol-neutral quality plus an optional native status code.

#### `Flags`

```csharp
public DriverQualityFlags Flags { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets flags.

#### `Good`

```csharp
public static DriverQuality Good { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets good.

#### `IsGood`

```csharp
public bool IsGood { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Good.

#### `Level`

```csharp
public DriverQualityLevel Level { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets level.

#### `NativeStatusCode`

```csharp
public uint NativeStatusCode { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets native Status Code.

## DriverQualityFlags

```csharp
public enum DriverQualityFlags
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Quality Flags choices. Select only values documented by the target protocol or device profile.

### Members

#### `None`

```csharp
None = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the none option.

#### `Stale`

```csharp
Stale = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the stale option.

#### `Substituted`

```csharp
Substituted = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the substituted option.

#### `OverRange`

```csharp
OverRange = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the over Range option.

#### `UnderRange`

```csharp
UnderRange = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the under Range option.

#### `SensorFailure`

```csharp
SensorFailure = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the sensor Failure option.

#### `CommunicationFailure`

```csharp
CommunicationFailure = 32
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the communication Failure option.

#### `ConfigurationError`

```csharp
ConfigurationError = 64
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the configuration Error option.

## DriverQualityLevel

```csharp
public enum DriverQualityLevel
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Quality Level choices. Select only values documented by the target protocol or device profile.

### Members

#### `Good`

```csharp
Good = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the good option.

#### `Uncertain`

```csharp
Uncertain = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uncertain option.

#### `Bad`

```csharp
Bad = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bad option.

## DriverReadBatchItem<TDescriptor>

```csharp
public struct DriverReadBatchItem<TDescriptor>
```

One heterogeneous read inside an arbitrary batch operation.

### Members

#### `DriverReadBatchItem`1`

```csharp
public DriverReadBatchItem<TDescriptor>(int DestinationOffset, TDescriptor Descriptor)
```

One heterogeneous read inside an arbitrary batch operation.

#### `Descriptor`

```csharp
public TDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `DestinationOffset`

```csharp
public int DestinationOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets destination Offset.

## DriverRecordArrayMetadata

```csharp
public struct DriverRecordArrayMetadata
```

Shape metadata for a scalar array or record array inside a flattened record schema.

### Members

#### `DriverRecordArrayMetadata`

```csharp
public DriverRecordArrayMetadata(string Name, int ValueOffset, int ElementValueCount, DriverArrayShape Shape)
```

Shape metadata for a scalar array or record array inside a flattened record schema.

#### `TryGetElementValueOffset`

```csharp
public bool TryGetElementValueOffset(ReadOnlySpan<int> indices, out int valueOffset)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Element Value Offset.

#### `ElementValueCount`

```csharp
public int ElementValueCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets element Value Count.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Shape`

```csharp
public DriverArrayShape Shape { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets shape.

#### `ValueCount`

```csharp
public int ValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value Count.

#### `ValueOffset`

```csharp
public int ValueOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Offset.

## DriverRecordField<TDescriptor>

```csharp
public struct DriverRecordField<TDescriptor>
```

One named field inside a schema-driven industrial STRUCT/UDT. ValueOffset addresses the first slot in the caller's flat DriverValue buffer; ValueCount is the number of logical values consumed by the descriptor (1 for a scalar, N for an array).

### Members

#### `DriverRecordField`1`

```csharp
public DriverRecordField<TDescriptor>(string Name, int ValueOffset, int ValueCount, TDescriptor Descriptor)
```

One named field inside a schema-driven industrial STRUCT/UDT. ValueOffset addresses the first slot in the caller's flat DriverValue buffer; ValueCount is the number of logical values consumed by the descriptor (1 for a scalar, N for an array).

#### `Descriptor`

```csharp
public TDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `ValueCount`

```csharp
public int ValueCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Count.

#### `ValueOffset`

```csharp
public int ValueOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Offset.

## DriverRecordFieldValue

```csharp
public struct DriverRecordFieldValue
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Record Field Value in the public driver contract.

### Members

#### `DriverRecordFieldValue`

```csharp
public DriverRecordFieldValue(string Name, bool IsPresent, DriverValue Value)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverRecordFieldValue` instance from the supplied values.

#### `Absent`

```csharp
public static DriverRecordFieldValue Absent(string name)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs absent.

#### `Present`

```csharp
public static DriverRecordFieldValue Present(string name, DriverValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs present.

#### `IsPresent`

```csharp
public bool IsPresent { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets is Present.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Value`

```csharp
public DriverValue Value { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value.

## DriverRecordPlanStatistics

```csharp
public struct DriverRecordPlanStatistics
```

Stable configuration-time metrics for a compiled record/batch plan.

### Members

#### `DriverRecordPlanStatistics`

```csharp
public DriverRecordPlanStatistics(int FieldCount, int ValueCount, int BlockCount, int PhysicalUnitCount, int BaselineReadBlockCount = 0)
```

Stable configuration-time metrics for a compiled record/batch plan.

#### `BaselineReadBlockCount`

```csharp
public int BaselineReadBlockCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets baseline Read Block Count.

#### `BlockCount`

```csharp
public int BlockCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets block Count.

#### `FieldCount`

```csharp
public int FieldCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets field Count.

#### `PhysicalUnitCount`

```csharp
public int PhysicalUnitCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets physical Unit Count.

#### `ValueCount`

```csharp
public int ValueCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Count.

## DriverRecordSchema<TDescriptor>

```csharp
public sealed class DriverRecordSchema<TDescriptor>
```

AOT-safe protocol-neutral STRUCT/UDT schema. The driver layer deliberately exposes a flat DriverValue buffer rather than reflection-based POCO materialization. This makes nested PLC types deterministic, source-generator friendly and usable from Native AOT.

### Members

#### `Create`

```csharp
public static DriverResult<DriverRecordSchema<TDescriptor>> Create(ReadOnlySpan<DriverRecordField<TDescriptor>> fields)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `TryGetArray`

```csharp
public bool TryGetArray(string name, out DriverRecordArrayMetadata array)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Array.

#### `TryGetField`

```csharp
public bool TryGetField(string name, out DriverRecordField<TDescriptor> field)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Field.

#### `Arrays`

```csharp
public ReadOnlyMemory<DriverRecordArrayMetadata> Arrays { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets arrays.

#### `Fields`

```csharp
public ReadOnlyMemory<DriverRecordField<TDescriptor>> Fields { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets fields.

#### `ReadItems`

```csharp
public ReadOnlyMemory<DriverReadBatchItem<TDescriptor>> ReadItems { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets read Items.

#### `ValueCount`

```csharp
public int ValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value Count.

#### `WriteItems`

```csharp
public ReadOnlyMemory<DriverWriteBatchItem<TDescriptor>> WriteItems { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets write Items.

## DriverRecordSchemaBuilder<TDescriptor>

```csharp
public sealed class DriverRecordSchemaBuilder<TDescriptor>
```

Configuration-time, reflection-free builder for nested industrial STRUCT/UDT layouts. Nested records are flattened into leaf descriptors once; runtime execution stays on the existing heterogeneous batch/compiled-plan path.

### Members

#### `DriverRecordSchemaBuilder`1`

```csharp
public DriverRecordSchemaBuilder<TDescriptor>()
```

Creates an instance with default member values.

#### `AddArrayField`

```csharp
public DriverRecordSchemaBuilder<TDescriptor> AddArrayField(string name, TDescriptor descriptor, DriverArrayShape shape)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs add Array Field.

#### `AddField`

```csharp
public DriverRecordSchemaBuilder<TDescriptor> AddField(string name, TDescriptor descriptor, int valueCount = 1)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs add Field.

#### `AddRecord`

```csharp
public DriverRecordSchemaBuilder<TDescriptor> AddRecord(string name, DriverRecordSchema<TDescriptor> schema, Func<TDescriptor, TDescriptor> descriptorMapper = null)
```

Appends a nested record. descriptorMapper is configuration-time only and can rebase relative child descriptors onto a parent device address without reflection.

#### `AddRecordArray`

```csharp
public DriverRecordSchemaBuilder<TDescriptor> AddRecordArray(string name, int count, DriverRecordSchema<TDescriptor> elementSchema, Func<int, TDescriptor, TDescriptor> descriptorMapper)
```

Appends an array of nested records. The mapper is intentionally mandatory: every element must explicitly project child descriptors to its own physical PLC region.

#### `AddRecordArray`

```csharp
public DriverRecordSchemaBuilder<TDescriptor> AddRecordArray(string name, DriverArrayShape shape, DriverRecordSchema<TDescriptor> elementSchema, Func<int, TDescriptor, TDescriptor> descriptorMapper)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs add Record Array.

#### `Build`

```csharp
public DriverResult<DriverRecordSchema<TDescriptor>> Build()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs build.

#### `ValueCount`

```csharp
public int ValueCount { get;  }
```

Number of logical DriverValue slots currently assigned by the builder.

## DriverRecordValue

```csharp
public sealed class DriverRecordValue
```

Ordered runtime record preserving optional-field presence independently from Null.

### Members

#### `Create`

```csharp
public static DriverResult<DriverRecordValue> Create(string typeName, ReadOnlySpan<DriverRecordFieldValue> fields)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `Equals`

```csharp
public bool Equals(DriverRecordValue other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `TryGetField`

```csharp
public bool TryGetField(string name, out DriverRecordFieldValue field)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Get Field.

#### `Fields`

```csharp
public ReadOnlyMemory<DriverRecordFieldValue> Fields { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets fields.

#### `TypeName`

```csharp
public string TypeName { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets type Name.

## DriverResult

```csharp
public struct DriverResult
```

*Undocumented: no XML summary; this text is generated from the name.* Carries the structured result of driver, including protocol status where applicable.

### Members

#### `Failure`

```csharp
public static DriverResult Failure(DriverError error)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs failure.

#### `Success`

```csharp
public static DriverResult Success()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs success.

#### `Error`

```csharp
public DriverError Error { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets error.

#### `IsSuccess`

```csharp
public bool IsSuccess { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Success.

## DriverResult<T>

```csharp
public struct DriverResult<T>
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Result`1 in the public driver contract.

### Members

#### `Failure`

```csharp
public static DriverResult<T> Failure(DriverError error)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs failure.

#### `Success`

```csharp
public static DriverResult<T> Success(T value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs success.

#### `Error`

```csharp
public DriverError Error { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets error.

#### `IsSuccess`

```csharp
public bool IsSuccess { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Success.

#### `Value`

```csharp
public T Value { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value.

## DriverRetryDisposition

```csharp
public enum DriverRetryDisposition
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported driver Retry Disposition choices. Select only values documented by the target protocol or device profile.

### Members

#### `Unknown`

```csharp
Unknown = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the unknown option.

#### `DoNotRetry`

```csharp
DoNotRetry = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the do Not Retry option.

#### `RetrySameConnection`

```csharp
RetrySameConnection = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the retry Same Connection option.

#### `ReconnectAndRetry`

```csharp
ReconnectAndRetry = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the reconnect And Retry option.

#### `FixConfiguration`

```csharp
FixConfiguration = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the fix Configuration option.

## DriverSample

```csharp
public struct DriverSample
```

One value with industrial quality/status and source/server timestamps.

### Members

#### `DriverSample`

```csharp
public DriverSample(DriverValue Value, DriverQuality Quality, DateTime SourceTimestampUtc, DateTime ServerTimestampUtc, ushort SourcePicoseconds = 0, ushort ServerPicoseconds = 0)
```

One value with industrial quality/status and source/server timestamps.

#### `Good`

```csharp
public static DriverSample Good(DriverValue value, DateTime sourceTimestampUtc, DateTime serverTimestampUtc)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs good.

#### `Quality`

```csharp
public DriverQuality Quality { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets quality.

#### `ServerPicoseconds`

```csharp
public ushort ServerPicoseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server Picoseconds.

#### `ServerTimestampUtc`

```csharp
public DateTime ServerTimestampUtc { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server Timestamp Utc.

#### `SourcePicoseconds`

```csharp
public ushort SourcePicoseconds { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets source Picoseconds.

#### `SourceTimestampUtc`

```csharp
public DateTime SourceTimestampUtc { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets source Timestamp Utc.

#### `Value`

```csharp
public DriverValue Value { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value.

## DriverValue

```csharp
public struct DriverValue
```

Tagged industrial scalar union. All fixed-width numeric, temporal, Guid and bit-string values are stored inline without boxing. String and Bytes retain their natural managed references. The struct deliberately carries semantic type information so a WORD is not silently treated as an unsigned engineering number and a Date is not flattened to Int32.

### Members

#### `AsArray`

```csharp
public DriverDynamicArray AsArray()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Array.

#### `AsBitString16`

```csharp
public ushort AsBitString16()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Bit String16.

#### `AsBitString32`

```csharp
public uint AsBitString32()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Bit String32.

#### `AsBitString64`

```csharp
public ulong AsBitString64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Bit String64.

#### `AsBitString8`

```csharp
public byte AsBitString8()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Bit String8.

#### `AsBoolean`

```csharp
public bool AsBoolean()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Boolean.

#### `AsBytes`

```csharp
public ReadOnlyMemory<byte> AsBytes()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Bytes.

#### `AsChar16`

```csharp
public char AsChar16()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Char16.

#### `AsChar8`

```csharp
public char AsChar8()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Char8.

#### `AsDate`

```csharp
public DateOnly AsDate()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Date.

#### `AsDateTime`

```csharp
public DateTime AsDateTime()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Date Time.

#### `AsDecimal`

```csharp
public decimal AsDecimal()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Decimal.

#### `AsDuration`

```csharp
public TimeSpan AsDuration()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Duration.

#### `AsEnumerationInt64`

```csharp
public long AsEnumerationInt64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Enumeration Int64.

#### `AsEnumerationUInt64`

```csharp
public ulong AsEnumerationUInt64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Enumeration UInt64.

#### `AsExpandedNodeId`

```csharp
public IndustrialExpandedNodeId AsExpandedNodeId()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Expanded Node Id.

#### `AsExtensionObject`

```csharp
public DriverExtensionObject AsExtensionObject()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Extension Object.

#### `AsFloat16`

```csharp
public Half AsFloat16()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Float16.

#### `AsFloat32`

```csharp
public float AsFloat32()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Float32.

#### `AsFloat64`

```csharp
public double AsFloat64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Float64.

#### `AsGuid`

```csharp
public Guid AsGuid()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Guid.

#### `AsInt128`

```csharp
public Int128 AsInt128()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Int128.

#### `AsInt16`

```csharp
public short AsInt16()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Int16.

#### `AsInt32`

```csharp
public int AsInt32()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Int32.

#### `AsInt64`

```csharp
public long AsInt64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Int64.

#### `AsInt8`

```csharp
public sbyte AsInt8()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Int8.

#### `AsLocalizedText`

```csharp
public IndustrialLocalizedText AsLocalizedText()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Localized Text.

#### `AsNodeId`

```csharp
public IndustrialNodeId AsNodeId()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Node Id.

#### `AsQualifiedName`

```csharp
public IndustrialQualifiedName AsQualifiedName()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Qualified Name.

#### `AsRecord`

```csharp
public DriverRecordValue AsRecord()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Record.

#### `AsStatusCode`

```csharp
public uint AsStatusCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Status Code.

#### `AsString`

```csharp
public string AsString()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as String.

#### `AsTimeOfDay`

```csharp
public TimeOnly AsTimeOfDay()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Time Of Day.

#### `AsUInt128`

```csharp
public UInt128 AsUInt128()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as UInt128.

#### `AsUInt16`

```csharp
public ushort AsUInt16()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as UInt16.

#### `AsUInt32`

```csharp
public uint AsUInt32()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as UInt32.

#### `AsUInt64`

```csharp
public ulong AsUInt64()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as UInt64.

#### `AsUInt8`

```csharp
public byte AsUInt8()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as UInt8.

#### `AsVariant`

```csharp
public DriverValue AsVariant()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Variant.

#### `AsXmlElement`

```csharp
public string AsXmlElement()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs as Xml Element.

#### `Equals`

```csharp
public bool Equals(DriverValue other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `FromArray`

```csharp
public static DriverValue FromArray(DriverDynamicArray value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Array.

#### `FromBitString16`

```csharp
public static DriverValue FromBitString16(ushort value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Bit String16.

#### `FromBitString32`

```csharp
public static DriverValue FromBitString32(uint value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Bit String32.

#### `FromBitString64`

```csharp
public static DriverValue FromBitString64(ulong value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Bit String64.

#### `FromBitString8`

```csharp
public static DriverValue FromBitString8(byte value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Bit String8.

#### `FromBoolean`

```csharp
public static DriverValue FromBoolean(bool value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Boolean.

#### `FromBytes`

```csharp
public static DriverValue FromBytes(ReadOnlySpan<byte> value)
```

Copies the supplied bytes so the value remains immutable to the caller.

#### `FromChar16`

```csharp
public static DriverValue FromChar16(char value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Char16.

#### `FromChar8`

```csharp
public static DriverValue FromChar8(char value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Char8.

#### `FromDate`

```csharp
public static DriverValue FromDate(DateOnly value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Date.

#### `FromDateTime`

```csharp
public static DriverValue FromDateTime(DateTime value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Date Time.

#### `FromDecimal`

```csharp
public static DriverValue FromDecimal(decimal value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Decimal.

#### `FromDuration`

```csharp
public static DriverValue FromDuration(TimeSpan value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Duration.

#### `FromEnumeration`

```csharp
public static DriverValue FromEnumeration(long value)
```

Creates a signed symbolic enumeration value. The protocol descriptor selects the storage width.

#### `FromExpandedNodeId`

```csharp
public static DriverValue FromExpandedNodeId(IndustrialExpandedNodeId value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Expanded Node Id.

#### `FromExtensionObject`

```csharp
public static DriverValue FromExtensionObject(DriverExtensionObject value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Extension Object.

#### `FromFloat16`

```csharp
public static DriverValue FromFloat16(Half value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Float16.

#### `FromFloat32`

```csharp
public static DriverValue FromFloat32(float value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Float32.

#### `FromFloat64`

```csharp
public static DriverValue FromFloat64(double value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Float64.

#### `FromGuid`

```csharp
public static DriverValue FromGuid(Guid value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Guid.

#### `FromInt128`

```csharp
public static DriverValue FromInt128(Int128 value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Int128.

#### `FromInt16`

```csharp
public static DriverValue FromInt16(short value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Int16.

#### `FromInt32`

```csharp
public static DriverValue FromInt32(int value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Int32.

#### `FromInt64`

```csharp
public static DriverValue FromInt64(long value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Int64.

#### `FromInt8`

```csharp
public static DriverValue FromInt8(sbyte value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Int8.

#### `FromLocalizedText`

```csharp
public static DriverValue FromLocalizedText(IndustrialLocalizedText value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Localized Text.

#### `FromNodeId`

```csharp
public static DriverValue FromNodeId(IndustrialNodeId value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Node Id.

#### `FromOwnedBytes`

```csharp
public static DriverValue FromOwnedBytes(byte[] value)
```

Transfers ownership of an existing array. The caller must not mutate it afterwards.

#### `FromQualifiedName`

```csharp
public static DriverValue FromQualifiedName(IndustrialQualifiedName value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Qualified Name.

#### `FromRecord`

```csharp
public static DriverValue FromRecord(DriverRecordValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Record.

#### `FromStatusCode`

```csharp
public static DriverValue FromStatusCode(uint value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Status Code.

#### `FromString`

```csharp
public static DriverValue FromString(string value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from String.

#### `FromTimeOfDay`

```csharp
public static DriverValue FromTimeOfDay(TimeOnly value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Time Of Day.

#### `FromUInt128`

```csharp
public static DriverValue FromUInt128(UInt128 value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from UInt128.

#### `FromUInt16`

```csharp
public static DriverValue FromUInt16(ushort value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from UInt16.

#### `FromUInt32`

```csharp
public static DriverValue FromUInt32(uint value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from UInt32.

#### `FromUInt64`

```csharp
public static DriverValue FromUInt64(ulong value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from UInt64.

#### `FromUInt8`

```csharp
public static DriverValue FromUInt8(byte value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from UInt8.

#### `FromUnsignedEnumeration`

```csharp
public static DriverValue FromUnsignedEnumeration(ulong value)
```

Creates an unsigned symbolic enumeration value. The protocol descriptor selects the storage width.

#### `FromVariant`

```csharp
public static DriverValue FromVariant(DriverValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Variant.

#### `FromXmlElement`

```csharp
public static DriverValue FromXmlElement(string value)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs from Xml Element.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `ToInvariantString`

```csharp
public string ToInvariantString()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs to Invariant String.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `IsEnumerationUnsigned`

```csharp
public bool IsEnumerationUnsigned { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Enumeration Unsigned.

#### `IsNull`

```csharp
public bool IsNull { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets is Null.

#### `Null`

```csharp
public static DriverValue Null { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets null.

#### `Type`

```csharp
public IndustrialDataType Type { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets type.

## DriverVariantCase<TDescriptor>

```csharp
public struct DriverVariantCase<TDescriptor>
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Variant Case`1 in the public driver contract.

### Members

#### `DriverVariantCase`1`

```csharp
public DriverVariantCase<TDescriptor>(string Name, DriverValue Discriminator, DriverRecordSchema<TDescriptor> Schema)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverVariantCase<TDescriptor>` instance from the supplied values.

#### `Discriminator`

```csharp
public DriverValue Discriminator { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discriminator.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `Schema`

```csharp
public DriverRecordSchema<TDescriptor> Schema { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets schema.

## DriverVariantSchema<TDescriptor>

```csharp
public sealed class DriverVariantSchema<TDescriptor>
```

Reflection-free discriminated union schema. Alternatives may overlay the same physical PLC memory; the discriminator determines which record schema is active.

### Members

#### `Create`

```csharp
public static DriverResult<DriverVariantSchema<TDescriptor>> Create(string name, ReadOnlySpan<DriverVariantCase<TDescriptor>> cases)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `TryResolve`

```csharp
public bool TryResolve(DriverValue discriminator, out DriverVariantCase<TDescriptor> variantCase)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs try Resolve.

#### `Cases`

```csharp
public ReadOnlyMemory<DriverVariantCase<TDescriptor>> Cases { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets cases.

#### `DiscriminatorType`

```csharp
public IndustrialDataType DiscriminatorType { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets discriminator Type.

#### `MaximumValueCount`

```csharp
public int MaximumValueCount { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets maximum Value Count.

#### `Name`

```csharp
public string Name { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets name.

## DriverVariantSelection

```csharp
public struct DriverVariantSelection
```

*Undocumented: no XML summary; this text is generated from the name.* Represents driver Variant Selection in the public driver contract.

### Members

#### `DriverVariantSelection`

```csharp
public DriverVariantSelection(string CaseName, DriverValue Discriminator, int ValueCount)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverVariantSelection` instance from the supplied values.

#### `CaseName`

```csharp
public string CaseName { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets case Name.

#### `Discriminator`

```csharp
public DriverValue Discriminator { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets discriminator.

#### `ValueCount`

```csharp
public int ValueCount { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets value Count.

## DriverVariantValue

```csharp
public sealed class DriverVariantValue
```

Explicit Variant wrapper preserving that a value came from a dynamic variant container.

### Members

#### `DriverVariantValue`

```csharp
public DriverVariantValue(DriverValue value)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `DriverVariantValue` instance from the supplied values.

#### `Equals`

```csharp
public bool Equals(DriverVariantValue other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `Value`

```csharp
public DriverValue Value { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets value.

## DriverWriteBatchItem<TDescriptor>

```csharp
public struct DriverWriteBatchItem<TDescriptor>
```

One heterogeneous write inside an arbitrary batch operation. SourceOffset points at the first DriverValue for the descriptor; arrays consume their protocol descriptor's element count. Batch execution is ordered but is not a cross-device transaction.

### Members

#### `DriverWriteBatchItem`1`

```csharp
public DriverWriteBatchItem<TDescriptor>(int SourceOffset, TDescriptor Descriptor)
```

One heterogeneous write inside an arbitrary batch operation. SourceOffset points at the first DriverValue for the descriptor; arrays consume their protocol descriptor's element count. Batch execution is ordered but is not a cross-device transaction.

#### `Descriptor`

```csharp
public TDescriptor Descriptor { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets descriptor.

#### `SourceOffset`

```csharp
public int SourceOffset { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets source Offset.

## IIndustrialDriver<TDescriptor>

```csharp
public interface IIndustrialDriver<TDescriptor>
```

Reflection-free industrial driver contract suitable for static registration and Native AOT. Scalar, typed-array and heterogeneous batch operations are part of the common contract; protocol implementations may additionally expose raw block and compiled-plan APIs.

### Members

#### `ConnectAsync`

```csharp
public ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Opens and initializes the protocol connection asynchronously.

#### `DisconnectAsync`

```csharp
public ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Closes the protocol connection gracefully and releases connection state.

#### `ReadBatchAsync`

```csharp
public ValueTask<DriverResult<int>> ReadBatchAsync(ReadOnlyMemory<DriverReadBatchItem<TDescriptor>> items, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads batch asynchronously.

#### `ReadValueAsync`

```csharp
public ValueTask<DriverResult<DriverValue>> ReadValueAsync(TDescriptor descriptor, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads value asynchronously.

#### `ReadValuesAsync`

```csharp
public ValueTask<DriverResult<int>> ReadValuesAsync(TDescriptor descriptor, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads values asynchronously.

#### `WriteBatchAsync`

```csharp
public ValueTask<DriverResult<int>> WriteBatchAsync(ReadOnlyMemory<DriverWriteBatchItem<TDescriptor>> items, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes batch asynchronously.

#### `WriteValueAsync`

```csharp
public ValueTask<DriverResult> WriteValueAsync(TDescriptor descriptor, DriverValue value, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes value asynchronously.

#### `WriteValuesAsync`

```csharp
public ValueTask<DriverResult> WriteValuesAsync(TDescriptor descriptor, ReadOnlyMemory<DriverValue> values, CancellationToken cancellationToken = default)
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

## IndustrialDataType

```csharp
public enum IndustrialDataType
```

Protocol-neutral semantic value kinds. Existing ordinal values are append-only so persisted configurations can safely carry the enum across library upgrades. Protocol spellings such as IEC INT, Siemens WORD, Allen-Bradley SINT, or OPC UA UInt16 belong to protocol-specific type compilers and metadata, not this enum.

### Members

#### `Bool`

```csharp
Bool = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bool option.

#### `Int8`

```csharp
Int8 = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the int8 option.

#### `UInt8`

```csharp
UInt8 = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uInt8 option.

#### `Int16`

```csharp
Int16 = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the int16 option.

#### `UInt16`

```csharp
UInt16 = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uInt16 option.

#### `Int32`

```csharp
Int32 = 5
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the int32 option.

#### `UInt32`

```csharp
UInt32 = 6
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uInt32 option.

#### `Int64`

```csharp
Int64 = 7
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the int64 option.

#### `UInt64`

```csharp
UInt64 = 8
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uInt64 option.

#### `Float32`

```csharp
Float32 = 9
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the float32 option.

#### `Float64`

```csharp
Float64 = 10
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the float64 option.

#### `String`

```csharp
String = 11
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the string option.

#### `Bytes`

```csharp
Bytes = 12
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bytes option.

#### `DateTime`

```csharp
DateTime = 13
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the date Time option.

#### `Duration`

```csharp
Duration = 14
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the duration option.

#### `Int128`

```csharp
Int128 = 15
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the int128 option.

#### `UInt128`

```csharp
UInt128 = 16
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the uInt128 option.

#### `Float16`

```csharp
Float16 = 17
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the float16 option.

#### `Decimal`

```csharp
Decimal = 18
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the decimal option.

#### `Char8`

```csharp
Char8 = 19
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the char8 option.

#### `Char16`

```csharp
Char16 = 20
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the char16 option.

#### `Guid`

```csharp
Guid = 21
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the guid option.

#### `Date`

```csharp
Date = 22
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the date option.

#### `TimeOfDay`

```csharp
TimeOfDay = 23
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the time Of Day option.

#### `BitString8`

```csharp
BitString8 = 24
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bit String8 option.

#### `BitString16`

```csharp
BitString16 = 25
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bit String16 option.

#### `BitString32`

```csharp
BitString32 = 26
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bit String32 option.

#### `BitString64`

```csharp
BitString64 = 27
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the bit String64 option.

#### `Enumeration`

```csharp
Enumeration = 28
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the enumeration option.

#### `StatusCode`

```csharp
StatusCode = 29
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the status Code option.

#### `NodeId`

```csharp
NodeId = 30
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the node Id option.

#### `ExpandedNodeId`

```csharp
ExpandedNodeId = 31
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the expanded Node Id option.

#### `QualifiedName`

```csharp
QualifiedName = 32
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the qualified Name option.

#### `LocalizedText`

```csharp
LocalizedText = 33
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the localized Text option.

#### `Null`

```csharp
Null = 34
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the null option.

#### `Array`

```csharp
Array = 35
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the array option.

#### `Record`

```csharp
Record = 36
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the record option.

#### `Variant`

```csharp
Variant = 37
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the variant option.

#### `ExtensionObject`

```csharp
ExtensionObject = 38
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the extension Object option.

#### `XmlElement`

```csharp
XmlElement = 39
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the xml Element option.

## IndustrialDataTypeInfo

```csharp
public static class IndustrialDataTypeInfo
```

Central semantic classification for canonical industrial value types.

### Members

#### `GetFixedWidthBytes`

```csharp
public static int GetFixedWidthBytes(IndustrialDataType type)
```

Returns the canonical scalar payload width for fixed-width values. String and Bytes return zero because their wire width must always be supplied by protocol metadata. Bool returns one byte as an in-memory scalar; protocols may encode it as a bit.

#### `IsBitString`

```csharp
public static bool IsBitString(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Bit String.

#### `IsEnumeration`

```csharp
public static bool IsEnumeration(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Enumeration.

#### `IsEnumerationStorageType`

```csharp
public static bool IsEnumerationStorageType(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Enumeration Storage Type.

#### `IsFloatingPoint`

```csharp
public static bool IsFloatingPoint(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Floating Point.

#### `IsInteger`

```csharp
public static bool IsInteger(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Integer.

#### `IsNumeric`

```csharp
public static bool IsNumeric(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Numeric.

#### `IsSignedInteger`

```csharp
public static bool IsSignedInteger(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Signed Integer.

#### `IsTemporal`

```csharp
public static bool IsTemporal(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Temporal.

#### `IsTextual`

```csharp
public static bool IsTextual(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Textual.

#### `IsUnsignedEnumerationStorageType`

```csharp
public static bool IsUnsignedEnumerationStorageType(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Unsigned Enumeration Storage Type.

#### `IsUnsignedInteger`

```csharp
public static bool IsUnsignedInteger(IndustrialDataType type)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs is Unsigned Integer.

## IndustrialDriverRecordExtensions

```csharp
public static class IndustrialDriverRecordExtensions
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Driver Record Extensions in the public driver contract.

### Members

#### `ReadRecordAsync`

```csharp
public static ValueTask<DriverResult<int>> ReadRecordAsync<TDescriptor>(IIndustrialDriver<TDescriptor> driver, DriverRecordSchema<TDescriptor> schema, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Reads record asynchronously.

#### `WriteRecordAsync`

```csharp
public static ValueTask<DriverResult<int>> WriteRecordAsync<TDescriptor>(IIndustrialDriver<TDescriptor> driver, DriverRecordSchema<TDescriptor> schema, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

*Undocumented: no XML summary; this text is generated from the name.* Writes record asynchronously.

## IndustrialDriverVariantExtensions

```csharp
public static class IndustrialDriverVariantExtensions
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Driver Variant Extensions in the public driver contract.

### Members

#### `ReadVariantAsync`

```csharp
public static ValueTask<DriverResult<DriverVariantSelection>> ReadVariantAsync<TDescriptor>(IIndustrialDriver<TDescriptor> driver, TDescriptor discriminatorDescriptor, DriverVariantSchema<TDescriptor> schema, Memory<DriverValue> destination, CancellationToken cancellationToken = default)
```

Reads the discriminator first and then only the active union payload. The destination buffer is sized for schema.MaximumValueCount, not for every alternative combined.

#### `WriteVariantAsync`

```csharp
public static ValueTask<DriverResult<int>> WriteVariantAsync<TDescriptor>(IIndustrialDriver<TDescriptor> driver, TDescriptor discriminatorDescriptor, DriverVariantSchema<TDescriptor> schema, DriverValue discriminator, ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
```

Writes the active payload first and commits the discriminator last, avoiding a transient state where readers observe a new union tag with an old payload.

## IndustrialExpandedNodeId

```csharp
public sealed class IndustrialExpandedNodeId
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Expanded Node Id in the public driver contract.

### Members

#### `IndustrialExpandedNodeId`

```csharp
public IndustrialExpandedNodeId(IndustrialNodeId NodeId, string NamespaceUri = null, uint ServerIndex = 0)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `IndustrialExpandedNodeId` instance from the supplied values.

#### `NamespaceUri`

```csharp
public string NamespaceUri { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets namespace Uri.

#### `NodeId`

```csharp
public IndustrialNodeId NodeId { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets node Id.

#### `ServerIndex`

```csharp
public uint ServerIndex { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets server Index.

## IndustrialLocalizedText

```csharp
public struct IndustrialLocalizedText
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Localized Text in the public driver contract.

### Members

#### `IndustrialLocalizedText`

```csharp
public IndustrialLocalizedText(string Locale, string Text)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `IndustrialLocalizedText` instance from the supplied values.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `Locale`

```csharp
public string Locale { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets locale.

#### `Text`

```csharp
public string Text { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets text.

## IndustrialNodeId

```csharp
public sealed class IndustrialNodeId
```

Protocol-neutral immutable representation of an OPC UA NodeId.

### Members

#### `Equals`

```csharp
public bool Equals(IndustrialNodeId other)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `Equals`

```csharp
public bool Equals(object obj)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs equals.

#### `GetHashCode`

```csharp
public int GetHashCode()
```

*Undocumented: no XML summary; this text is generated from the name.* Gets hash Code.

#### `Guid`

```csharp
public static IndustrialNodeId Guid(ushort namespaceIndex, Guid identifier)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs guid.

#### `Numeric`

```csharp
public static IndustrialNodeId Numeric(ushort namespaceIndex, ulong identifier)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs numeric.

#### `Opaque`

```csharp
public static IndustrialNodeId Opaque(ushort namespaceIndex, ReadOnlySpan<byte> identifier)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs opaque.

#### `String`

```csharp
public static IndustrialNodeId String(ushort namespaceIndex, string identifier)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs string.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `GuidIdentifier`

```csharp
public Guid GuidIdentifier { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets guid Identifier.

#### `Kind`

```csharp
public IndustrialNodeIdKind Kind { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets kind.

#### `NamespaceIndex`

```csharp
public ushort NamespaceIndex { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets namespace Index.

#### `NumericIdentifier`

```csharp
public ulong NumericIdentifier { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets numeric Identifier.

#### `OpaqueIdentifier`

```csharp
public ReadOnlyMemory<byte> OpaqueIdentifier { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets opaque Identifier.

#### `StringIdentifier`

```csharp
public string StringIdentifier { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets string Identifier.

## IndustrialNodeIdKind

```csharp
public enum IndustrialNodeIdKind
```

*Undocumented: no XML summary; this text is generated from the name.* Defines the supported industrial Node Id Kind choices. Select only values documented by the target protocol or device profile.

### Members

#### `Numeric`

```csharp
Numeric = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the numeric option.

#### `String`

```csharp
String = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the string option.

#### `Guid`

```csharp
Guid = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the guid option.

#### `Opaque`

```csharp
Opaque = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the opaque option.

## IndustrialQualifiedName

```csharp
public struct IndustrialQualifiedName
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Qualified Name in the public driver contract.

### Members

#### `IndustrialQualifiedName`

```csharp
public IndustrialQualifiedName(ushort NamespaceIndex, string Name)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates a `IndustrialQualifiedName` instance from the supplied values.

#### `ToString`

```csharp
public string ToString()
```

*Undocumented: no XML summary; this text is generated from the name.* Returns the canonical text representation of this value.

#### `Name`

```csharp
public string Name { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets name.

#### `NamespaceIndex`

```csharp
public ushort NamespaceIndex { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets namespace Index.

## IndustrialTypeDialect

```csharp
public enum IndustrialTypeDialect
```

Source-language/protocol naming dialect. A type token is never interpreted without its dialect because names such as INT, BYTE and WORD do not carry the same semantics in .NET, IEC 61131-3, CIP and OPC UA.

### Members

#### `DotNet`

```csharp
DotNet = 0
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the dot Net option.

#### `Iec61131`

```csharp
Iec61131 = 1
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the iec61131 option.

#### `Siemens`

```csharp
Siemens = 2
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the siemens option.

#### `AllenBradleyCip`

```csharp
AllenBradleyCip = 3
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the allen Bradley Cip option.

#### `OpcUa`

```csharp
OpcUa = 4
```

*Undocumented: no XML summary; this text is generated from the name.* Represents the opc Ua option.

## IndustrialTypeResolution

```csharp
public struct IndustrialTypeResolution
```

Canonical semantic result while retaining the original native spelling.

### Members

#### `IndustrialTypeResolution`

```csharp
public IndustrialTypeResolution(IndustrialDataType DataType, IndustrialTypeDialect Dialect, string NativeType)
```

Canonical semantic result while retaining the original native spelling.

#### `DataType`

```csharp
public IndustrialDataType DataType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets data Type.

#### `Dialect`

```csharp
public IndustrialTypeDialect Dialect { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets dialect.

#### `NativeType`

```csharp
public string NativeType { get; init;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets or sets native Type.

## IndustrialTypeResolver

```csharp
public static class IndustrialTypeResolver
```

*Undocumented: no XML summary; this text is generated from the name.* Represents industrial Type Resolver in the public driver contract.

### Members

#### `Resolve`

```csharp
public static DriverResult<IndustrialTypeResolution> Resolve(string typeName, IndustrialTypeDialect dialect)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs resolve.

