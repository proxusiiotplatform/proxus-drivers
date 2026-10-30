using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace Proxus.Drivers.OpcUa;

/// <summary>Pooled OPC UA Binary encoder. No reflection, streams or BinaryWriter allocations.</summary>
internal sealed class UaBinaryWriter : IDisposable
{
    private byte[] _buffer;
    private bool _disposed;

    public UaBinaryWriter(int initialCapacity = 512)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 64));
    }

    public int Position { get; private set; }

    public ReadOnlySpan<byte> WrittenSpan => _buffer.AsSpan(0, Position);
    public ReadOnlyMemory<byte> WrittenMemory => _buffer.AsMemory(0, Position);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var buffer = _buffer;
        _buffer = [];
        Position = 0;
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public void WriteByte(byte value)
    {
        Ensure(1);
        _buffer[Position++] = value;
    }

    public void WriteSByte(sbyte value)
    {
        WriteByte(unchecked((byte)value));
    }

    public void WriteBoolean(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteInt16(short value)
    {
        Ensure(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(Position, 2), value);
        Position += 2;
    }

    public void WriteUInt16(ushort value)
    {
        Ensure(2);
        BinaryPrimitives.WriteUInt16LittleEndian(_buffer.AsSpan(Position, 2), value);
        Position += 2;
    }

    public void WriteInt32(int value)
    {
        Ensure(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(Position, 4), value);
        Position += 4;
    }

    public void WriteUInt32(uint value)
    {
        Ensure(4);
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(Position, 4), value);
        Position += 4;
    }

    public void WriteInt64(long value)
    {
        Ensure(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(Position, 8), value);
        Position += 8;
    }

    public void WriteUInt64(ulong value)
    {
        Ensure(8);
        BinaryPrimitives.WriteUInt64LittleEndian(_buffer.AsSpan(Position, 8), value);
        Position += 8;
    }

    public void WriteFloat(float value)
    {
        WriteUInt32(BitConverter.SingleToUInt32Bits(value));
    }

    public void WriteDouble(double value)
    {
        WriteUInt64(BitConverter.DoubleToUInt64Bits(value));
    }

    public void WriteDateTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        var fileTime = utc < DateTime.FromFileTimeUtc(0) ? 0L : utc.ToFileTimeUtc();
        WriteInt64(fileTime);
    }

    public void WriteGuid(Guid value)
    {
        Ensure(16);
        value.TryWriteBytes(_buffer.AsSpan(Position, 16));
        Position += 16;
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            WriteInt32(-1);
            return;
        }

        var byteCount = Encoding.UTF8.GetByteCount(value);
        WriteInt32(byteCount);
        Ensure(byteCount);
        Encoding.UTF8.GetBytes(value, _buffer.AsSpan(Position, byteCount));
        Position += byteCount;
    }

    public void WriteByteString(ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        WriteBytes(value);
    }

    public void WriteNullByteString()
    {
        WriteInt32(-1);
    }

    public void WriteBytes(ReadOnlySpan<byte> value)
    {
        Ensure(value.Length);
        value.CopyTo(_buffer.AsSpan(Position));
        Position += value.Length;
    }

    public void WriteNodeId(IndustrialNodeId value, byte extraEncodingFlags = 0)
    {
        switch (value.Kind)
        {
            case IndustrialNodeIdKind.Numeric:
                if (value.NumericIdentifier > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(value),
                        "OPC UA numeric NodeId identifiers are UInt32.");
                var numeric = (uint)value.NumericIdentifier;
                if (value.NamespaceIndex == 0 && numeric <= byte.MaxValue)
                {
                    WriteByte((byte)(0x00 | extraEncodingFlags));
                    WriteByte((byte)numeric);
                }
                else if (value.NamespaceIndex <= byte.MaxValue && numeric <= ushort.MaxValue)
                {
                    WriteByte((byte)(0x01 | extraEncodingFlags));
                    WriteByte((byte)value.NamespaceIndex);
                    WriteUInt16((ushort)numeric);
                }
                else
                {
                    WriteByte((byte)(0x02 | extraEncodingFlags));
                    WriteUInt16(value.NamespaceIndex);
                    WriteUInt32(numeric);
                }

                break;
            case IndustrialNodeIdKind.String:
                WriteByte((byte)(0x03 | extraEncodingFlags));
                WriteUInt16(value.NamespaceIndex);
                WriteString(value.StringIdentifier);
                break;
            case IndustrialNodeIdKind.Guid:
                WriteByte((byte)(0x04 | extraEncodingFlags));
                WriteUInt16(value.NamespaceIndex);
                WriteGuid(value.GuidIdentifier);
                break;
            case IndustrialNodeIdKind.Opaque:
                WriteByte((byte)(0x05 | extraEncodingFlags));
                WriteUInt16(value.NamespaceIndex);
                WriteByteString(value.OpaqueIdentifier.Span);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(value));
        }
    }

    public void WriteExpandedNodeId(IndustrialExpandedNodeId value)
    {
        byte flags = 0;
        if (!string.IsNullOrEmpty(value.NamespaceUri)) flags |= 0x80;
        if (value.ServerIndex != 0) flags |= 0x40;
        WriteNodeId(value.NodeId, flags);
        if ((flags & 0x80) != 0) WriteString(value.NamespaceUri);
        if ((flags & 0x40) != 0) WriteUInt32(value.ServerIndex);
    }

    public void WriteQualifiedName(IndustrialQualifiedName value)
    {
        WriteUInt16(value.NamespaceIndex);
        WriteString(value.Name);
    }

    public void WriteLocalizedText(IndustrialLocalizedText value)
    {
        byte mask = 0;
        if (value.Locale is not null) mask |= 0x01;
        if (value.Text is not null) mask |= 0x02;
        WriteByte(mask);
        if ((mask & 0x01) != 0) WriteString(value.Locale);
        if ((mask & 0x02) != 0) WriteString(value.Text);
    }

    public void WriteExtensionObject(DriverExtensionObject? value)
    {
        if (value is null || value.Encoding == DriverExtensionObjectEncoding.None)
        {
            WriteNodeId(IndustrialNodeId.Numeric(0, 0));
            WriteByte(0);
            return;
        }

        WriteNodeId(value.TypeId.NodeId);
        switch (value.Encoding)
        {
            case DriverExtensionObjectEncoding.Binary:
                WriteByte(1);
                WriteByteString(value.EncodedBody.Span);
                break;
            case DriverExtensionObjectEncoding.Xml:
                WriteByte(2);
                WriteByteString(value.EncodedBody.Span);
                break;
            default:
                throw new NotSupportedException("OPC UA Binary ExtensionObject supports Binary or XML bodies only.");
        }
    }

    public void PatchUInt32(int offset, uint value)
    {
        if ((uint)offset > (uint)(Position - 4)) throw new ArgumentOutOfRangeException(nameof(offset));
        BinaryPrimitives.WriteUInt32LittleEndian(_buffer.AsSpan(offset, 4), value);
    }

    public byte[] ToArray()
    {
        return [.. WrittenSpan];
    }

    public void ReplaceWritten(ReadOnlySpan<byte> value)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UaBinaryWriter));
        Position = 0;
        WriteBytes(value);
    }

    private void Ensure(int count)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(UaBinaryWriter));
        var required = checked(Position + count);
        if (required <= _buffer.Length) return;
        var next = ArrayPool<byte>.Shared.Rent(Math.Max(required, _buffer.Length * 2));
        _buffer.AsSpan(0, Position).CopyTo(next);
        var old = _buffer;
        _buffer = next;
        ArrayPool<byte>.Shared.Return(old);
    }
}
