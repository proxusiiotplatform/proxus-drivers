using System.Buffers.Binary;
using System.Text;

namespace Proxus.Drivers.OpcUa;

internal ref struct UaBinaryReader
{
    private readonly ReadOnlySpan<byte> _buffer;

    public UaBinaryReader(ReadOnlySpan<byte> buffer)
    {
        _buffer = buffer;
        Position = 0;
    }

    public int Position { get; private set; }

    public readonly int Remaining => _buffer.Length - Position;

    public byte ReadByte()
    {
        Require(1);
        return _buffer[Position++];
    }

    public sbyte ReadSByte()
    {
        return unchecked((sbyte)ReadByte());
    }

    public bool ReadBoolean()
    {
        return ReadByte() != 0;
    }

    public short ReadInt16()
    {
        Require(2);
        var v = BinaryPrimitives.ReadInt16LittleEndian(_buffer[Position..]);
        Position += 2;
        return v;
    }

    public ushort ReadUInt16()
    {
        Require(2);
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_buffer[Position..]);
        Position += 2;
        return v;
    }

    public int ReadInt32()
    {
        Require(4);
        var v = BinaryPrimitives.ReadInt32LittleEndian(_buffer[Position..]);
        Position += 4;
        return v;
    }

    public uint ReadUInt32()
    {
        Require(4);
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_buffer[Position..]);
        Position += 4;
        return v;
    }

    public long ReadInt64()
    {
        Require(8);
        var v = BinaryPrimitives.ReadInt64LittleEndian(_buffer[Position..]);
        Position += 8;
        return v;
    }

    public ulong ReadUInt64()
    {
        Require(8);
        var v = BinaryPrimitives.ReadUInt64LittleEndian(_buffer[Position..]);
        Position += 8;
        return v;
    }

    public float ReadFloat()
    {
        return BitConverter.UInt32BitsToSingle(ReadUInt32());
    }

    public double ReadDouble()
    {
        return BitConverter.UInt64BitsToDouble(ReadUInt64());
    }

    public DateTime ReadDateTime()
    {
        var value = ReadInt64();
        if (value <= 0) return DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);
        try
        {
            return DateTime.FromFileTimeUtc(value);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException("Invalid OPC UA DateTime FILETIME value.", ex);
        }
    }

    /// <summary>
    ///     Reads an array length (OPC 10000-6 5.2.5: Int32, -1 means a null array) and rejects a length the
    ///     remaining bytes cannot hold at <paramref name="minimumElementBytes" /> bytes per element, so no
    ///     decoder allocates from an untrusted count.
    /// </summary>
    public int ReadArrayLength(string field, int minimumElementBytes = 1)
    {
        var length = ReadInt32();
        if (length < -1) throw new InvalidDataException($"{field} length {length} is invalid.");
        if (length > 0 && length > Remaining / Math.Max(1, minimumElementBytes))
            throw new InvalidDataException(
                $"{field} declares {length} elements but only {Remaining} bytes remain in the message.");
        return length;
    }

    public Guid ReadGuid()
    {
        Require(16);
        var value = new Guid(_buffer.Slice(Position, 16));
        Position += 16;
        return value;
    }

    public string? ReadString()
    {
        var length = ReadInt32();
        if (length == -1) return null;
        if (length < -1) throw new InvalidDataException("OPC UA string length cannot be less than -1.");
        Require(length);
        var value = Encoding.UTF8.GetString(_buffer.Slice(Position, length));
        Position += length;
        return value;
    }

    public byte[]? ReadByteString()
    {
        var length = ReadInt32();
        if (length == -1) return null;
        if (length < -1) throw new InvalidDataException("OPC UA ByteString length cannot be less than -1.");
        Require(length);
        var value = _buffer.Slice(Position, length).ToArray();
        Position += length;
        return value;
    }

    public IndustrialNodeId ReadNodeId(out byte encodingFlags)
    {
        var encoding = ReadByte();
        encodingFlags = (byte)(encoding & 0xC0);
        return (encoding & 0x3F) switch
        {
            0x00 => IndustrialNodeId.Numeric(0, ReadByte()),
            0x01 => IndustrialNodeId.Numeric(ReadByte(), ReadUInt16()),
            0x02 => IndustrialNodeId.Numeric(ReadUInt16(), ReadUInt32()),
            0x03 => IndustrialNodeId.String(ReadUInt16(), ReadString() ?? string.Empty),
            0x04 => IndustrialNodeId.Guid(ReadUInt16(), ReadGuid()),
            0x05 => IndustrialNodeId.Opaque(ReadUInt16(), ReadByteString() ?? []),
            _ => throw new InvalidDataException($"Unsupported OPC UA NodeId encoding 0x{encoding & 0x3F:X2}.")
        };
    }

    public IndustrialNodeId ReadNodeId()
    {
        return ReadNodeId(out _);
    }

    public IndustrialExpandedNodeId ReadExpandedNodeId()
    {
        var nodeId = ReadNodeId(out var flags);
        var namespaceUri = (flags & 0x80) != 0 ? ReadString() : null;
        var serverIndex = (flags & 0x40) != 0 ? ReadUInt32() : 0;
        return new IndustrialExpandedNodeId(nodeId, namespaceUri, serverIndex);
    }

    public IndustrialQualifiedName ReadQualifiedName()
    {
        return new IndustrialQualifiedName(ReadUInt16(), ReadString() ?? string.Empty);
    }

    public IndustrialLocalizedText ReadLocalizedText()
    {
        var mask = ReadByte();
        var locale = (mask & 0x01) != 0 ? ReadString() : null;
        var text = (mask & 0x02) != 0 ? ReadString() : string.Empty;
        if ((mask & 0xFC) != 0) throw new InvalidDataException($"Invalid OPC UA LocalizedText mask 0x{mask:X2}.");
        return new IndustrialLocalizedText(locale, text ?? string.Empty);
    }

    public DriverExtensionObject ReadExtensionObject()
    {
        var typeId = ReadNodeId();
        var encoding = ReadByte();
        if (encoding == 0)
            return new DriverExtensionObject(new IndustrialExpandedNodeId(typeId), DriverExtensionObjectEncoding.None,
                ReadOnlySpan<byte>.Empty);
        var body = ReadByteString() ?? [];
        return encoding switch
        {
            1 => new DriverExtensionObject(new IndustrialExpandedNodeId(typeId), DriverExtensionObjectEncoding.Binary,
                body),
            2 => new DriverExtensionObject(new IndustrialExpandedNodeId(typeId), DriverExtensionObjectEncoding.Xml,
                body),
            _ => throw new InvalidDataException($"Unsupported OPC UA ExtensionObject encoding 0x{encoding:X2}.")
        };
    }

    private readonly void Require(int count)
    {
        if (count < 0 || count > Remaining)
            throw new EndOfStreamException(
                $"OPC UA Binary payload truncated at offset {Position}; need {count} bytes, have {Remaining}.");
    }
}
