using System.Text;

namespace Proxus.Drivers.OpcUa;

internal static class OpcUaBinaryCodec
{
    private const byte ArrayFlag = 0x80;
    private const byte ArrayDimensionsFlag = 0x40;
    private static readonly DateTime MinUtc = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    public static void WriteVariant(UaBinaryWriter writer, DriverValue value)
    {
        if (value.Type == IndustrialDataType.Null)
        {
            writer.WriteByte(0);
            return;
        }

        if (value.Type == IndustrialDataType.Array)
        {
            var array = value.AsArray();
            var builtIn = GetBuiltInTypeId(array.ElementType);
            var dimensions = array.Shape.Dimensions.Span;
            var includeDimensions = dimensions.Length >= 2;
            writer.WriteByte((byte)(builtIn | ArrayFlag | (includeDimensions ? ArrayDimensionsFlag : 0)));
            writer.WriteInt32(array.Count);
            foreach (var item in array.Values.Span)
                WriteScalarBody(writer, builtIn, item);
            if (includeDimensions)
            {
                writer.WriteInt32(dimensions.Length);
                for (var i = 0; i < dimensions.Length; i++) writer.WriteInt32(dimensions[i]);
            }

            return;
        }

        var typeId = GetBuiltInTypeId(value.Type);
        writer.WriteByte(typeId);
        WriteScalarBody(writer, typeId, value);
    }

    /// <summary>Maximum Variant nesting (arrays of Variant). Bounds recursion on untrusted input.</summary>
    internal const int MaximumVariantDepth = 32;

    /// <summary>
    ///     OPC UA StatusCode Bad_DataEncodingUnsupported, reported for a value whose built-in type (DataValue or
    ///     DiagnosticInfo inside a Variant) has no <see cref="DriverValue" /> representation.
    /// </summary>
    internal const uint BadDataEncodingUnsupported = 0x80390000u;

    public static DriverValue ReadVariant(ref UaBinaryReader reader)
    {
        var unrepresentable = false;
        var value = ReadVariant(ref reader, 0, ref unrepresentable);
        if (unrepresentable)
            throw new NotSupportedException(
                "OPC UA Variant contains a DataValue or DiagnosticInfo, which has no DriverValue representation.");
        return value;
    }

    // OPC 10000-6 5.2.2.16. A well-formed value of a built-in type without a DriverValue representation
    // (23 DataValue, 25 DiagnosticInfo) is consumed structurally and reported through `unrepresentable`, so
    // the caller can keep decoding the rest of the message.
    private static DriverValue ReadVariant(ref UaBinaryReader reader, int depth, ref bool unrepresentable)
    {
        if (depth > MaximumVariantDepth)
            throw new InvalidDataException($"OPC UA Variant nesting exceeds {MaximumVariantDepth} levels.");
        var mask = reader.ReadByte();
        if (mask == 0) return DriverValue.Null;
        var builtIn = (byte)(mask & 0x3F);
        var isArray = (mask & ArrayFlag) != 0;
        var hasDimensions = (mask & ArrayDimensionsFlag) != 0;
        if (!isArray && hasDimensions)
            throw new InvalidDataException("OPC UA Variant has ArrayDimensions without the Array flag.");

        if (!isArray)
            return ReadScalarBody(ref reader, builtIn, depth, ref unrepresentable);

        if (builtIn is 0 or > 31)
            throw new InvalidDataException($"OPC UA Variant built-in type {builtIn} is not defined.");
        var length = reader.ReadArrayLength("OPC UA Variant array", MinimumEncodedSize(builtIn));
        if (length == -1) return DriverValue.Null;
        var elementType = builtIn is 23 or 25 ? IndustrialDataType.Null : GetCanonicalType(builtIn);
        var values = new DriverValue[length];
        for (var i = 0; i < length; i++) values[i] = ReadScalarBody(ref reader, builtIn, depth, ref unrepresentable);

        int[] dimensions;
        if (hasDimensions)
        {
            var rank = reader.ReadInt32();
            if (rank <= 0 || rank > 32) throw new InvalidDataException($"Invalid OPC UA Variant array rank {rank}.");
            dimensions = new int[rank];
            var product = 1L;
            for (var i = 0; i < rank; i++)
            {
                dimensions[i] = reader.ReadInt32();
                if (dimensions[i] < 0)
                    throw new InvalidDataException("OPC UA Variant array dimensions cannot be negative.");
                product *= dimensions[i];
                if (product > length)
                    break;
            }

            if (product != length)
                throw new InvalidDataException(
                    $"OPC UA Variant array dimensions describe {product} elements but payload contains {length}.");
        }
        else
        {
            dimensions = [length];
        }

        if (unrepresentable)
            return DriverValue.Null;
        var result = DriverDynamicArray.Create(elementType, dimensions, values);
        if (!result.IsSuccess) throw new InvalidDataException(result.Error.ToString());
        return DriverValue.FromArray(result.Value);
    }

