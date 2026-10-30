using System.Buffers.Binary;

namespace Proxus.Drivers;

/// <summary>
///     Canonical fixed-width packed-layout codec. It is intentionally protocol-neutral: the layout
///     explicitly selects byte order. Protocol-native formats such as S7 DTL or Modbus BCD remain in
///     their protocol codecs.
/// </summary>
public static class DriverPackedLayoutCodec
{
    public static DriverResult<int> Encode(DriverPackedLayout layout, ReadOnlySpan<DriverValue> source,
        Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (source.Length < layout.ValueCount)
            return Failure("LAYOUT_SOURCE_SHORT", "Value buffer is shorter than the packed layout.");
        if (destination.Length < layout.ByteLength)
            return Failure("LAYOUT_DESTINATION_SHORT", "Byte buffer is shorter than the packed layout.");
        destination[..layout.ByteLength].Clear();
        foreach (var field in layout.Fields.Span)
        {
            var width = field.ByteLength / field.ValueCount;
            for (var i = 0; i < field.ValueCount; i++)
            {
                var value = source[field.ValueOffset + i];
                if (value.Type != field.DataType)
                    return Failure("LAYOUT_TYPE_MISMATCH",
                        $"Field '{field.Name}' expects {field.DataType} but received {value.Type}.");
                EncodeScalar(field.DataType, value, destination.Slice(field.ByteOffset + i * width, width),
                    field.ByteOrder);
            }
        }

        return DriverResult<int>.Success(layout.ByteLength);
    }

    public static DriverResult<int> Decode(DriverPackedLayout layout, ReadOnlySpan<byte> source,
        Span<DriverValue> destination)
    {
        ArgumentNullException.ThrowIfNull(layout);
        if (source.Length < layout.ByteLength)
            return Failure("LAYOUT_SOURCE_BYTES_SHORT", "Byte buffer is shorter than the packed layout.");
        if (destination.Length < layout.ValueCount)
            return Failure("LAYOUT_DESTINATION_VALUES_SHORT", "Value buffer is shorter than the packed layout.");
        foreach (var field in layout.Fields.Span)
        {
            var width = field.ByteLength / field.ValueCount;
            for (var i = 0; i < field.ValueCount; i++)
                destination[field.ValueOffset + i] = DecodeScalar(field.DataType,
                    source.Slice(field.ByteOffset + i * width, width), field.ByteOrder);
        }

        return DriverResult<int>.Success(layout.ValueCount);
    }

