using Proxus.Drivers;

var failures = 0;
var total = 0;

void Check(bool condition, string name)
{
    total++;
    if (!condition)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {name}");
    }
    else
    {
        Console.WriteLine($"PASS  {name}");
    }
}

IndustrialDataType R(string name, IndustrialTypeDialect dialect)
{
    var result = IndustrialTypeResolver.Resolve(name, dialect);
    if (!result.IsSuccess) throw new InvalidOperationException(result.Error.ToUserMessage());
    return result.Value.DataType;
}

Check(R("int", IndustrialTypeDialect.DotNet) == IndustrialDataType.Int32, ".NET int is Int32");
Check(R("uint", IndustrialTypeDialect.DotNet) == IndustrialDataType.UInt32, ".NET uint is UInt32");
Check(R("byte", IndustrialTypeDialect.DotNet) == IndustrialDataType.UInt8, ".NET byte is UInt8");
Check(R("byte[]", IndustrialTypeDialect.DotNet) == IndustrialDataType.Bytes, ".NET byte[] is Bytes");
Check(R("INT", IndustrialTypeDialect.Iec61131) == IndustrialDataType.Int16, "IEC INT is Int16");
Check(R("UINT", IndustrialTypeDialect.Iec61131) == IndustrialDataType.UInt16, "IEC UINT is UInt16");
Check(R("BYTE", IndustrialTypeDialect.Iec61131) == IndustrialDataType.BitString8, "IEC BYTE is bit-string8");
Check(R("WORD", IndustrialTypeDialect.Iec61131) == IndustrialDataType.BitString16, "IEC WORD is bit-string16");
Check(R("DWORD", IndustrialTypeDialect.Iec61131) == IndustrialDataType.BitString32, "IEC DWORD is bit-string32");
Check(R("LWORD", IndustrialTypeDialect.Iec61131) == IndustrialDataType.BitString64, "IEC LWORD is bit-string64");
Check(R("DTL", IndustrialTypeDialect.Siemens) == IndustrialDataType.DateTime, "Siemens DTL is DateTime semantic");
Check(R("SINT", IndustrialTypeDialect.AllenBradleyCip) == IndustrialDataType.Int8, "CIP SINT is Int8");
Check(R("Byte", IndustrialTypeDialect.OpcUa) == IndustrialDataType.UInt8, "OPC UA Byte is UInt8");
Check(R("ByteString", IndustrialTypeDialect.OpcUa) == IndustrialDataType.Bytes, "OPC UA ByteString is Bytes");
Check(R("Guid", IndustrialTypeDialect.OpcUa) == IndustrialDataType.Guid, "OPC UA Guid is Guid");
Check(R("INT", IndustrialTypeDialect.DotNet) == IndustrialDataType.Int32,
    ".NET dialect stays Int32 regardless token casing");
Check(R("Enum", IndustrialTypeDialect.DotNet) == IndustrialDataType.Enumeration, ".NET Enum is Enumeration semantic");
Check(R("ENUM", IndustrialTypeDialect.Iec61131) == IndustrialDataType.Enumeration, "IEC ENUM is Enumeration semantic");
Check(R("LDATE_AND_TIME", IndustrialTypeDialect.Siemens) == IndustrialDataType.DateTime,
    "Siemens LDATE_AND_TIME is DateTime semantic");
Check(R("Enumeration", IndustrialTypeDialect.OpcUa) == IndustrialDataType.Enumeration,
    "OPC UA Enumeration preserves enumeration semantic");
Check(R("StatusCode", IndustrialTypeDialect.OpcUa) == IndustrialDataType.StatusCode,
    "OPC UA StatusCode preserves status semantic");
Check(R("NodeId", IndustrialTypeDialect.OpcUa) == IndustrialDataType.NodeId,
    "OPC UA NodeId preserves structured identity");
Check(R("ExpandedNodeId", IndustrialTypeDialect.OpcUa) == IndustrialDataType.ExpandedNodeId,
    "OPC UA ExpandedNodeId preserves structured identity");
Check(R("QualifiedName", IndustrialTypeDialect.OpcUa) == IndustrialDataType.QualifiedName,
    "OPC UA QualifiedName is not flattened to String");
Check(R("LocalizedText", IndustrialTypeDialect.OpcUa) == IndustrialDataType.LocalizedText,
    "OPC UA LocalizedText is not flattened to String");