    public static DriverSample ReadDataValue(ref UaBinaryReader reader)
    {
        return ReadDataValue(ref reader, 0);
    }

    private static DriverSample ReadDataValue(ref UaBinaryReader reader, int depth)
    {
        var mask = reader.ReadByte();
        if ((mask & 0xC0) != 0) throw new InvalidDataException($"Invalid OPC UA DataValue mask 0x{mask:X2}.");
        var unrepresentable = false;
        var value = (mask & 0x01) != 0 ? ReadVariant(ref reader, depth, ref unrepresentable) : DriverValue.Null;
        var status = (mask & 0x02) != 0 ? reader.ReadUInt32() : 0u;
        var sourceTimestamp = (mask & 0x04) != 0 ? reader.ReadDateTime() : MinUtc;
        var sourcePicoseconds = (mask & 0x10) != 0 ? ClampPicoseconds(reader.ReadUInt16()) : (ushort)0;
        var serverTimestamp = (mask & 0x08) != 0 ? reader.ReadDateTime() : MinUtc;
        var serverPicoseconds = (mask & 0x20) != 0 ? ClampPicoseconds(reader.ReadUInt16()) : (ushort)0;
        // A value this client cannot represent fails only its own node: Null with Bad_DataEncodingUnsupported,
        // unless the server already reported a Bad status for it.
        var quality = unrepresentable && (status & 0x80000000u) == 0
            ? new DriverQuality(DriverQualityLevel.Bad, DriverQualityFlags.None, BadDataEncodingUnsupported)
            : MapQuality(status);
        return new DriverSample(value, quality, sourceTimestamp, serverTimestamp, sourcePicoseconds,
            serverPicoseconds);
    }

    /// <summary>Consumes an encoded DiagnosticInfo (OPC 10000-6 5.2.2.12) without materializing it.</summary>
    internal static void SkipDiagnosticInfo(ref UaBinaryReader reader, int depth)
    {
        if (depth > 32) throw new InvalidDataException("OPC UA DiagnosticInfo nesting exceeds 32 levels.");
        var mask = reader.ReadByte();
        if ((mask & 0x80) != 0) throw new InvalidDataException($"Invalid OPC UA DiagnosticInfo mask 0x{mask:X2}.");
        if ((mask & 0x01) != 0) _ = reader.ReadInt32();
        if ((mask & 0x02) != 0) _ = reader.ReadInt32();
        if ((mask & 0x04) != 0) _ = reader.ReadInt32();
        if ((mask & 0x08) != 0) _ = reader.ReadInt32();
        if ((mask & 0x10) != 0) _ = reader.ReadString();
        if ((mask & 0x20) != 0) _ = reader.ReadUInt32();
        if ((mask & 0x40) != 0) SkipDiagnosticInfo(ref reader, depth + 1);
    }

    /// <summary>Smallest encoding of one element of a built-in type (OPC 10000-6 5.2.2), for length bounds.</summary>
    private static int MinimumEncodedSize(byte builtIn)
    {
        return builtIn switch
        {
            1 or 2 or 3 => 1,
            4 or 5 => 2,
            6 or 7 or 10 or 19 => 4,
            8 or 9 or 11 or 13 => 8,
            14 => 16,
            12 or 15 or 16 or >= 26 and <= 31 => 4,
            17 or 18 => 2,
            20 => 6,
            22 => 3,
            _ => 1
        };
    }