    private static void EncodeScalar(IndustrialDataType type, DriverValue value, Span<byte> s, DriverByteOrder order)
    {
        var le = order == DriverByteOrder.LittleEndian;
        switch (type)
        {
            case IndustrialDataType.Bool: s[0] = value.AsBoolean() ? (byte)1 : (byte)0; break;
            case IndustrialDataType.Int8: s[0] = unchecked((byte)value.AsInt8()); break;
            case IndustrialDataType.UInt8: s[0] = value.AsUInt8(); break;
            case IndustrialDataType.Char8: s[0] = checked((byte)value.AsChar8()); break;
            case IndustrialDataType.BitString8: s[0] = value.AsBitString8(); break;
            case IndustrialDataType.Int16: Write16(s, unchecked((ushort)value.AsInt16()), le); break;
            case IndustrialDataType.UInt16: Write16(s, value.AsUInt16(), le); break;
            case IndustrialDataType.Float16: Write16(s, BitConverter.HalfToUInt16Bits(value.AsFloat16()), le); break;
            case IndustrialDataType.Char16: Write16(s, value.AsChar16(), le); break;
            case IndustrialDataType.BitString16: Write16(s, value.AsBitString16(), le); break;
            case IndustrialDataType.Int32: Write32(s, unchecked((uint)value.AsInt32()), le); break;
            case IndustrialDataType.UInt32: Write32(s, value.AsUInt32(), le); break;
            case IndustrialDataType.Float32: Write32(s, BitConverter.SingleToUInt32Bits(value.AsFloat32()), le); break;
            case IndustrialDataType.Date: Write32(s, unchecked((uint)value.AsDate().DayNumber), le); break;
            case IndustrialDataType.TimeOfDay: Write64(s, unchecked((ulong)value.AsTimeOfDay().Ticks), le); break;
            case IndustrialDataType.BitString32: Write32(s, value.AsBitString32(), le); break;
            case IndustrialDataType.Int64: Write64(s, unchecked((ulong)value.AsInt64()), le); break;
            case IndustrialDataType.UInt64: Write64(s, value.AsUInt64(), le); break;
            case IndustrialDataType.Float64:
                Write64(s, unchecked((ulong)BitConverter.DoubleToInt64Bits(value.AsFloat64())), le); break;
            case IndustrialDataType.DateTime: Write64(s, unchecked((ulong)value.AsDateTime().Ticks), le); break;
            case IndustrialDataType.Duration: Write64(s, unchecked((ulong)value.AsDuration().Ticks), le); break;
            case IndustrialDataType.BitString64: Write64(s, value.AsBitString64(), le); break;
            case IndustrialDataType.Int128: Write128(s, unchecked((UInt128)value.AsInt128()), le); break;
            case IndustrialDataType.UInt128: Write128(s, value.AsUInt128(), le); break;
            case IndustrialDataType.Guid:
                if (!value.AsGuid().TryWriteBytes(s, !le, out var written) || written != 16)
                    throw new InvalidOperationException("Unable to encode Guid.");
                break;
            case IndustrialDataType.Decimal:
                EncodeDecimal(value.AsDecimal(), s, le); break;
            default:
                throw new InvalidOperationException($"{type} is not supported by the canonical packed-layout codec.");
        }
    }

    private static DriverValue DecodeScalar(IndustrialDataType type, ReadOnlySpan<byte> s, DriverByteOrder order)
    {
        var le = order == DriverByteOrder.LittleEndian;
        return type switch
        {
            IndustrialDataType.Bool => DriverValue.FromBoolean(s[0] != 0),
            IndustrialDataType.Int8 => DriverValue.FromInt8(unchecked((sbyte)s[0])),
            IndustrialDataType.UInt8 => DriverValue.FromUInt8(s[0]),
            IndustrialDataType.Char8 => DriverValue.FromChar8((char)s[0]),
            IndustrialDataType.BitString8 => DriverValue.FromBitString8(s[0]),
            IndustrialDataType.Int16 => DriverValue.FromInt16(unchecked((short)Read16(s, le))),
            IndustrialDataType.UInt16 => DriverValue.FromUInt16(Read16(s, le)),
            IndustrialDataType.Float16 => DriverValue.FromFloat16(BitConverter.UInt16BitsToHalf(Read16(s, le))),
            IndustrialDataType.Char16 => DriverValue.FromChar16((char)Read16(s, le)),
            IndustrialDataType.BitString16 => DriverValue.FromBitString16(Read16(s, le)),
            IndustrialDataType.Int32 => DriverValue.FromInt32(unchecked((int)Read32(s, le))),
            IndustrialDataType.UInt32 => DriverValue.FromUInt32(Read32(s, le)),
            IndustrialDataType.Float32 => DriverValue.FromFloat32(BitConverter.UInt32BitsToSingle(Read32(s, le))),
            IndustrialDataType.Date => DriverValue.FromDate(DateOnly.FromDayNumber(unchecked((int)Read32(s, le)))),
            IndustrialDataType.TimeOfDay => DriverValue.FromTimeOfDay(new TimeOnly(unchecked((long)Read64(s, le)))),
            IndustrialDataType.BitString32 => DriverValue.FromBitString32(Read32(s, le)),
            IndustrialDataType.Int64 => DriverValue.FromInt64(unchecked((long)Read64(s, le))),
            IndustrialDataType.UInt64 => DriverValue.FromUInt64(Read64(s, le)),
            IndustrialDataType.Float64 => DriverValue.FromFloat64(
                BitConverter.Int64BitsToDouble(unchecked((long)Read64(s, le)))),
            IndustrialDataType.DateTime => DriverValue.FromDateTime(new DateTime(unchecked((long)Read64(s, le)),
                DateTimeKind.Utc)),
            IndustrialDataType.Duration => DriverValue.FromDuration(new TimeSpan(unchecked((long)Read64(s, le)))),
            IndustrialDataType.BitString64 => DriverValue.FromBitString64(Read64(s, le)),
            IndustrialDataType.Int128 => DriverValue.FromInt128(unchecked((Int128)Read128(s, le))),
            IndustrialDataType.UInt128 => DriverValue.FromUInt128(Read128(s, le)),
            IndustrialDataType.Guid => DriverValue.FromGuid(new Guid(s, !le)),
            IndustrialDataType.Decimal => DriverValue.FromDecimal(DecodeDecimal(s, le)),
            _ => throw new InvalidOperationException($"{type} is not supported by the canonical packed-layout codec.")
        };
    }