var guid = new Guid("00112233-4455-6677-8899-aabbccddeeff");
var values = new[]
{
    DriverValue.FromInt128(Int128.MinValue + 123),
    DriverValue.FromUInt128(UInt128.MaxValue - 123),
    DriverValue.FromFloat16((Half)1.5f),
    DriverValue.FromDecimal(1234567890.123456789m),
    DriverValue.FromChar8('A'),
    DriverValue.FromChar16('Ω'),
    DriverValue.FromGuid(guid),
    DriverValue.FromDate(new DateOnly(2026, 8, 10)),
    DriverValue.FromTimeOfDay(new TimeOnly(17, 1, 2, 345)),
    DriverValue.FromBitString8(0xA5),
    DriverValue.FromBitString16(0xA55A),
    DriverValue.FromBitString32(0xA55A_F00D),
    DriverValue.FromBitString64(0xA55A_F00D_DEAD_BEEFUL),
    DriverValue.FromEnumeration(-42),
    DriverValue.FromUnsignedEnumeration(ulong.MaxValue)
};
Check(values.Distinct().Count() == values.Length, "Expanded DriverValue equality/hash");
Check(DriverValue.FromGuid(guid).AsGuid() == guid, "Guid round-trip");
Check(DriverValue.FromDecimal(1234567890.123456789m).AsDecimal() == 1234567890.123456789m, "Decimal exact round-trip");
var nodeId = IndustrialNodeId.String(2, "Motor/Speed");
var expandedNodeId = new IndustrialExpandedNodeId(nodeId, "urn:proxus:test", 3);
var qualifiedName = new IndustrialQualifiedName(2, "Speed");
var localizedText = new IndustrialLocalizedText("tr-TR", "Hız");
Check(DriverValue.FromStatusCode(0x80AB0000).AsStatusCode() == 0x80AB0000, "OPC UA StatusCode exact round-trip");
Check(DriverValue.FromNodeId(nodeId).AsNodeId().Equals(nodeId), "OPC UA NodeId exact round-trip");
Check(DriverValue.FromExpandedNodeId(expandedNodeId).AsExpandedNodeId() == expandedNodeId,
    "OPC UA ExpandedNodeId exact round-trip");
Check(DriverValue.FromQualifiedName(qualifiedName).AsQualifiedName() == qualifiedName,
    "OPC UA QualifiedName exact round-trip");
Check(DriverValue.FromLocalizedText(localizedText).AsLocalizedText() == localizedText,
    "OPC UA LocalizedText exact round-trip");
Check(
    DriverValue.FromNodeId(IndustrialNodeId.Numeric(1, 42)) != DriverValue.FromNodeId(IndustrialNodeId.Numeric(1, 43)),
    "OPC UA NodeId equality preserves identifier");

Check(IndustrialDataTypeInfo.IsNumeric(IndustrialDataType.Int128), "Int128 classified numeric");
Check(IndustrialDataTypeInfo.IsBitString(IndustrialDataType.BitString32), "DWORD semantic classified bit-string");
Check(IndustrialDataTypeInfo.IsTextual(IndustrialDataType.Char16), "Char16 classified textual");
Check(IndustrialDataTypeInfo.GetFixedWidthBytes(IndustrialDataType.Guid) == 16, "Guid canonical width metadata");
Check(IndustrialDataTypeInfo.IsEnumeration(IndustrialDataType.Enumeration),
    "Enumeration classified independently from numeric scalars");
Check(DriverValue.FromEnumeration(-42).AsEnumerationInt64() == -42, "Signed Enumeration exact round-trip");
Check(DriverValue.FromUnsignedEnumeration(ulong.MaxValue).AsEnumerationUInt64() == ulong.MaxValue,
    "Unsigned Enumeration exact round-trip");

var enumSchemaResult = DriverEnumerationSchema.Create("MachineState", [
    new DriverEnumerationMember("Stopped", DriverValue.FromEnumeration(0)),
    new DriverEnumerationMember("Running", DriverValue.FromEnumeration(1)),
    new DriverEnumerationMember("Fault", DriverValue.FromEnumeration(10))
]);
Check(enumSchemaResult.IsSuccess, "Enumeration symbolic schema compiles");
var enumSchema = enumSchemaResult.Value;
Check(enumSchema.Name == "MachineState" && enumSchema.Members.Length == 3,
    "Enumeration schema exposes stable metadata");
