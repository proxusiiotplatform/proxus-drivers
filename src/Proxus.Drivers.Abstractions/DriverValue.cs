using System.Buffers.Binary;
using System.Globalization;

namespace Proxus.Drivers;

/// <summary>
///     Tagged industrial scalar union. All fixed-width numeric, temporal, Guid and bit-string
///     values are stored inline without boxing. String and Bytes retain their natural managed
///     references. The struct deliberately carries semantic type information so a WORD is not
///     silently treated as an unsigned engineering number and a Date is not flattened to Int32.
/// </summary>
public readonly struct DriverValue : IEquatable<DriverValue>
{
    private readonly ulong _lo;
    private readonly ulong _hi;
    private readonly object? _reference;

    private DriverValue(IndustrialDataType type, ulong lo, ulong hi = 0, object? reference = null)
    {
        Type = type;
        _lo = lo;
        _hi = hi;
        _reference = reference;
    }

    public IndustrialDataType Type { get; }

    public static DriverValue Null => new(IndustrialDataType.Null, 0);
    public bool IsNull => Type == IndustrialDataType.Null;

    public static DriverValue FromBoolean(bool value)
    {
        return new DriverValue(IndustrialDataType.Bool, value ? 1UL : 0UL);
    }

    public static DriverValue FromInt8(sbyte value)
    {
        return new DriverValue(IndustrialDataType.Int8, unchecked((ulong)value));
    }

    public static DriverValue FromUInt8(byte value)
    {
        return new DriverValue(IndustrialDataType.UInt8, value);
    }

    public static DriverValue FromInt16(short value)
    {
        return new DriverValue(IndustrialDataType.Int16, unchecked((ulong)value));
    }

    public static DriverValue FromUInt16(ushort value)
    {
        return new DriverValue(IndustrialDataType.UInt16, value);
    }

    public static DriverValue FromInt32(int value)
    {
        return new DriverValue(IndustrialDataType.Int32, unchecked((ulong)value));
    }

    public static DriverValue FromUInt32(uint value)
    {
        return new DriverValue(IndustrialDataType.UInt32, value);
    }

    public static DriverValue FromInt64(long value)
    {
        return new DriverValue(IndustrialDataType.Int64, unchecked((ulong)value));
    }

    public static DriverValue FromUInt64(ulong value)
    {
        return new DriverValue(IndustrialDataType.UInt64, value);
    }

    public static DriverValue FromInt128(Int128 value)
    {
        var raw = unchecked((UInt128)value);
        return new DriverValue(IndustrialDataType.Int128, (ulong)raw, (ulong)(raw >> 64));
    }

    public static DriverValue FromUInt128(UInt128 value)
    {
        return new DriverValue(IndustrialDataType.UInt128, (ulong)value, (ulong)(value >> 64));
    }

    public static DriverValue FromFloat16(Half value)
    {
        return new DriverValue(IndustrialDataType.Float16, BitConverter.HalfToUInt16Bits(value));
    }

    public static DriverValue FromFloat32(float value)
    {
        return new DriverValue(IndustrialDataType.Float32, BitConverter.SingleToUInt32Bits(value));
    }

    public static DriverValue FromFloat64(double value)
    {
        return new DriverValue(IndustrialDataType.Float64, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    }

    public static DriverValue FromDecimal(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        decimal.GetBits(value, bits);
        var lo = (uint)bits[0] | ((ulong)(uint)bits[1] << 32);
        var hi = (uint)bits[2] | ((ulong)(uint)bits[3] << 32);
        return new DriverValue(IndustrialDataType.Decimal, lo, hi);
    }

    public static DriverValue FromChar8(char value)
    {
        if (value > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Char8 requires a code unit in the range U+0000..U+00FF.");
        return new DriverValue(IndustrialDataType.Char8, value);
    }

    public static DriverValue FromChar16(char value)
    {
        return new DriverValue(IndustrialDataType.Char16, value);
    }

    public static DriverValue FromString(string value)
    {
        return new DriverValue(IndustrialDataType.String, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Copies the supplied bytes so the value remains immutable to the caller.</summary>
    public static DriverValue FromBytes(ReadOnlySpan<byte> value)
    {
        return new DriverValue(IndustrialDataType.Bytes, 0, reference: value.ToArray());
    }

    /// <summary>Transfers ownership of an existing array. The caller must not mutate it afterwards.</summary>
    public static DriverValue FromOwnedBytes(byte[] value)
    {
        return new DriverValue(IndustrialDataType.Bytes, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromGuid(Guid value)
    {
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes, true, out var written) || written != 16)
            throw new InvalidOperationException("Unable to serialize Guid.");
        return new DriverValue(
            IndustrialDataType.Guid,
            BinaryPrimitives.ReadUInt64BigEndian(bytes),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));
    }

    public static DriverValue FromDate(DateOnly value)
    {
        return new DriverValue(IndustrialDataType.Date, unchecked((uint)value.DayNumber));
    }

    public static DriverValue FromTimeOfDay(TimeOnly value)
    {
        return new DriverValue(IndustrialDataType.TimeOfDay, unchecked((ulong)value.Ticks));
    }

    public static DriverValue FromDateTime(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return new DriverValue(IndustrialDataType.DateTime, unchecked((ulong)utc.Ticks));
    }

    public static DriverValue FromDuration(TimeSpan value)
    {
        return new DriverValue(IndustrialDataType.Duration, unchecked((ulong)value.Ticks));
    }

    public static DriverValue FromBitString8(byte value)
    {
        return new DriverValue(IndustrialDataType.BitString8, value);
    }

    public static DriverValue FromBitString16(ushort value)
    {
        return new DriverValue(IndustrialDataType.BitString16, value);
    }

    public static DriverValue FromBitString32(uint value)
    {
        return new DriverValue(IndustrialDataType.BitString32, value);
    }

    public static DriverValue FromBitString64(ulong value)
    {
        return new DriverValue(IndustrialDataType.BitString64, value);
    }

    public static DriverValue FromStatusCode(uint value)
    {
        return new DriverValue(IndustrialDataType.StatusCode, value);
    }

    public static DriverValue FromNodeId(IndustrialNodeId value)
    {
        return new DriverValue(IndustrialDataType.NodeId, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromExpandedNodeId(IndustrialExpandedNodeId value)
    {
        return new DriverValue(IndustrialDataType.ExpandedNodeId, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromQualifiedName(IndustrialQualifiedName value)
    {
        return new DriverValue(IndustrialDataType.QualifiedName, 0, reference: new Box<IndustrialQualifiedName>(value));
    }

    public static DriverValue FromLocalizedText(IndustrialLocalizedText value)
    {
        return new DriverValue(IndustrialDataType.LocalizedText, 0, reference: new Box<IndustrialLocalizedText>(value));
    }

    public static DriverValue FromArray(DriverDynamicArray value)
    {
        return new DriverValue(IndustrialDataType.Array, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromRecord(DriverRecordValue value)
    {
        return new DriverValue(IndustrialDataType.Record, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromVariant(DriverValue value)
    {
        return new DriverValue(IndustrialDataType.Variant, 0, reference: new DriverVariantValue(value));
    }

    public static DriverValue FromExtensionObject(DriverExtensionObject value)
    {
        return new DriverValue(IndustrialDataType.ExtensionObject, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static DriverValue FromXmlElement(string value)
    {
        return new DriverValue(IndustrialDataType.XmlElement, 0,
            reference: value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Creates a signed symbolic enumeration value. The protocol descriptor selects the storage width.</summary>
    public static DriverValue FromEnumeration(long value)
    {
        return new DriverValue(IndustrialDataType.Enumeration, unchecked((ulong)value));
    }

    /// <summary>Creates an unsigned symbolic enumeration value. The protocol descriptor selects the storage width.</summary>
    public static DriverValue FromUnsignedEnumeration(ulong value)
    {
        return new DriverValue(IndustrialDataType.Enumeration, value, 1);
    }

    public bool AsBoolean()
    {
        return Require(IndustrialDataType.Bool, _lo != 0);
    }

    public sbyte AsInt8()
    {
        return Require(IndustrialDataType.Int8, unchecked((sbyte)(long)_lo));
    }

    public byte AsUInt8()
    {
        return Require(IndustrialDataType.UInt8, checked((byte)_lo));
    }

    public short AsInt16()
    {
        return Require(IndustrialDataType.Int16, unchecked((short)(long)_lo));
    }

    public ushort AsUInt16()
    {
        return Require(IndustrialDataType.UInt16, checked((ushort)_lo));
    }

    public int AsInt32()
    {
        return Require(IndustrialDataType.Int32, unchecked((int)(long)_lo));
    }

    public uint AsUInt32()
    {
        return Require(IndustrialDataType.UInt32, checked((uint)_lo));
    }

    public long AsInt64()
    {
        return Require(IndustrialDataType.Int64, unchecked((long)_lo));
    }

    public ulong AsUInt64()
    {
        return Require(IndustrialDataType.UInt64, _lo);
    }

    public Int128 AsInt128()
    {
        RequireType(IndustrialDataType.Int128);
        return unchecked((Int128)(((UInt128)_hi << 64) | _lo));
    }

    public UInt128 AsUInt128()
    {
        RequireType(IndustrialDataType.UInt128);
        return ((UInt128)_hi << 64) | _lo;
    }

    public Half AsFloat16()
    {
        return Require(IndustrialDataType.Float16, BitConverter.UInt16BitsToHalf(checked((ushort)_lo)));
    }

    public float AsFloat32()
    {
        return Require(IndustrialDataType.Float32, BitConverter.UInt32BitsToSingle(checked((uint)_lo)));
    }

    public double AsFloat64()
    {
        return Require(IndustrialDataType.Float64, BitConverter.Int64BitsToDouble(unchecked((long)_lo)));
    }

    public decimal AsDecimal()
    {
        RequireType(IndustrialDataType.Decimal);
        Span<int> bits = stackalloc int[4];
        bits[0] = unchecked((int)(uint)_lo);
        bits[1] = unchecked((int)(uint)(_lo >> 32));
        bits[2] = unchecked((int)(uint)_hi);
        bits[3] = unchecked((int)(uint)(_hi >> 32));
        return new decimal(bits);
    }

    public char AsChar8()
    {
        return Require(IndustrialDataType.Char8, checked((char)_lo));
    }

    public char AsChar16()
    {
        return Require(IndustrialDataType.Char16, checked((char)_lo));
    }

    public string AsString()
    {
        return RequireReference<string>(IndustrialDataType.String);
    }

    public ReadOnlyMemory<byte> AsBytes()
    {
        return new ReadOnlyMemory<byte>(RequireReference<byte[]>(IndustrialDataType.Bytes));
    }

    public Guid AsGuid()
    {
        RequireType(IndustrialDataType.Guid);
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, _lo);
        BinaryPrimitives.WriteUInt64BigEndian(bytes[8..], _hi);
        return new Guid(bytes, true);
    }

    public DateOnly AsDate()
    {
        return Require(IndustrialDataType.Date, DateOnly.FromDayNumber(unchecked((int)(uint)_lo)));
    }

    public TimeOnly AsTimeOfDay()
    {
        return Require(IndustrialDataType.TimeOfDay, new TimeOnly(unchecked((long)_lo)));
    }

    public DateTime AsDateTime()
    {
        return Require(IndustrialDataType.DateTime, new DateTime(unchecked((long)_lo), DateTimeKind.Utc));
    }

    public TimeSpan AsDuration()
    {
        return Require(IndustrialDataType.Duration, new TimeSpan(unchecked((long)_lo)));
    }

    public byte AsBitString8()
    {
        return Require(IndustrialDataType.BitString8, checked((byte)_lo));
    }

    public ushort AsBitString16()
    {
        return Require(IndustrialDataType.BitString16, checked((ushort)_lo));
    }

    public uint AsBitString32()
    {
        return Require(IndustrialDataType.BitString32, checked((uint)_lo));
    }

    public ulong AsBitString64()
    {
        return Require(IndustrialDataType.BitString64, _lo);
    }

    public uint AsStatusCode()
    {
        return Require(IndustrialDataType.StatusCode, checked((uint)_lo));
    }

    public IndustrialNodeId AsNodeId()
    {
        return RequireReference<IndustrialNodeId>(IndustrialDataType.NodeId);
    }

    public IndustrialExpandedNodeId AsExpandedNodeId()
    {
        return RequireReference<IndustrialExpandedNodeId>(IndustrialDataType.ExpandedNodeId);
    }

    public IndustrialQualifiedName AsQualifiedName()
    {
        return RequireReference<Box<IndustrialQualifiedName>>(IndustrialDataType.QualifiedName).Value;
    }

    public IndustrialLocalizedText AsLocalizedText()
    {
        return RequireReference<Box<IndustrialLocalizedText>>(IndustrialDataType.LocalizedText).Value;
    }

    public DriverDynamicArray AsArray()
    {
        return RequireReference<DriverDynamicArray>(IndustrialDataType.Array);
    }

    public DriverRecordValue AsRecord()
    {
        return RequireReference<DriverRecordValue>(IndustrialDataType.Record);
    }

    public DriverValue AsVariant()
    {
        return RequireReference<DriverVariantValue>(IndustrialDataType.Variant).Value;
    }

    public DriverExtensionObject AsExtensionObject()
    {
        return RequireReference<DriverExtensionObject>(IndustrialDataType.ExtensionObject);
    }

    public string AsXmlElement()
    {
        return RequireReference<string>(IndustrialDataType.XmlElement);
    }

    public bool IsEnumerationUnsigned
    {
        get
        {
            RequireType(IndustrialDataType.Enumeration);
            return _hi != 0;
        }
    }

    public long AsEnumerationInt64()
    {
        RequireType(IndustrialDataType.Enumeration);
        if (_hi != 0 && _lo > long.MaxValue)
            throw new OverflowException("Unsigned enumeration value does not fit Int64.");
        return _hi == 0 ? unchecked((long)_lo) : checked((long)_lo);
    }

    public ulong AsEnumerationUInt64()
    {
        RequireType(IndustrialDataType.Enumeration);
        if (_hi == 0 && unchecked((long)_lo) < 0)
            throw new OverflowException("Negative enumeration value does not fit UInt64.");
        return _hi != 0 ? _lo : checked((ulong)unchecked((long)_lo));
    }

    public string ToInvariantString()
    {
        return Type switch
        {
            IndustrialDataType.Bool => AsBoolean() ? "true" : "false",
            IndustrialDataType.Int8 => AsInt8().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.UInt8 => AsUInt8().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Int16 => AsInt16().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.UInt16 => AsUInt16().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Int32 => AsInt32().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.UInt32 => AsUInt32().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Int64 => AsInt64().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.UInt64 => AsUInt64().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Int128 => AsInt128().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.UInt128 => AsUInt128().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Float16 => AsFloat16().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Float32 => AsFloat32().ToString("R", CultureInfo.InvariantCulture),
            IndustrialDataType.Float64 => AsFloat64().ToString("R", CultureInfo.InvariantCulture),
            IndustrialDataType.Decimal => AsDecimal().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.Char8 => AsChar8().ToString(),
            IndustrialDataType.Char16 => AsChar16().ToString(),
            IndustrialDataType.String => AsString(),
            IndustrialDataType.Bytes => Convert.ToHexString(AsBytes().Span),
            IndustrialDataType.Guid => AsGuid().ToString("D"),
            IndustrialDataType.Date => AsDate().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IndustrialDataType.TimeOfDay => AsTimeOfDay().ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            IndustrialDataType.DateTime => AsDateTime().ToString("O", CultureInfo.InvariantCulture),
            IndustrialDataType.Duration => AsDuration().ToString("c", CultureInfo.InvariantCulture),
            IndustrialDataType.BitString8 => $"16#{AsBitString8():X2}",
            IndustrialDataType.BitString16 => $"16#{AsBitString16():X4}",
            IndustrialDataType.BitString32 => $"16#{AsBitString32():X8}",
            IndustrialDataType.BitString64 => $"16#{AsBitString64():X16}",
            IndustrialDataType.Enumeration => IsEnumerationUnsigned
                ? AsEnumerationUInt64().ToString(CultureInfo.InvariantCulture)
                : AsEnumerationInt64().ToString(CultureInfo.InvariantCulture),
            IndustrialDataType.StatusCode => $"0x{AsStatusCode():X8}",
            IndustrialDataType.NodeId => AsNodeId().ToString(),
            IndustrialDataType.ExpandedNodeId => AsExpandedNodeId().ToString(),
            IndustrialDataType.QualifiedName => AsQualifiedName().ToString(),
            IndustrialDataType.LocalizedText => AsLocalizedText().ToString(),
            IndustrialDataType.Null => "null",
            IndustrialDataType.Array => AsArray().ToString(),
            IndustrialDataType.Record => AsRecord().ToString(),
            IndustrialDataType.Variant => AsVariant().ToInvariantString(),
            IndustrialDataType.ExtensionObject => AsExtensionObject().ToString(),
            IndustrialDataType.XmlElement => AsXmlElement(),
            _ => string.Empty
        };
    }

    public override string ToString()
    {
        return ToInvariantString();
    }

    public bool Equals(DriverValue other)
    {
        if (Type != other.Type)
            return false;
        return Type switch
        {
            IndustrialDataType.String or IndustrialDataType.XmlElement => string.Equals(_reference as string,
                other._reference as string, StringComparison.Ordinal),
            IndustrialDataType.Bytes => _reference is byte[] left && other._reference is byte[] right &&
                                        left.AsSpan().SequenceEqual(right),
            IndustrialDataType.Decimal => AsDecimal() == other.AsDecimal(),
            IndustrialDataType.NodeId or IndustrialDataType.ExpandedNodeId => Equals(_reference, other._reference),
            IndustrialDataType.QualifiedName => AsQualifiedName().Equals(other.AsQualifiedName()),
            IndustrialDataType.LocalizedText => AsLocalizedText().Equals(other.AsLocalizedText()),
            IndustrialDataType.Array or IndustrialDataType.Record or IndustrialDataType.Variant
                or IndustrialDataType.ExtensionObject => Equals(_reference, other._reference),
            _ => _lo == other._lo && _hi == other._hi
        };
    }

    public override bool Equals(object? obj)
    {
        return obj is DriverValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        switch (Type)
        {
            case IndustrialDataType.String:
                hash.Add(_reference as string, StringComparer.Ordinal);
                break;
            case IndustrialDataType.Bytes when _reference is byte[] bytes:
                for (var i = 0; i < bytes.Length; i++) hash.Add(bytes[i]);
                break;
            case IndustrialDataType.Decimal:
                hash.Add(AsDecimal());
                break;
            case IndustrialDataType.NodeId:
            case IndustrialDataType.ExpandedNodeId:
                hash.Add(_reference);
                break;
            case IndustrialDataType.QualifiedName:
                hash.Add(AsQualifiedName());
                break;
            case IndustrialDataType.LocalizedText:
                hash.Add(AsLocalizedText());
                break;
            case IndustrialDataType.Array:
            case IndustrialDataType.Record:
            case IndustrialDataType.Variant:
            case IndustrialDataType.ExtensionObject:
                hash.Add(_reference);
                break;
            default:
                hash.Add(_lo);
                hash.Add(_hi);
                break;
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(DriverValue left, DriverValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(DriverValue left, DriverValue right)
    {
        return !left.Equals(right);
    }

    private T Require<T>(IndustrialDataType expected, T value)
    {
        RequireType(expected);
        return value;
    }

    private void RequireType(IndustrialDataType expected)
    {
        if (Type != expected)
            throw new InvalidOperationException($"Driver value is {Type}, not {expected}.");
    }

    private T RequireReference<T>(IndustrialDataType expected) where T : class
    {
        if (Type != expected || _reference is not T value)
            throw new InvalidOperationException($"Driver value is {Type}, not {expected}.");
        return value;
    }

    private sealed class Box<T>(T value)
    {
        public T Value { get; } = value;
    }
}