    public static void WriteDataValue(UaBinaryWriter writer, DriverSample sample)
    {
        byte mask = 0x01;
        if (sample.Quality.NativeStatusCode != 0 || !sample.Quality.IsGood) mask |= 0x02;
        if (sample.SourceTimestampUtc != MinUtc) mask |= 0x04;
        if (sample.ServerTimestampUtc != MinUtc) mask |= 0x08;
        if (sample.SourcePicoseconds != 0) mask |= 0x10;
        if (sample.ServerPicoseconds != 0) mask |= 0x20;
        writer.WriteByte(mask);
        WriteVariant(writer, sample.Value);
        if ((mask & 0x02) != 0) writer.WriteUInt32(sample.Quality.NativeStatusCode);
        if ((mask & 0x04) != 0) writer.WriteDateTime(sample.SourceTimestampUtc);
        if ((mask & 0x10) != 0) writer.WriteUInt16(ClampPicoseconds(sample.SourcePicoseconds));
        if ((mask & 0x08) != 0) writer.WriteDateTime(sample.ServerTimestampUtc);
        if ((mask & 0x20) != 0) writer.WriteUInt16(ClampPicoseconds(sample.ServerPicoseconds));
    }

    public static DriverQuality MapQuality(uint statusCode)
    {
        var severity = statusCode & 0xC0000000u;
        var level = severity switch
        {
            0x00000000u => DriverQualityLevel.Good,
            0x40000000u => DriverQualityLevel.Uncertain,
            _ => DriverQualityLevel.Bad
        };
        return new DriverQuality(level, DriverQualityFlags.None, statusCode);
    }

    private static void WriteScalarBody(UaBinaryWriter writer, byte builtIn, DriverValue value)
    {
        switch (builtIn)
        {
            case 1: writer.WriteBoolean(value.AsBoolean()); break;
            case 2: writer.WriteSByte(value.AsInt8()); break;
            case 3: writer.WriteByte(value.AsUInt8()); break;
            case 4: writer.WriteInt16(value.AsInt16()); break;
            case 5: writer.WriteUInt16(value.AsUInt16()); break;
            case 6: writer.WriteInt32(value.AsInt32()); break;
            case 7: writer.WriteUInt32(value.AsUInt32()); break;
            case 8: writer.WriteInt64(value.AsInt64()); break;
            case 9: writer.WriteUInt64(value.AsUInt64()); break;
            case 10: writer.WriteFloat(value.AsFloat32()); break;
            case 11: writer.WriteDouble(value.AsFloat64()); break;
            case 12: writer.WriteString(value.AsString()); break;
            case 13: writer.WriteDateTime(value.AsDateTime()); break;
            case 14: writer.WriteGuid(value.AsGuid()); break;
            case 15: writer.WriteByteString(value.AsBytes().Span); break;
            case 16:
            {
                var xml = Encoding.UTF8.GetBytes(value.AsXmlElement());
                writer.WriteByteString(xml);
                break;
            }
            case 17: writer.WriteNodeId(value.AsNodeId()); break;
            case 18: writer.WriteExpandedNodeId(value.AsExpandedNodeId()); break;
            case 19: writer.WriteUInt32(value.AsStatusCode()); break;
            case 20: writer.WriteQualifiedName(value.AsQualifiedName()); break;
            case 21: writer.WriteLocalizedText(value.AsLocalizedText()); break;
            case 22: writer.WriteExtensionObject(value.AsExtensionObject()); break;
            case 24: WriteVariant(writer, value.AsVariant()); break;
            default:
                throw new NotSupportedException(
                    $"OPC UA Variant built-in type {builtIn} is not supported for encoding in this milestone.");
        }
    }