Check(enumSchema.TryGetName(DriverValue.FromEnumeration(10), out var faultName) && faultName == "Fault",
    "Enumeration value resolves to symbolic name");
Check(enumSchema.TryGetValue("Running", out var runningValue) && runningValue.AsEnumerationInt64() == 1,
    "Enumeration symbolic name resolves to value");
Check(!DriverEnumerationSchema.Create("Bad", [
    new DriverEnumerationMember("A", DriverValue.FromEnumeration(1)),
    new DriverEnumerationMember("B", DriverValue.FromEnumeration(1))
]).IsSuccess, "Enumeration duplicate values rejected");

// Schema-driven STRUCT/UDT: a scalar field plus a three-element array occupies four flat values.
var schemaResult = DriverRecordSchema<int>.Create([
    new DriverRecordField<int>("Status", 0, 1, 10),
    new DriverRecordField<int>("Temperatures", 1, 3, 20)
]);
Check(schemaResult.IsSuccess, "STRUCT/UDT schema compiles");
var schema = schemaResult.Value;
Check(
    schema.ValueCount == 4 && schema.Fields.Length == 2 && schema.TryGetField("Temperatures", out var tf) &&
    tf.ValueCount == 3,
    "STRUCT/UDT field lookup and shape");
Check(!DriverRecordSchema<int>.Create([
    new DriverRecordField<int>("A", 0, 2, 1),
    new DriverRecordField<int>("B", 1, 1, 2)
]).IsSuccess, "STRUCT/UDT overlapping value ranges rejected");

await using (var fake = new SchemaFakeDriver())
{
    var recordValues = new DriverValue[4];
    var read = await fake.ReadRecordAsync(schema, recordValues);
    Check(read.IsSuccess && read.Value == 4 && recordValues[0].AsInt32() == 10 && recordValues[3].AsInt32() == 22,
        "STRUCT/UDT consumer read uses arbitrary batch contract");
    var write = await fake.WriteRecordAsync(schema, recordValues);
    Check(write.IsSuccess && write.Value == 4 && fake.LastWrittenCount == 4,
        "STRUCT/UDT consumer write uses arbitrary batch contract");
}


var nestedBuilder = new DriverRecordSchemaBuilder<int>()
    .AddField("MachineState", 1)
    .AddRecord("Motor", schema, static descriptor => descriptor + 100)
    .AddRecordArray("Axes", 2, schema, static (index, descriptor) => descriptor + (index + 1) * 1000);
var nestedResult = nestedBuilder.Build();
Check(nestedResult.IsSuccess, "Nested STRUCT/UDT builder compiles");
var nested = nestedResult.Value;
Check(nested.ValueCount == 13 && nested.Fields.Length == 7,
    "Nested STRUCT/UDT value shape is flattened once");
Check(
    nested.TryGetField("Motor.Temperatures", out var motorTemps) && motorTemps.Descriptor == 120 &&
    motorTemps.ValueOffset == 2,
    "Nested STRUCT/UDT descriptor projection");
Check(
    nested.TryGetField("Axes[1].Temperatures", out var axisTemps) && axisTemps.Descriptor == 2020 &&
    axisTemps.ValueOffset == 10,
    "STRUCT/UDT array descriptor projection and stable path");
Check(
    !new DriverRecordSchemaBuilder<int>().AddRecordArray("Bad", 0, schema, static (_, descriptor) => descriptor).Build()
        .IsSuccess,
    "STRUCT/UDT array invalid count is structured");


// Rank-N array shape: row-major indexing is deterministic and reversible.
var shapeResult = DriverArrayShape.Create([2, 3, 4]);
Check(shapeResult.IsSuccess, "Rank-N array shape compiles");
var shape = shapeResult.Value;
Check(shape.Rank == 3 && shape.ElementCount == 24 && shape.Dimensions.Span.SequenceEqual([2, 3, 4]),
    "Rank-N array metadata is stable");
Check(shape.TryGetLinearIndex([1, 2, 3], out var linear) && linear == 23,
    "Rank-N array coordinates flatten row-major");
Span<int> recovered = stackalloc int[3];
Check(shape.TryGetIndices(23, recovered) && recovered.SequenceEqual([1, 2, 3]),
    "Rank-N array coordinates round-trip");
