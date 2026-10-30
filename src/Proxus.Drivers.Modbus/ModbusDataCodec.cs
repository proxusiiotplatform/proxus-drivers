using System.Buffers.Binary;
using System.Text;

namespace Proxus.Drivers.Modbus;

/// <summary>
///     Canonical codec for contiguous 16-bit Modbus registers. Scalar layout transforms
///     are applied independently to each array element. Temporary wire buffers use stack
///     storage; only String and Bytes results allocate their natural managed representation.
/// </summary>
public static class ModbusDataCodec
{
    private static readonly UTF8Encoding sUtf8Strict =
        new(false, true);

    private static readonly UnicodeEncoding sUtf16LeStrict =
        new(false, false, true);

    private static readonly UnicodeEncoding sUtf16BeStrict =
        new(true, false, true);

    private static readonly decimal[] sDecimalPowers = CreateDecimalPowers();
    private static readonly DateOnly sUnixDateEpoch = new(1970, 1, 1);
    private static readonly DateOnly sS7DateEpoch = new(1990, 1, 1);

    public static DriverResult<DriverValue> Decode(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<ushort> registers)
    {
        var validation = descriptor.ValidateScalar();
        if (!validation.IsSuccess)
            return DriverResult<DriverValue>.Failure(validation.Error);

        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return Failure("MODBUS_CODEC_AREA", "Bit areas are decoded by the bit API.");

        var width = descriptor.GetElementRegisterWidth();
        if (!width.IsSuccess)
            return DriverResult<DriverValue>.Failure(width.Error);

        if (registers.Length < width.Value)
            return Failure(
                "MODBUS_DATA_SHORT",
                $"Expected {width.Value} registers but received {registers.Length}.");

        return DecodeScalar(descriptor, registers[..width.Value]);
    }

    public static DriverResult<int> DecodeMany(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<ushort> registers,
        Span<DriverValue> destination)
    {
        var validation = descriptor.ValidateArray();
        if (!validation.IsSuccess)
            return DriverResult<int>.Failure(validation.Error);

        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return FailureCount("MODBUS_CODEC_AREA", "Bit arrays are decoded by the bit API.");

        if (destination.Length < descriptor.ElementCount)
            return FailureCount(
                "MODBUS_DESTINATION_SHORT",
                $"Destination has {destination.Length} elements; {descriptor.ElementCount} are required.");

        var width = descriptor.GetElementRegisterWidth();
        if (!width.IsSuccess)
            return DriverResult<int>.Failure(width.Error);

        var required = descriptor.GetRequiredRegisterCount();
        if (!required.IsSuccess)
            return DriverResult<int>.Failure(required.Error);

        if (registers.Length < required.Value)
            return FailureCount(
                "MODBUS_DATA_SHORT",
                $"Expected {required.Value} registers but received {registers.Length}.");

        var scalarDescriptor = descriptor with { ElementCount = 1 };
        for (var i = 0; i < descriptor.ElementCount; i++)
        {
            var decoded = DecodeScalar(
                scalarDescriptor,
                registers.Slice(i * width.Value, width.Value));

            if (!decoded.IsSuccess)
                return DriverResult<int>.Failure(decoded.Error);

            destination[i] = decoded.Value;
        }

        return DriverResult<int>.Success(descriptor.ElementCount);
    }

    public static DriverResult<int> Encode(
        ModbusRegisterDescriptor descriptor,
        DriverValue value,
        Span<ushort> destination)
    {
        return Encode(descriptor, value, ReadOnlySpan<ushort>.Empty, destination);
    }

    public static DriverResult<int> Encode(
        ModbusRegisterDescriptor descriptor,
        DriverValue value,
        ReadOnlySpan<ushort> baseline,
        Span<ushort> destination)
    {
        var validation = descriptor.ValidateScalar();
        if (!validation.IsSuccess)
            return DriverResult<int>.Failure(validation.Error);

        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return FailureCount("MODBUS_CODEC_AREA", "Bit areas are encoded by the bit API.");

        if (descriptor.DataType != value.Type)
            return FailureCount(
                "MODBUS_VALUE_TYPE",
                $"Descriptor expects {descriptor.DataType}, but value is {value.Type}.");

        var bytePolicy = ValidateByteEncodingPolicy(descriptor, baseline.IsEmpty);
        if (bytePolicy is { } bytePolicyError)
            return DriverResult<int>.Failure(bytePolicyError);

        var width = descriptor.GetElementRegisterWidth();
        if (!width.IsSuccess)
            return DriverResult<int>.Failure(width.Error);

        if (destination.Length < width.Value)
            return FailureCount(
                "MODBUS_DESTINATION_SHORT",
                $"Destination has {destination.Length} registers; {width.Value} are required.");

        if (!baseline.IsEmpty && baseline.Length < width.Value)
            return FailureCount(
                "MODBUS_BASELINE_SHORT",
                $"Baseline has {baseline.Length} registers; {width.Value} are required.");

        return EncodeScalar(
            descriptor,
            value,
            baseline.IsEmpty ? baseline : baseline[..width.Value],
            destination[..width.Value]);
    }

    public static DriverResult<int> EncodeMany(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<DriverValue> values,
        Span<ushort> destination)
    {
        return EncodeMany(descriptor, values, ReadOnlySpan<ushort>.Empty, destination);
    }