    private static DriverValue ReadScalarBody(ref UaBinaryReader reader, byte builtIn, int depth,
        ref bool unrepresentable)
    {
        switch (builtIn)
        {
            case 23:
                _ = ReadDataValue(ref reader, depth + 1);
                unrepresentable = true;
                return DriverValue.Null;
            case 24:
            {
                var inner = ReadVariant(ref reader, depth + 1, ref unrepresentable);
                // OPC 10000-6 5.2.2.16: a Variant cannot directly contain another Variant.
                if (inner.Type == IndustrialDataType.Variant)
                    throw new InvalidDataException("OPC UA Variant directly contains another Variant.");
                return DriverValue.FromVariant(inner);
            }
            case 25:
                SkipDiagnosticInfo(ref reader, 0);
                unrepresentable = true;
                return DriverValue.Null;
        }

        return builtIn switch
        {
            1 => DriverValue.FromBoolean(reader.ReadBoolean()),
            2 => DriverValue.FromInt8(reader.ReadSByte()),
            3 => DriverValue.FromUInt8(reader.ReadByte()),
            4 => DriverValue.FromInt16(reader.ReadInt16()),
            5 => DriverValue.FromUInt16(reader.ReadUInt16()),
            6 => DriverValue.FromInt32(reader.ReadInt32()),
            7 => DriverValue.FromUInt32(reader.ReadUInt32()),
            8 => DriverValue.FromInt64(reader.ReadInt64()),
            9 => DriverValue.FromUInt64(reader.ReadUInt64()),
            10 => DriverValue.FromFloat32(reader.ReadFloat()),
            11 => DriverValue.FromFloat64(reader.ReadDouble()),
            12 => DriverValue.FromString(reader.ReadString() ?? string.Empty),
            13 => DriverValue.FromDateTime(reader.ReadDateTime()),
            14 => DriverValue.FromGuid(reader.ReadGuid()),
            15 => DriverValue.FromBytes(reader.ReadByteString() ?? []),
            16 => DriverValue.FromXmlElement(Encoding.UTF8.GetString(reader.ReadByteString() ?? [])),
            17 => DriverValue.FromNodeId(reader.ReadNodeId()),
            18 => DriverValue.FromExpandedNodeId(reader.ReadExpandedNodeId()),
            19 => DriverValue.FromStatusCode(reader.ReadUInt32()),
            20 => DriverValue.FromQualifiedName(reader.ReadQualifiedName()),
            21 => DriverValue.FromLocalizedText(reader.ReadLocalizedText()),
            22 => DriverValue.FromExtensionObject(reader.ReadExtensionObject()),
            >= 26 and <= 31 => DriverValue.FromBytes(reader.ReadByteString() ?? []),
            _ => throw new InvalidDataException($"OPC UA Variant built-in type {builtIn} is not defined.")
        };
    }

    private static byte GetBuiltInTypeId(IndustrialDataType type)
    {
        return type switch
        {
            IndustrialDataType.Bool => 1,
            IndustrialDataType.Int8 => 2,
            IndustrialDataType.UInt8 => 3,
            IndustrialDataType.Int16 => 4,
            IndustrialDataType.UInt16 => 5,
            IndustrialDataType.Int32 or IndustrialDataType.Enumeration => 6,
            IndustrialDataType.UInt32 => 7,
            IndustrialDataType.Int64 => 8,
            IndustrialDataType.UInt64 => 9,
            IndustrialDataType.Float32 => 10,
            IndustrialDataType.Float64 or IndustrialDataType.Duration => 11,
            IndustrialDataType.String => 12,
            IndustrialDataType.DateTime => 13,
            IndustrialDataType.Guid => 14,
            IndustrialDataType.Bytes => 15,
            IndustrialDataType.XmlElement => 16,
            IndustrialDataType.NodeId => 17,
            IndustrialDataType.ExpandedNodeId => 18,
            IndustrialDataType.StatusCode => 19,
            IndustrialDataType.QualifiedName => 20,
            IndustrialDataType.LocalizedText => 21,
            IndustrialDataType.ExtensionObject => 22,
            IndustrialDataType.Variant => 24,
            _ => throw new NotSupportedException(
                $"Canonical type {type} has no direct OPC UA built-in Variant encoding.")
        };
    }

    private static IndustrialDataType GetCanonicalType(byte builtIn)
    {
        return builtIn switch
        {
            1 => IndustrialDataType.Bool,
            2 => IndustrialDataType.Int8,
            3 => IndustrialDataType.UInt8,
            4 => IndustrialDataType.Int16,
            5 => IndustrialDataType.UInt16,
            6 => IndustrialDataType.Int32,
            7 => IndustrialDataType.UInt32,
            8 => IndustrialDataType.Int64,
            9 => IndustrialDataType.UInt64,
            10 => IndustrialDataType.Float32,
            11 => IndustrialDataType.Float64,
            12 => IndustrialDataType.String,
            13 => IndustrialDataType.DateTime,
            14 => IndustrialDataType.Guid,
            15 => IndustrialDataType.Bytes,
            16 => IndustrialDataType.XmlElement,
            17 => IndustrialDataType.NodeId,
            18 => IndustrialDataType.ExpandedNodeId,
            19 => IndustrialDataType.StatusCode,
            20 => IndustrialDataType.QualifiedName,
            21 => IndustrialDataType.LocalizedText,
            22 => IndustrialDataType.ExtensionObject,
            24 => IndustrialDataType.Variant,
            >= 26 and <= 31 => IndustrialDataType.Bytes,
            _ => throw new NotSupportedException(
                $"OPC UA Variant built-in type {builtIn} is not supported in arrays by this milestone.")
        };
    }

    private static ushort ClampPicoseconds(ushort value)
    {
        return value >= 10_000 ? (ushort)9_999 : value;
    }
}