Check(shape.FormatIndices(23) == "[1,2,3]", "Rank-N array path formatting");
Check(!shape.TryGetLinearIndex([2, 0, 0], out _), "Rank-N out-of-range coordinates rejected");

var matrixShape = DriverArrayShape.Create([2, 3]).Value;
var arraySchemaResult = new DriverRecordSchemaBuilder<int>()
    .AddField("Mode", 5)
    .AddArrayField("Matrix", 20, matrixShape)
    .AddRecordArray("Cells", matrixShape, schema,
        static (linearIndex, descriptor) => descriptor + 1000 + linearIndex * 100)
    .Build();
Check(arraySchemaResult.IsSuccess, "Multidimensional STRUCT/UDT arrays compile");
var arraySchema = arraySchemaResult.Value;
var hasMatrixMeta = arraySchema.TryGetArray("Matrix", out var matrixMeta);
Check(arraySchema.Arrays.Length == 2 && hasMatrixMeta && matrixMeta.ValueCount == 6,
    "Scalar array shape metadata retained in record schema");
Check(
    hasMatrixMeta && matrixMeta.TryGetElementValueOffset([1, 2], out var matrixOffset) &&
    matrixOffset == 6,
    "Scalar multidimensional array resolves logical value offset");
var hasCellsMeta = arraySchema.TryGetArray("Cells", out var cellsMeta);
Check(hasCellsMeta && cellsMeta.ElementValueCount == 4 && cellsMeta.Shape.Rank == 2,
    "Record-array multidimensional shape retained");
Check(
    hasCellsMeta && cellsMeta.TryGetElementValueOffset([1, 2], out var cellOffset) &&
    cellOffset == 27,
    "Record-array multidimensional coordinates resolve record value offset");
Check(arraySchema.TryGetField("Cells[1,2].Temperatures", out var multidimField) && multidimField.Descriptor == 1520,
    "Nested multidimensional record-array descriptor projection");

// Explicit padding/alignment plus reusable packed canonical codec.
var packedBuilder = new DriverPackedLayoutBuilder()
    .AddField("State", IndustrialDataType.UInt8)
    .Align(4)
    .AddField("Counter", IndustrialDataType.UInt32)
    .Pad(2)
    .AddArrayField("Temperatures", IndustrialDataType.Int16, matrixShape, DriverByteOrder.LittleEndian);
Check(packedBuilder.CurrentByteOffset == 22 && packedBuilder.CurrentValueOffset == 8,
    "Packed layout builder tracks physical/logical cursors");
var packedResult = packedBuilder.Build();
Check(packedResult.IsSuccess, "Explicit padding/alignment packed layout compiles");
var packed = packedResult.Value;
Check(packed.ByteLength == 22 && packed.ValueCount == 8 && packed.PaddingByteCount == 5 && packed.Fields.Length == 3,
    "Packed layout reports byte/value/padding metrics");
Check(
    packed.Fields.Span[1].ByteOffset == 4 && packed.Fields.Span[2].ByteOffset == 10 &&
    packed.Fields.Span[2].Shape == matrixShape,
    "Packed layout preserves explicit offsets and array shape");

var packedValues = new[]
{
    DriverValue.FromUInt8(7), DriverValue.FromUInt32(0x11223344),
    DriverValue.FromInt16(-1), DriverValue.FromInt16(2), DriverValue.FromInt16(-3),
    DriverValue.FromInt16(4), DriverValue.FromInt16(-5), DriverValue.FromInt16(6)
};
var packedBytes = new byte[packed.ByteLength];
var packedEncode = DriverPackedLayoutCodec.Encode(packed, packedValues, packedBytes);
Check(
    packedEncode.IsSuccess && packedEncode.Value == 22 && packedBytes[0] == 7 && packedBytes[1] == 0 &&
    packedBytes[4] == 0x11 && packedBytes[7] == 0x44,
    "Packed layout codec honors padding and mixed byte order");
var unpacked = new DriverValue[packed.ValueCount];
var packedDecode = DriverPackedLayoutCodec.Decode(packed, packedBytes, unpacked);
Check(
    packedDecode.IsSuccess && packedDecode.Value == packed.ValueCount && packedValues.AsSpan().SequenceEqual(unpacked),
    "Packed layout codec round-trips heterogeneous values");