    private static void EncodeDecimal(decimal value, Span<byte> s, bool le)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        for (var i = 0; i < 4; i++) Write32(s.Slice(i * 4, 4), unchecked((uint)bits[i]), le);
    }

    private static decimal DecodeDecimal(ReadOnlySpan<byte> s, bool le)
    {
        Span<int> bits = stackalloc int[4];
        for (var i = 0; i < 4; i++) bits[i] = unchecked((int)Read32(s.Slice(i * 4, 4), le));
        return new decimal(bits);
    }

    private static void Write16(Span<byte> s, ushort v, bool le)
    {
        if (le) BinaryPrimitives.WriteUInt16LittleEndian(s, v);
        else BinaryPrimitives.WriteUInt16BigEndian(s, v);
    }

    private static ushort Read16(ReadOnlySpan<byte> s, bool le)
    {
        return le ? BinaryPrimitives.ReadUInt16LittleEndian(s) : BinaryPrimitives.ReadUInt16BigEndian(s);
    }

    private static void Write32(Span<byte> s, uint v, bool le)
    {
        if (le) BinaryPrimitives.WriteUInt32LittleEndian(s, v);
        else BinaryPrimitives.WriteUInt32BigEndian(s, v);
    }

    private static uint Read32(ReadOnlySpan<byte> s, bool le)
    {
        return le ? BinaryPrimitives.ReadUInt32LittleEndian(s) : BinaryPrimitives.ReadUInt32BigEndian(s);
    }

    private static void Write64(Span<byte> s, ulong v, bool le)
    {
        if (le) BinaryPrimitives.WriteUInt64LittleEndian(s, v);
        else BinaryPrimitives.WriteUInt64BigEndian(s, v);
    }

    private static ulong Read64(ReadOnlySpan<byte> s, bool le)
    {
        return le ? BinaryPrimitives.ReadUInt64LittleEndian(s) : BinaryPrimitives.ReadUInt64BigEndian(s);
    }

    private static void Write128(Span<byte> s, UInt128 v, bool le)
    {
        if (le)
        {
            Write64(s, (ulong)v, true);
            Write64(s[8..], (ulong)(v >> 64), true);
        }
        else
        {
            Write64(s, (ulong)(v >> 64), false);
            Write64(s[8..], (ulong)v, false);
        }
    }

    private static UInt128 Read128(ReadOnlySpan<byte> s, bool le)
    {
        return le
            ? ((UInt128)Read64(s[8..], true) << 64) | Read64(s, true)
            : ((UInt128)Read64(s, false) << 64) | Read64(s[8..], false);
    }

    private static DriverResult<int> Failure(string code, string message)
    {
        return DriverResult<int>.Failure(new DriverError(
            DriverErrorKind.Data, code, message,
            Operation: DriverOperation.Validate,
            Cause: "The packed layout buffer does not match the compiled schema.",
            SuggestedAction: "Use layout.ValueCount/layout.ByteLength and values with the declared canonical types.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