    public static DriverResult<int> EncodeMany(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<DriverValue> values,
        ReadOnlySpan<ushort> baseline,
        Span<ushort> destination)
    {
        var validation = descriptor.ValidateArray();
        if (!validation.IsSuccess)
            return DriverResult<int>.Failure(validation.Error);

        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return FailureCount("MODBUS_CODEC_AREA", "Bit arrays are encoded by the bit API.");

        if (values.Length != descriptor.ElementCount)
            return FailureCount(
                "MODBUS_ELEMENT_COUNT",
                $"Expected {descriptor.ElementCount} values, received {values.Length}.");

        var bytePolicy = ValidateByteEncodingPolicy(descriptor, baseline.IsEmpty);
        if (bytePolicy is { } bytePolicyError)
            return DriverResult<int>.Failure(bytePolicyError);

        var width = descriptor.GetElementRegisterWidth();
        if (!width.IsSuccess)
            return DriverResult<int>.Failure(width.Error);

        var required = descriptor.GetRequiredRegisterCount();
        if (!required.IsSuccess)
            return DriverResult<int>.Failure(required.Error);

        if (destination.Length < required.Value)
            return FailureCount(
                "MODBUS_DESTINATION_SHORT",
                $"Destination has {destination.Length} registers; {required.Value} are required.");

        if (!baseline.IsEmpty && baseline.Length < required.Value)
            return FailureCount(
                "MODBUS_BASELINE_SHORT",
                $"Baseline has {baseline.Length} registers; {required.Value} are required.");

        var scalarDescriptor = descriptor with { ElementCount = 1 };
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i].Type != descriptor.DataType)
                return FailureCount(
                    "MODBUS_VALUE_TYPE",
                    $"Element {i} is {values[i].Type}; expected {descriptor.DataType}.");

            var offset = i * width.Value;
            var encoded = EncodeScalar(
                scalarDescriptor,
                values[i],
                baseline.IsEmpty ? baseline : baseline.Slice(offset, width.Value),
                destination.Slice(offset, width.Value));

            if (!encoded.IsSuccess)
                return DriverResult<int>.Failure(encoded.Error);
        }

        return DriverResult<int>.Success(required.Value);
    }

    internal static void CopyRegistersToCanonical(
        ReadOnlySpan<ushort> registers,
        Span<byte> destination,
        ModbusRegisterLayout layout)
    {
        if (destination.Length < registers.Length * 2)
            throw new ArgumentException("Destination is too small.", nameof(destination));

        for (var canonicalWord = 0; canonicalWord < registers.Length; canonicalWord++)
        {
            var sourceWord = layout.ReverseWordOrder
                ? registers.Length - 1 - canonicalWord
                : canonicalWord;

            var value = registers[sourceWord];
            var target = destination.Slice(canonicalWord * 2, 2);
            if (layout.SwapBytesInWord)
            {
                target[0] = (byte)value;
                target[1] = (byte)(value >> 8);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(target, value);
            }
        }
    }

    internal static void CopyCanonicalToRegisters(
        ReadOnlySpan<byte> canonical,
        Span<ushort> destination,
        ModbusRegisterLayout layout)
    {
        if (canonical.Length < destination.Length * 2)
            throw new ArgumentException("Canonical source is too small.", nameof(canonical));

        for (var canonicalWord = 0; canonicalWord < destination.Length; canonicalWord++)
        {
            var bytes = canonical.Slice(canonicalWord * 2, 2);
            var value = layout.SwapBytesInWord
                ? (ushort)((bytes[1] << 8) | bytes[0])
                : BinaryPrimitives.ReadUInt16BigEndian(bytes);

            var targetWord = layout.ReverseWordOrder
                ? destination.Length - 1 - canonicalWord
                : canonicalWord;

            destination[targetWord] = value;
        }
    }

    private static DriverResult<DriverValue> DecodeScalar(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<ushort> registers)
    {
        Span<byte> canonical = stackalloc byte[registers.Length * 2];
        CopyRegistersToCanonical(registers, canonical, descriptor.Layout);

        try
        {
            var byteIndex = descriptor.ByteLane == ModbusByteLane.High ? 0 : 1;
            return descriptor.DataType switch
            {
                IndustrialDataType.Bool => Success(
                    DriverValue.FromBoolean(
                        BinaryPrimitives.ReadUInt16BigEndian(canonical) != 0)),

                IndustrialDataType.Int8 => Success(
                    DriverValue.FromInt8(unchecked((sbyte)canonical[byteIndex]))),

                IndustrialDataType.UInt8 => Success(
                    DriverValue.FromUInt8(canonical[byteIndex])),

                IndustrialDataType.Int16 => Success(
                    DriverValue.FromInt16(BinaryPrimitives.ReadInt16BigEndian(canonical))),

                IndustrialDataType.UInt16 => Success(
                    DriverValue.FromUInt16(BinaryPrimitives.ReadUInt16BigEndian(canonical))),

                IndustrialDataType.Int32 => Success(
                    DriverValue.FromInt32(BinaryPrimitives.ReadInt32BigEndian(canonical))),

                IndustrialDataType.UInt32 => Success(
                    DriverValue.FromUInt32(BinaryPrimitives.ReadUInt32BigEndian(canonical))),

                IndustrialDataType.Int64 => Success(
                    DriverValue.FromInt64(BinaryPrimitives.ReadInt64BigEndian(canonical))),

                IndustrialDataType.UInt64 => Success(
                    DriverValue.FromUInt64(BinaryPrimitives.ReadUInt64BigEndian(canonical))),

                IndustrialDataType.Int128 => Success(DriverValue.FromInt128(ReadInt128BigEndian(canonical))),
                IndustrialDataType.UInt128 => Success(DriverValue.FromUInt128(ReadUInt128BigEndian(canonical))),
                IndustrialDataType.Float16 => Success(DriverValue.FromFloat16(
                    BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16BigEndian(canonical)))),

                IndustrialDataType.Float32 => Success(
                    DriverValue.FromFloat32(
                        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32BigEndian(canonical)))),

                IndustrialDataType.Float64 => Success(
                    DriverValue.FromFloat64(
                        BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64BigEndian(canonical)))),

                IndustrialDataType.Decimal => DecodeDecimal(descriptor, canonical),
                IndustrialDataType.Char8 => Success(DriverValue.FromChar8((char)canonical[byteIndex])),
                IndustrialDataType.Char16 => Success(
                    DriverValue.FromChar16((char)BinaryPrimitives.ReadUInt16BigEndian(canonical))),
                IndustrialDataType.String => DecodeString(descriptor, canonical),
                IndustrialDataType.Bytes => DecodeBytes(descriptor, canonical),
                IndustrialDataType.Guid => DecodeGuid(descriptor, canonical),
                IndustrialDataType.Date => DecodeDate(descriptor, canonical),
                IndustrialDataType.TimeOfDay => DecodeTimeOfDay(descriptor, canonical),
                IndustrialDataType.DateTime => DecodeDateTime(descriptor, canonical),
                IndustrialDataType.Duration => DecodeDuration(descriptor, canonical),
                IndustrialDataType.BitString8 => Success(DriverValue.FromBitString8(canonical[byteIndex])),
                IndustrialDataType.BitString16 => Success(
                    DriverValue.FromBitString16(BinaryPrimitives.ReadUInt16BigEndian(canonical))),
                IndustrialDataType.BitString32 => Success(
                    DriverValue.FromBitString32(BinaryPrimitives.ReadUInt32BigEndian(canonical))),
                IndustrialDataType.BitString64 => Success(
                    DriverValue.FromBitString64(BinaryPrimitives.ReadUInt64BigEndian(canonical))),
                IndustrialDataType.Enumeration => DecodeEnumeration(descriptor.EnumerationStorageType, canonical,
                    byteIndex),
                _ => Failure("MODBUS_DATA_TYPE", $"Unsupported data type {descriptor.DataType}.")
            };
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or DecoderFallbackException)
        {
            return Failure("MODBUS_DECODE", ex.Message);
        }
    }

    private static DriverResult<int> EncodeScalar(
        ModbusRegisterDescriptor descriptor,
        DriverValue value,
        ReadOnlySpan<ushort> baseline,
        Span<ushort> destination)
    {
        Span<byte> canonical = stackalloc byte[destination.Length * 2];
        if (baseline.IsEmpty)
            canonical.Clear();
        else
            CopyRegistersToCanonical(baseline, canonical, descriptor.Layout);

        try
        {
            var byteIndex = descriptor.ByteLane == ModbusByteLane.High ? 0 : 1;
            switch (descriptor.DataType)
            {
                case IndustrialDataType.Bool:
                    BinaryPrimitives.WriteUInt16BigEndian(
                        canonical,
                        value.AsBoolean() ? descriptor.RegisterBoolTrueValue : (ushort)0);
                    break;

                case IndustrialDataType.Int8:
                    canonical[byteIndex] = unchecked((byte)value.AsInt8());
                    break;

                case IndustrialDataType.UInt8:
                    canonical[byteIndex] = value.AsUInt8();
                    break;

                case IndustrialDataType.Int16:
                    BinaryPrimitives.WriteInt16BigEndian(canonical, value.AsInt16());
                    break;

                case IndustrialDataType.UInt16:
                    BinaryPrimitives.WriteUInt16BigEndian(canonical, value.AsUInt16());
                    break;

                case IndustrialDataType.Int32:
                    BinaryPrimitives.WriteInt32BigEndian(canonical, value.AsInt32());
                    break;

                case IndustrialDataType.UInt32:
                    BinaryPrimitives.WriteUInt32BigEndian(canonical, value.AsUInt32());
                    break;

                case IndustrialDataType.Int64:
                    BinaryPrimitives.WriteInt64BigEndian(canonical, value.AsInt64());
                    break;

                case IndustrialDataType.UInt64:
                    BinaryPrimitives.WriteUInt64BigEndian(canonical, value.AsUInt64());
                    break;

                case IndustrialDataType.Int128:
                    WriteUInt128BigEndian(canonical, unchecked((UInt128)value.AsInt128()));
                    break;

                case IndustrialDataType.UInt128:
                    WriteUInt128BigEndian(canonical, value.AsUInt128());
                    break;

                case IndustrialDataType.Float16:
                    BinaryPrimitives.WriteUInt16BigEndian(canonical, BitConverter.HalfToUInt16Bits(value.AsFloat16()));
                    break;

                case IndustrialDataType.Float32:
                    BinaryPrimitives.WriteInt32BigEndian(
                        canonical,
                        BitConverter.SingleToInt32Bits(value.AsFloat32()));
                    break;

                case IndustrialDataType.Float64:
                    BinaryPrimitives.WriteInt64BigEndian(
                        canonical,
                        BitConverter.DoubleToInt64Bits(value.AsFloat64()));
                    break;

                case IndustrialDataType.Decimal:
                    EncodeDecimal(descriptor, value.AsDecimal(), canonical);
                    break;

                case IndustrialDataType.Char8:
                    canonical[byteIndex] = checked((byte)value.AsChar8());
                    break;

                case IndustrialDataType.Char16:
                    BinaryPrimitives.WriteUInt16BigEndian(canonical, value.AsChar16());
                    break;

                case IndustrialDataType.String:
                    EncodeString(descriptor, value.AsString(), canonical);
                    break;

                case IndustrialDataType.Bytes:
                    EncodeBytes(descriptor, value.AsBytes().Span, canonical);
                    break;

                case IndustrialDataType.Guid:
                    EncodeGuid(descriptor, value.AsGuid(), canonical);
                    break;

                case IndustrialDataType.Date:
                    EncodeDate(descriptor, value.AsDate(), canonical);
                    break;

                case IndustrialDataType.TimeOfDay:
                    EncodeTimeOfDay(descriptor, value.AsTimeOfDay(), canonical);
                    break;

                case IndustrialDataType.DateTime:
                    EncodeDateTime(descriptor, value.AsDateTime(), canonical);
                    break;

                case IndustrialDataType.Duration:
                    EncodeDuration(descriptor, value.AsDuration(), canonical);
                    break;

                case IndustrialDataType.BitString8:
                    canonical[byteIndex] = value.AsBitString8();
                    break;

                case IndustrialDataType.BitString16:
                    BinaryPrimitives.WriteUInt16BigEndian(canonical, value.AsBitString16());
                    break;

                case IndustrialDataType.BitString32:
                    BinaryPrimitives.WriteUInt32BigEndian(canonical, value.AsBitString32());
                    break;

                case IndustrialDataType.BitString64:
                    BinaryPrimitives.WriteUInt64BigEndian(canonical, value.AsBitString64());
                    break;

                case IndustrialDataType.Enumeration:
                    EncodeEnumeration(descriptor.EnumerationStorageType, value, canonical, byteIndex);
                    break;

                default:
                    return FailureCount(
                        "MODBUS_DATA_TYPE",
                        $"Unsupported data type {descriptor.DataType}.");
            }

            CopyCanonicalToRegisters(canonical, destination, descriptor.Layout);
            return DriverResult<int>.Success(destination.Length);
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException or EncoderFallbackException)
        {
            return FailureCount("MODBUS_ENCODE", ex.Message);
        }
    }

    private static decimal[] CreateDecimalPowers()
    {
        var values = new decimal[29];
        values[0] = 1m;
        for (var i = 1; i < values.Length; i++) values[i] = values[i - 1] * 10m;
        return values;
    }

    private static UInt128 ReadUInt128BigEndian(ReadOnlySpan<byte> source)
    {
        return ((UInt128)BinaryPrimitives.ReadUInt64BigEndian(source) << 64) |
               BinaryPrimitives.ReadUInt64BigEndian(source[8..]);
    }

    private static Int128 ReadInt128BigEndian(ReadOnlySpan<byte> source)
    {
        return unchecked((Int128)ReadUInt128BigEndian(source));
    }

    private static void WriteUInt128BigEndian(Span<byte> destination, UInt128 value)
    {
        BinaryPrimitives.WriteUInt64BigEndian(destination, (ulong)(value >> 64));
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..], (ulong)value);
    }

    private static DriverResult<DriverValue> DecodeEnumeration(
        IndustrialDataType storageType, ReadOnlySpan<byte> canonical, int byteIndex)
    {
        return storageType switch
        {
            IndustrialDataType.Int8 => Success(DriverValue.FromEnumeration(unchecked((sbyte)canonical[byteIndex]))),
            IndustrialDataType.UInt8 => Success(DriverValue.FromUnsignedEnumeration(canonical[byteIndex])),
            IndustrialDataType.Int16 => Success(
                DriverValue.FromEnumeration(BinaryPrimitives.ReadInt16BigEndian(canonical))),
            IndustrialDataType.UInt16 => Success(
                DriverValue.FromUnsignedEnumeration(BinaryPrimitives.ReadUInt16BigEndian(canonical))),
            IndustrialDataType.Int32 => Success(
                DriverValue.FromEnumeration(BinaryPrimitives.ReadInt32BigEndian(canonical))),
            IndustrialDataType.UInt32 => Success(
                DriverValue.FromUnsignedEnumeration(BinaryPrimitives.ReadUInt32BigEndian(canonical))),
            IndustrialDataType.Int64 => Success(
                DriverValue.FromEnumeration(BinaryPrimitives.ReadInt64BigEndian(canonical))),
            IndustrialDataType.UInt64 => Success(
                DriverValue.FromUnsignedEnumeration(BinaryPrimitives.ReadUInt64BigEndian(canonical))),
            _ => throw new ArgumentException($"Unsupported Modbus enumeration storage type {storageType}.")
        };
    }

    private static void EncodeEnumeration(
        IndustrialDataType storageType, DriverValue value, Span<byte> canonical, int byteIndex)
    {
        if (IndustrialDataTypeInfo.IsUnsignedEnumerationStorageType(storageType))
        {
            var raw = value.AsEnumerationUInt64();
            switch (storageType)
            {
                case IndustrialDataType.UInt8: canonical[byteIndex] = checked((byte)raw); break;
                case IndustrialDataType.UInt16:
                    BinaryPrimitives.WriteUInt16BigEndian(canonical, checked((ushort)raw)); break;
                case IndustrialDataType.UInt32:
                    BinaryPrimitives.WriteUInt32BigEndian(canonical, checked((uint)raw)); break;
                case IndustrialDataType.UInt64: BinaryPrimitives.WriteUInt64BigEndian(canonical, raw); break;
                default: throw new ArgumentException($"Unsupported Modbus enumeration storage type {storageType}.");
            }

            return;
        }

        var signed = value.AsEnumerationInt64();
        switch (storageType)
        {
            case IndustrialDataType.Int8: canonical[byteIndex] = unchecked((byte)checked((sbyte)signed)); break;
            case IndustrialDataType.Int16:
                BinaryPrimitives.WriteInt16BigEndian(canonical, checked((short)signed)); break;
            case IndustrialDataType.Int32: BinaryPrimitives.WriteInt32BigEndian(canonical, checked((int)signed)); break;
            case IndustrialDataType.Int64: BinaryPrimitives.WriteInt64BigEndian(canonical, signed); break;
            default: throw new ArgumentException($"Unsupported Modbus enumeration storage type {storageType}.");
        }
    }

    private static decimal DecodePackedBcd(ReadOnlySpan<byte> source)
    {
        var value = 0m;
        for (var i = 0; i < source.Length; i++)
        {
            var high = source[i] >> 4;
            var low = source[i] & 0x0F;
            if (high > 9 || low > 9)
                throw new ArgumentException($"Packed BCD contains invalid digit in byte {i}: 0x{source[i]:X2}.");
            value = checked(value * 100m + high * 10 + low);
        }

        return value;
    }

    private static void EncodePackedBcd(decimal scaled, Span<byte> destination)
    {
        if (scaled < 0)
            throw new ArgumentOutOfRangeException(nameof(scaled),
                "Unsigned packed BCD cannot represent negative values.");
        if (decimal.Truncate(scaled) != scaled)
            throw new ArgumentException("Packed BCD input must be an integer after applying DecimalScale.");

        destination.Clear();
        var remaining = scaled;
        for (var i = destination.Length - 1; i >= 0; i--)
        {
            var low = (int)(remaining % 10m);
            remaining = decimal.Truncate(remaining / 10m);
            var high = (int)(remaining % 10m);
            remaining = decimal.Truncate(remaining / 10m);
            destination[i] = (byte)((high << 4) | low);
        }

        if (remaining != 0m)
            throw new OverflowException($"Packed BCD value does not fit {destination.Length * 2} digits.");
    }

    private static DriverResult<DriverValue> DecodeDecimal(
        ModbusRegisterDescriptor descriptor, ReadOnlySpan<byte> canonical)
    {
        var raw = descriptor.DecimalFormat switch
        {
            ModbusDecimalFormat.ScaledInt16 => BinaryPrimitives.ReadInt16BigEndian(canonical),
            ModbusDecimalFormat.ScaledUInt16 => BinaryPrimitives.ReadUInt16BigEndian(canonical),
            ModbusDecimalFormat.ScaledInt32 => BinaryPrimitives.ReadInt32BigEndian(canonical),
            ModbusDecimalFormat.ScaledUInt32 => BinaryPrimitives.ReadUInt32BigEndian(canonical),
            ModbusDecimalFormat.ScaledInt64 => BinaryPrimitives.ReadInt64BigEndian(canonical),
            ModbusDecimalFormat.ScaledUInt64 => BinaryPrimitives.ReadUInt64BigEndian(canonical),
            ModbusDecimalFormat.PackedBcd16 or ModbusDecimalFormat.PackedBcd32 or ModbusDecimalFormat.PackedBcd64 =>
                DecodePackedBcd(canonical),
            _ => throw new ArgumentException("Unsupported Modbus decimal format.")
        };
        return Success(DriverValue.FromDecimal(raw / sDecimalPowers[descriptor.DecimalScale]));
    }

    private static void EncodeDecimal(
        ModbusRegisterDescriptor descriptor, decimal value, Span<byte> canonical)
    {
        var scaled = checked(value * sDecimalPowers[descriptor.DecimalScale]);
        if (decimal.Truncate(scaled) != scaled)
            throw new ArgumentException(
                $"Decimal {value} cannot be represented exactly at scale {descriptor.DecimalScale}.");
        switch (descriptor.DecimalFormat)
        {
            case ModbusDecimalFormat.ScaledInt16:
                BinaryPrimitives.WriteInt16BigEndian(canonical, checked((short)scaled)); break;
            case ModbusDecimalFormat.ScaledUInt16:
                BinaryPrimitives.WriteUInt16BigEndian(canonical, checked((ushort)scaled)); break;
            case ModbusDecimalFormat.ScaledInt32:
                BinaryPrimitives.WriteInt32BigEndian(canonical, checked((int)scaled)); break;
            case ModbusDecimalFormat.ScaledUInt32:
                BinaryPrimitives.WriteUInt32BigEndian(canonical, checked((uint)scaled)); break;
            case ModbusDecimalFormat.ScaledInt64:
                BinaryPrimitives.WriteInt64BigEndian(canonical, checked((long)scaled)); break;
            case ModbusDecimalFormat.ScaledUInt64:
                BinaryPrimitives.WriteUInt64BigEndian(canonical, checked((ulong)scaled)); break;
            case ModbusDecimalFormat.PackedBcd16:
            case ModbusDecimalFormat.PackedBcd32:
            case ModbusDecimalFormat.PackedBcd64:
                EncodePackedBcd(scaled, canonical);
                break;
            default: throw new ArgumentException("Unsupported Modbus decimal format.");
        }
    }

    private static DriverResult<DriverValue> DecodeGuid(
        ModbusRegisterDescriptor descriptor, ReadOnlySpan<byte> canonical)
    {
        var value = descriptor.GuidFormat switch
        {
            ModbusGuidFormat.Rfc4122NetworkOrder => new Guid(canonical[..16], true),
            ModbusGuidFormat.DotNetMixedEndian => new Guid(canonical[..16]),
            _ => throw new ArgumentException("Unsupported Modbus Guid format.")
        };
        return Success(DriverValue.FromGuid(value));
    }

    private static void EncodeGuid(
        ModbusRegisterDescriptor descriptor, Guid value, Span<byte> canonical)
    {
        var bigEndian = descriptor.GuidFormat == ModbusGuidFormat.Rfc4122NetworkOrder;
        if (descriptor.GuidFormat == ModbusGuidFormat.None ||
            !value.TryWriteBytes(canonical[..16], bigEndian, out var written) || written != 16)
            throw new ArgumentException("Unable to encode Guid using the selected Modbus Guid format.");
    }

    private static DriverResult<DriverValue> DecodeDate(
        ModbusRegisterDescriptor descriptor, ReadOnlySpan<byte> canonical)
    {
        var value = descriptor.TemporalFormat switch
        {
            ModbusTemporalFormat.DateDaysSinceUnixInt32 => sUnixDateEpoch.AddDays(
                BinaryPrimitives.ReadInt32BigEndian(canonical)),
            ModbusTemporalFormat.DateDaysSince1990UInt16 => sS7DateEpoch.AddDays(
                BinaryPrimitives.ReadUInt16BigEndian(canonical)),
            _ => throw new ArgumentException("Unsupported Modbus Date format.")
        };
        return Success(DriverValue.FromDate(value));
    }

    private static void EncodeDate(
        ModbusRegisterDescriptor descriptor, DateOnly value, Span<byte> destination)
    {
        switch (descriptor.TemporalFormat)
        {
            case ModbusTemporalFormat.DateDaysSinceUnixInt32:
                BinaryPrimitives.WriteInt32BigEndian(destination, checked(value.DayNumber - sUnixDateEpoch.DayNumber));
                break;
            case ModbusTemporalFormat.DateDaysSince1990UInt16:
                BinaryPrimitives.WriteUInt16BigEndian(destination,
                    checked((ushort)(value.DayNumber - sS7DateEpoch.DayNumber)));
                break;
            default: throw new ArgumentException("Unsupported Modbus Date format.");
        }
    }

    private static DriverResult<DriverValue> DecodeTimeOfDay(
        ModbusRegisterDescriptor descriptor, ReadOnlySpan<byte> canonical)
    {
        var ticks = descriptor.TemporalFormat switch
        {
            ModbusTemporalFormat.TimeOfDayMillisecondsUInt32 =>
                checked(BinaryPrimitives.ReadUInt32BigEndian(canonical) * TimeSpan.TicksPerMillisecond),
            ModbusTemporalFormat.TimeOfDaySecondsUInt32 => checked(BinaryPrimitives.ReadUInt32BigEndian(canonical) *
                                                                   TimeSpan.TicksPerSecond),
            ModbusTemporalFormat.TimeOfDayNanosecondsUInt64 => NanosecondsToTicksChecked(
                BinaryPrimitives.ReadUInt64BigEndian(canonical)),
            _ => throw new ArgumentException("Unsupported Modbus TimeOfDay format.")
        };
        if (ticks < 0 || ticks >= TimeSpan.TicksPerDay)
            throw new ArgumentOutOfRangeException(nameof(canonical), "TimeOfDay is outside a 24-hour day.");
        return Success(DriverValue.FromTimeOfDay(new TimeOnly(ticks)));
    }

    private static void EncodeTimeOfDay(
        ModbusRegisterDescriptor descriptor, TimeOnly value, Span<byte> destination)
    {
        switch (descriptor.TemporalFormat)
        {
            case ModbusTemporalFormat.TimeOfDayMillisecondsUInt32:
                EnsureDivisible(value.Ticks, TimeSpan.TicksPerMillisecond, "TimeOfDayMilliseconds");
                BinaryPrimitives.WriteUInt32BigEndian(destination,
                    checked((uint)(value.Ticks / TimeSpan.TicksPerMillisecond)));
                break;
            case ModbusTemporalFormat.TimeOfDaySecondsUInt32:
                EnsureDivisible(value.Ticks, TimeSpan.TicksPerSecond, "TimeOfDaySeconds");
                BinaryPrimitives.WriteUInt32BigEndian(destination,
                    checked((uint)(value.Ticks / TimeSpan.TicksPerSecond)));
                break;
            case ModbusTemporalFormat.TimeOfDayNanosecondsUInt64:
                BinaryPrimitives.WriteUInt64BigEndian(destination, checked((ulong)value.Ticks * 100UL));
                break;
            default: throw new ArgumentException("Unsupported Modbus TimeOfDay format.");
        }
    }

    private static long NanosecondsToTicksChecked(ulong nanoseconds)
    {
        if (nanoseconds % 100UL != 0)
            throw new ArgumentException("Nanoseconds cannot be represented exactly by .NET 100ns ticks.");
        return checked((long)(nanoseconds / 100UL));
    }

    private static void EnsureDivisible(long value, long divisor, string format)
    {
        if (value % divisor != 0)
            throw new ArgumentException($"{format} would lose precision.");
    }

    private static DriverResult<DriverValue> DecodeString(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<byte> canonical)
    {
        var storage = canonical[..descriptor.ByteLength];
        var bytes = descriptor.StringTermination switch
        {
            ModbusStringTermination.NullTerminated => SliceAtNullTerminator(storage, descriptor.StringEncoding),
            ModbusStringTermination.SpacePadded => TrimSpacePadding(storage, descriptor.StringEncoding),
            ModbusStringTermination.ByteLengthPrefixed8 => SliceByteLengthPrefixed8(storage, descriptor.StringEncoding),
            ModbusStringTermination.ByteLengthPrefixed16BigEndian => SliceByteLengthPrefixed16(storage,
                descriptor.StringEncoding),
            _ => descriptor.TrimTrailingNulls ? TrimNullPadding(storage, descriptor.StringEncoding) : storage
        };

        if (descriptor.StringEncoding == ModbusStringEncoding.Ascii)
            ValidateAsciiBytes(bytes);

        return Success(DriverValue.FromString(GetEncoding(descriptor.StringEncoding).GetString(bytes)));
    }

    private static DriverResult<DriverValue> DecodeBytes(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<byte> canonical)
    {
        return Success(DriverValue.FromOwnedBytes([.. canonical[..descriptor.ByteLength]]));
    }

    private static DriverResult<DriverValue> DecodeDateTime(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<byte> canonical)
    {
        var value = descriptor.TemporalFormat switch
        {
            ModbusTemporalFormat.UnixSecondsInt32 =>
                DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt32BigEndian(canonical)).UtcDateTime,

            ModbusTemporalFormat.UnixSecondsUInt32 =>
                DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadUInt32BigEndian(canonical)).UtcDateTime,

            ModbusTemporalFormat.UnixSecondsInt64 =>
                DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64BigEndian(canonical)).UtcDateTime,

            ModbusTemporalFormat.UnixMillisecondsInt64 =>
                DateTimeOffset.FromUnixTimeMilliseconds(BinaryPrimitives.ReadInt64BigEndian(canonical)).UtcDateTime,

            ModbusTemporalFormat.DotNetTicksInt64 =>
                new DateTime(BinaryPrimitives.ReadInt64BigEndian(canonical), DateTimeKind.Utc),

            _ => throw new ArgumentException("Unsupported DateTime temporal format.")
        };

        return Success(DriverValue.FromDateTime(value));
    }

    private static DriverResult<DriverValue> DecodeDuration(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<byte> canonical)
    {
        var ticks = descriptor.TemporalFormat switch
        {
            ModbusTemporalFormat.DurationMillisecondsInt32 =>
                checked(BinaryPrimitives.ReadInt32BigEndian(canonical) * TimeSpan.TicksPerMillisecond),

            ModbusTemporalFormat.DurationMillisecondsInt64 =>
                checked(BinaryPrimitives.ReadInt64BigEndian(canonical) * TimeSpan.TicksPerMillisecond),

            ModbusTemporalFormat.DurationTicksInt64 =>
                BinaryPrimitives.ReadInt64BigEndian(canonical),

            _ => throw new ArgumentException("Unsupported Duration temporal format.")
        };

        return Success(DriverValue.FromDuration(TimeSpan.FromTicks(ticks)));
    }

    private static void EncodeString(
        ModbusRegisterDescriptor descriptor,
        string value,
        Span<byte> destination)
    {
        if (descriptor.StringEncoding == ModbusStringEncoding.Ascii)
            ValidateAsciiChars(value.AsSpan());

        var encoding = GetEncoding(descriptor.StringEncoding);
        var target = destination[..descriptor.ByteLength];
        target.Clear();
        var byteCount = encoding.GetByteCount(value.AsSpan());

        switch (descriptor.StringTermination)
        {
            case ModbusStringTermination.NullTerminated:
            {
                var terminatorWidth = GetNullTerminatorWidth(descriptor.StringEncoding);
                if (byteCount + terminatorWidth > target.Length)
                    throw new ArgumentException(
                        $"Encoded string and terminator require {byteCount + terminatorWidth} bytes, but ByteLength is {target.Length}.");
                _ = encoding.GetBytes(value.AsSpan(), target);
                return;
            }
            case ModbusStringTermination.SpacePadded:
            {
                if (byteCount > target.Length)
                    throw new ArgumentException(
                        $"Encoded string requires {byteCount} bytes, but ByteLength is {target.Length}.");
                FillSpacePadding(target, descriptor.StringEncoding);
                _ = encoding.GetBytes(value.AsSpan(), target);
                return;
            }
            case ModbusStringTermination.ByteLengthPrefixed8:
            {
                if (byteCount > byte.MaxValue || byteCount + 1 > target.Length)
                    throw new ArgumentException(
                        $"Encoded string requires {byteCount} payload bytes; 8-bit length framing allows at most {Math.Min(byte.MaxValue, target.Length - 1)}.");
                target[0] = checked((byte)byteCount);
                _ = encoding.GetBytes(value.AsSpan(), target[1..]);
                return;
            }
            case ModbusStringTermination.ByteLengthPrefixed16BigEndian:
            {
                if (byteCount > ushort.MaxValue || byteCount + 2 > target.Length)
                    throw new ArgumentException(
                        $"Encoded string requires {byteCount} payload bytes; 16-bit length framing capacity is {target.Length - 2}.");
                BinaryPrimitives.WriteUInt16BigEndian(target, checked((ushort)byteCount));
                _ = encoding.GetBytes(value.AsSpan(), target[2..]);
                return;
            }
            default:
                if (byteCount > target.Length)
                    throw new ArgumentException(
                        $"Encoded string requires {byteCount} bytes, but ByteLength is {target.Length}.");
                _ = encoding.GetBytes(value.AsSpan(), target);
                return;
        }
    }

    private static ReadOnlySpan<byte> SliceByteLengthPrefixed8(ReadOnlySpan<byte> storage,
        ModbusStringEncoding encoding)
    {
        var length = storage[0];
        if (length > storage.Length - 1)
            throw new ArgumentException(
                $"8-bit string length prefix {length} exceeds payload capacity {storage.Length - 1}.");
        ValidateEncodedLength(length, encoding);
        return storage.Slice(1, length);
    }

    private static ReadOnlySpan<byte> SliceByteLengthPrefixed16(ReadOnlySpan<byte> storage,
        ModbusStringEncoding encoding)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(storage);
        if (length > storage.Length - 2)
            throw new ArgumentException(
                $"16-bit string length prefix {length} exceeds payload capacity {storage.Length - 2}.");
        ValidateEncodedLength(length, encoding);
        return storage.Slice(2, length);
    }

    private static void ValidateEncodedLength(int byteLength, ModbusStringEncoding encoding)
    {
        if (encoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian &&
            (byteLength & 1) != 0)
            throw new ArgumentException("UTF-16 string framing contains an odd encoded byte length.");
    }

    private static ReadOnlySpan<byte> TrimSpacePadding(ReadOnlySpan<byte> value, ModbusStringEncoding encoding)
    {
        if (encoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian)
        {
            var little = encoding == ModbusStringEncoding.Utf16LittleEndian;
            var end = value.Length;
            while (end >= 2)
            {
                var unit = little
                    ? BinaryPrimitives.ReadUInt16LittleEndian(value[(end - 2)..end])
                    : BinaryPrimitives.ReadUInt16BigEndian(value[(end - 2)..end]);
                if (unit != 0x0020) break;
                end -= 2;
            }

            return value[..end];
        }

        var byteEnd = value.Length;
        while (byteEnd > 0 && value[byteEnd - 1] == 0x20) byteEnd--;
        return value[..byteEnd];
    }

    private static void FillSpacePadding(Span<byte> target, ModbusStringEncoding encoding)
    {
        if (encoding == ModbusStringEncoding.Utf16LittleEndian)
        {
            for (var i = 0; i < target.Length; i += 2) BinaryPrimitives.WriteUInt16LittleEndian(target[i..], 0x0020);
            return;
        }

        if (encoding == ModbusStringEncoding.Utf16BigEndian)
        {
            for (var i = 0; i < target.Length; i += 2) BinaryPrimitives.WriteUInt16BigEndian(target[i..], 0x0020);
            return;
        }

        target.Fill(0x20);
    }

    private static void EncodeBytes(
        ModbusRegisterDescriptor descriptor,
        ReadOnlySpan<byte> value,
        Span<byte> destination)
    {
        if (value.Length > descriptor.ByteLength)
            throw new ArgumentException(
                $"Byte value has {value.Length} bytes, but ByteLength is {descriptor.ByteLength}.");

        var target = destination[..descriptor.ByteLength];
        target.Clear();
        value.CopyTo(target);
    }

    private static void EncodeDateTime(
        ModbusRegisterDescriptor descriptor,
        DateTime value,
        Span<byte> destination)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        var instant = new DateTimeOffset(utc);
        switch (descriptor.TemporalFormat)
        {
            case ModbusTemporalFormat.UnixSecondsInt32:
                BinaryPrimitives.WriteInt32BigEndian(
                    destination,
                    checked((int)instant.ToUnixTimeSeconds()));
                return;

            case ModbusTemporalFormat.UnixSecondsUInt32:
                BinaryPrimitives.WriteUInt32BigEndian(
                    destination,
                    checked((uint)instant.ToUnixTimeSeconds()));
                return;

            case ModbusTemporalFormat.UnixSecondsInt64:
                BinaryPrimitives.WriteInt64BigEndian(destination, instant.ToUnixTimeSeconds());
                return;

            case ModbusTemporalFormat.UnixMillisecondsInt64:
                BinaryPrimitives.WriteInt64BigEndian(destination, instant.ToUnixTimeMilliseconds());
                return;

            case ModbusTemporalFormat.DotNetTicksInt64:
                BinaryPrimitives.WriteInt64BigEndian(destination, utc.Ticks);
                return;

            default:
                throw new ArgumentException("Unsupported DateTime temporal format.");
        }
    }

    private static void EncodeDuration(
        ModbusRegisterDescriptor descriptor,
        TimeSpan value,
        Span<byte> destination)
    {
        switch (descriptor.TemporalFormat)
        {
            case ModbusTemporalFormat.DurationMillisecondsInt32:
                EnsureWholeMilliseconds(value);
                BinaryPrimitives.WriteInt32BigEndian(
                    destination,
                    checked((int)(value.Ticks / TimeSpan.TicksPerMillisecond)));
                return;

            case ModbusTemporalFormat.DurationMillisecondsInt64:
                EnsureWholeMilliseconds(value);
                BinaryPrimitives.WriteInt64BigEndian(
                    destination,
                    value.Ticks / TimeSpan.TicksPerMillisecond);
                return;

            case ModbusTemporalFormat.DurationTicksInt64:
                BinaryPrimitives.WriteInt64BigEndian(destination, value.Ticks);
                return;

            default:
                throw new ArgumentException("Unsupported Duration temporal format.");
        }
    }

    private static void EnsureWholeMilliseconds(TimeSpan value)
    {
        if (value.Ticks % TimeSpan.TicksPerMillisecond != 0)
            throw new ArgumentException("Duration contains sub-millisecond precision.");
    }

    private static ReadOnlySpan<byte> SliceAtNullTerminator(
        ReadOnlySpan<byte> bytes,
        ModbusStringEncoding encoding)
    {
        if (encoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian)
        {
            for (var i = 0; i + 1 < bytes.Length; i += 2)
                if (bytes[i] == 0 && bytes[i + 1] == 0)
                    return bytes[..i];

            return bytes;
        }

        var index = bytes.IndexOf((byte)0);
        return index >= 0 ? bytes[..index] : bytes;
    }

    private static ReadOnlySpan<byte> TrimNullPadding(
        ReadOnlySpan<byte> bytes,
        ModbusStringEncoding encoding)
    {
        if (encoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian)
        {
            while (bytes.Length >= 2 && bytes[^1] == 0 && bytes[^2] == 0)
                bytes = bytes[..^2];

            return bytes;
        }

        while (!bytes.IsEmpty && bytes[^1] == 0)
            bytes = bytes[..^1];

        return bytes;
    }

    private static int GetNullTerminatorWidth(ModbusStringEncoding encoding)
    {
        return encoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian
            ? 2
            : 1;
    }

    private static void ValidateAsciiChars(ReadOnlySpan<char> chars)
    {
        for (var i = 0; i < chars.Length; i++)
            if (chars[i] > 0x7F)
                throw new EncoderFallbackException(
                    $"Character U+{(int)chars[i]:X4} cannot be encoded as ASCII.");
    }

    private static void ValidateAsciiBytes(ReadOnlySpan<byte> bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
            if (bytes[i] > 0x7F)
                throw new DecoderFallbackException($"Byte 0x{bytes[i]:X2} is not valid ASCII.");
    }

    private static Encoding GetEncoding(ModbusStringEncoding encoding)
    {
        return encoding switch
        {
            ModbusStringEncoding.Ascii => Encoding.ASCII,
            ModbusStringEncoding.Utf8 => sUtf8Strict,
            ModbusStringEncoding.Utf16LittleEndian => sUtf16LeStrict,
            ModbusStringEncoding.Utf16BigEndian => sUtf16BeStrict,
            _ => throw new ArgumentOutOfRangeException(nameof(encoding))
        };
    }


    private static DriverError? ValidateByteEncodingPolicy(
        ModbusRegisterDescriptor descriptor,
        bool baselineIsEmpty)
    {
        if (descriptor.DataType is not (IndustrialDataType.Int8 or IndustrialDataType.UInt8 or
            IndustrialDataType.Char8 or IndustrialDataType.BitString8))
            return null;

        return descriptor.ByteWriteBehavior switch
        {
            ModbusByteWriteBehavior.Reject => new DriverError(
                DriverErrorKind.Configuration,
                "MODBUS_BYTE_WRITE",
                "Int8/UInt8 encoding requires an explicit byte write behavior."),

            ModbusByteWriteBehavior.PreserveOtherLane when baselineIsEmpty => new DriverError(
                DriverErrorKind.Configuration,
                "MODBUS_BYTE_BASELINE",
                "PreserveOtherLane requires the current physical register image as a baseline."),

            _ => null
        };
    }

    private static DriverResult<DriverValue> Success(DriverValue value)
    {
        return DriverResult<DriverValue>.Success(value);
    }

    private static DriverResult<DriverValue> Failure(string code, string message)
    {
        return DriverResult<DriverValue>.Failure(new DriverError(DriverErrorKind.Data, code, message));
    }

    private static DriverResult<int> FailureCount(string code, string message)
    {
        return DriverResult<int>.Failure(new DriverError(DriverErrorKind.Data, code, message));
    }
}