var projected = packed.Project(static field => field.ByteOffset);
Check(projected.IsSuccess && projected.Value.Fields.Length == 3 && projected.Value.Fields.Span[2].Descriptor == 10,
    "Packed layout projects to protocol descriptor schema without reflection");
Check(!new DriverPackedLayoutBuilder().Align(3).Build().IsSuccess,
    "Non-power-of-two packed alignment rejected");

// Discriminated union / variant metadata.
var runSchema = DriverRecordSchema<int>.Create([new DriverRecordField<int>("Speed", 0, 1, 1)]).Value;
var faultSchema = DriverRecordSchema<int>.Create([
    new DriverRecordField<int>("Code", 0, 1, 2),
    new DriverRecordField<int>("Detail", 1, 1, 3)
]).Value;
var variantResult = DriverVariantSchema<int>.Create("MachinePayload", [
    new DriverVariantCase<int>("Running", DriverValue.FromUInt8(1), runSchema),
    new DriverVariantCase<int>("Fault", DriverValue.FromUInt8(2), faultSchema)
]);
Check(variantResult.IsSuccess, "Discriminated union schema compiles");
var variant = variantResult.Value;
Check(
    variant.Name == "MachinePayload" && variant.DiscriminatorType == IndustrialDataType.UInt8 &&
    variant.Cases.Length == 2 && variant.MaximumValueCount == 2,
    "Discriminated union exposes stable metadata");
Check(
    variant.TryResolve(DriverValue.FromUInt8(2), out var resolvedCase) && resolvedCase.Name == "Fault" &&
    resolvedCase.Schema.ValueCount == 2,
    "Discriminated union resolves active case");
Check(!variant.TryResolve(DriverValue.FromUInt8(99), out _), "Unknown union discriminator is explicit");
Check(!DriverVariantSchema<int>.Create("BadVariant", [
    new DriverVariantCase<int>("A", DriverValue.FromUInt8(1), runSchema),
    new DriverVariantCase<int>("B", DriverValue.FromUInt8(1), faultSchema)
]).IsSuccess, "Duplicate union discriminators rejected");


// Runtime union execution reads only the active case and commits the discriminator after payload writes.
var runtimeVariant = DriverVariantSchema<int>.Create("RuntimePayload", [
    new DriverVariantCase<int>("Small", DriverValue.FromInt32(1), runSchema),
    new DriverVariantCase<int>("Expanded", DriverValue.FromInt32(2), schema)
]);
Check(runtimeVariant.IsSuccess && runtimeVariant.Value.MaximumValueCount == 4, "Runtime union schema compiles");
await using (var variantDriver = new SchemaFakeDriver())
{
    var variantValues = new DriverValue[4];
    var selected = await variantDriver.ReadVariantAsync(2, runtimeVariant.Value, variantValues);
    Check(selected.IsSuccess && selected.Value.CaseName == "Expanded" && selected.Value.Discriminator.AsInt32() == 2 &&
          selected.Value.ValueCount == 4 && variantValues[3].AsInt32() == 22,
        "Runtime union reads discriminator then active payload only");
    var variantWrite =
        await variantDriver.WriteVariantAsync(99, runtimeVariant.Value, DriverValue.FromInt32(2), variantValues);
    Check(variantWrite.IsSuccess && variantWrite.Value == 4 && variantDriver.LastWrittenCount == 4 &&
          variantDriver.ScalarWriteCount == 1 && variantDriver.LastScalarWrittenValue.AsInt32() == 2,
        "Runtime union writes payload then commits discriminator");
    var unknownVariantWrite =
        await variantDriver.WriteVariantAsync(99, runtimeVariant.Value, DriverValue.FromInt32(9), variantValues);
    Check(!unknownVariantWrite.IsSuccess && unknownVariantWrite.Error.Code == "VARIANT_UNKNOWN_DISCRIMINATOR",
        "Runtime union rejects undeclared discriminator before I/O");
}

// Dynamic industrial values preserve runtime shape, optional presence and raw extension bodies.
Check(R("Variant", IndustrialTypeDialect.OpcUa) == IndustrialDataType.Variant,
    "OPC UA Variant preserves dynamic semantic");
Check(R("ExtensionObject", IndustrialTypeDialect.OpcUa) == IndustrialDataType.ExtensionObject,
    "OPC UA ExtensionObject preserves envelope semantic");
Check(DriverValue.Null.IsNull && DriverValue.Null.ToInvariantString() == "null", "Null is explicit canonical value");
var runtimeSource = new[]
{
    DriverValue.FromInt32(10), DriverValue.FromInt32(20), DriverValue.FromInt32(30), DriverValue.FromInt32(40),
    DriverValue.FromInt32(50), DriverValue.FromInt32(60)
};
var runtimeArrayResult = DriverDynamicArray.Create(IndustrialDataType.Int32, [2, 3], runtimeSource);
Check(runtimeArrayResult.IsSuccess && runtimeArrayResult.Value.Shape.Rank == 2 && runtimeArrayResult.Value.Count == 6,
    "Runtime-sized rank-N array compiles");
runtimeSource[0] = DriverValue.FromInt32(999);
Check(runtimeArrayResult.Value.Values.Span[0].AsInt32() == 10, "Runtime array owns immutable value snapshot");
var runtimeArrayTwin =
    DriverDynamicArray.Create(IndustrialDataType.Int32, [2, 3], runtimeArrayResult.Value.Values.Span);
Check(runtimeArrayTwin.IsSuccess && runtimeArrayResult.Value.Equals(runtimeArrayTwin.Value),
    "Runtime array deep equality preserves shape and values");
Check(runtimeArrayTwin.IsSuccess && runtimeArrayResult.Value.GetHashCode() == runtimeArrayTwin.Value.GetHashCode(),
    "Runtime array equality/hash contract");
Check(
    !DriverDynamicArray.Create(IndustrialDataType.Int32, [2], [DriverValue.FromInt32(1), DriverValue.FromFloat32(2)])
        .IsSuccess, "Runtime array rejects mixed element semantics");
var recordResult = DriverRecordValue.Create("OpcPayload", [
    DriverRecordFieldValue.Present("Required", DriverValue.FromInt32(7)),
    DriverRecordFieldValue.Absent("Optional"),
    DriverRecordFieldValue.Present("Nullable", DriverValue.Null),
    DriverRecordFieldValue.Present("Samples", DriverValue.FromArray(runtimeArrayResult.Value))
]);
Check(recordResult.IsSuccess && recordResult.Value.Fields.Length == 4, "Runtime structured value compiles");
Check(recordResult.Value.TryGetField("Optional", out var optionalField) && !optionalField.IsPresent,
    "Optional field absence preserved separately");
Check(
    recordResult.Value.TryGetField("Nullable", out var nullableField) && nullableField.IsPresent &&
    nullableField.Value.IsNull, "Present-but-Null differs from absent");
Check(recordResult.Value.TryGetField("Samples", out var samplesField) && samplesField.Value.AsArray().Shape.Rank == 2,
    "Runtime record carries dynamic arrays losslessly");
Check(
    !DriverRecordValue.Create("Bad",
    [
        DriverRecordFieldValue.Present("A", DriverValue.FromInt32(1)),
        DriverRecordFieldValue.Present("A", DriverValue.FromInt32(2))
    ]).IsSuccess, "Runtime record rejects duplicate fields");
var variantValue = DriverValue.FromVariant(DriverValue.FromRecord(recordResult.Value));
Check(variantValue.Type == IndustrialDataType.Variant && variantValue.AsVariant().AsRecord().Equals(recordResult.Value),
    "Variant round-trips nested canonical record");
var extType = new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(2, 5001), "urn:vendor:model");
var rawExtensionBody = new byte[] { 0x01, 0x02, 0xFE, 0xFF };
var extension =
    new DriverExtensionObject(extType, DriverExtensionObjectEncoding.Binary, rawExtensionBody, variantValue);
rawExtensionBody[0] = 0x99;
var extensionValue = DriverValue.FromExtensionObject(extension);
Check(extensionValue.AsExtensionObject().EncodedBody.Span.SequenceEqual(new byte[] { 0x01, 0x02, 0xFE, 0xFF }),
    "ExtensionObject preserves original encoded body immutably");
Check(
    extensionValue.AsExtensionObject().HasDecodedValue &&
    extensionValue.AsExtensionObject().DecodedValue!.Value.AsVariant().Type == IndustrialDataType.Record,
    "ExtensionObject preserves optional decoded canonical value");
var jsonExtension = new DriverExtensionObject(extType, DriverExtensionObjectEncoding.Json, [.. "{}"u8]);
Check(jsonExtension.Encoding == DriverExtensionObjectEncoding.Json &&
      jsonExtension.EncodedBody.Span.SequenceEqual("{}"u8),
    "ExtensionObject preserves JSON encoding kind and body");
Check(
    extensionValue == DriverValue.FromExtensionObject(new DriverExtensionObject(extType,
        DriverExtensionObjectEncoding.Binary, [1, 2, 0xFE, 0xFF], variantValue)),
    "Dynamic DriverValue deep equality is deterministic");

// Quality/status metadata stays separate from the exact DriverValue payload.
var timestamp = new DateTime(2026, 8, 10, 16, 0, 0, DateTimeKind.Utc);
var goodSample = DriverSample.Good(DriverValue.FromFloat32(42.5f), timestamp, timestamp.AddMilliseconds(2));
Check(goodSample.Quality.IsGood && goodSample.Value.AsFloat32() == 42.5f && goodSample.SourceTimestampUtc == timestamp,
    "Good industrial sample carries exact value and UTC timestamps");
var allFlags = DriverQualityFlags.Stale | DriverQualityFlags.Substituted | DriverQualityFlags.OverRange |
               DriverQualityFlags.UnderRange | DriverQualityFlags.SensorFailure |
               DriverQualityFlags.CommunicationFailure |
               DriverQualityFlags.ConfigurationError;
var uncertain = new DriverQuality(DriverQualityLevel.Uncertain, allFlags, 0x12345678);
var bad = new DriverQuality(DriverQualityLevel.Bad, DriverQualityFlags.CommunicationFailure, 0x80000000);
Check(
    !uncertain.IsGood && uncertain.NativeStatusCode == 0x12345678 && bad.Level == DriverQualityLevel.Bad &&
    uncertain.Flags == allFlags,
    "Quality/status preserves uncertain/bad state, flags and native status code");

Console.WriteLine($"{total - failures}/{total} tests passed.");
return failures == 0 ? 0 : 1;

internal sealed class SchemaFakeDriver : IIndustrialDriver<int>
{
    public int LastWrittenCount { get; private set; }
    public int ScalarWriteCount { get; private set; }
    public DriverValue LastScalarWrittenValue { get; private set; }

    public DriverCapabilities Capabilities => new("SchemaFake",
        DriverFeature.ArbitraryBatchRead | DriverFeature.ArbitraryBatchWrite, 0, 0, 0, 0, false);

    public bool IsConnected => true;

    public ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(DriverResult.Success());
    }

    public ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(DriverResult.Success());
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public ValueTask<DriverResult<DriverValue>> ReadValueAsync(int descriptor,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(DriverResult<DriverValue>.Success(DriverValue.FromInt32(descriptor)));
    }

    public ValueTask<DriverResult<int>> ReadValuesAsync(int descriptor, Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(DriverResult<int>.Success(0));
    }

    public ValueTask<DriverResult> WriteValueAsync(int descriptor, DriverValue value,
        CancellationToken cancellationToken = default)
    {
        ScalarWriteCount++;
        LastScalarWrittenValue = value;
        return ValueTask.FromResult(DriverResult.Success());
    }

    public ValueTask<DriverResult> WriteValuesAsync(int descriptor, ReadOnlyMemory<DriverValue> values,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(DriverResult.Success());
    }

    public ValueTask<DriverResult<int>> ReadBatchAsync(ReadOnlyMemory<DriverReadBatchItem<int>> items,
        Memory<DriverValue> destination, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var item in items.Span)
        {
            var n = item.Descriptor == 20 ? 3 : 1;
            for (var i = 0; i < n; i++)
                destination.Span[item.DestinationOffset + i] = DriverValue.FromInt32(item.Descriptor + i);
            count += n;
        }

        return ValueTask.FromResult(DriverResult<int>.Success(count));
    }

    public ValueTask<DriverResult<int>> WriteBatchAsync(ReadOnlyMemory<DriverWriteBatchItem<int>> items,
        ReadOnlyMemory<DriverValue> source, CancellationToken cancellationToken = default)
    {
        var count = 0;
        foreach (var item in items.Span) count += item.Descriptor == 20 ? 3 : 1;
        LastWrittenCount = count;
        return ValueTask.FromResult(DriverResult<int>.Success(count));
    }
}
