namespace Proxus.Drivers.Modbus;

/// <summary>
///     Modbus ASCII framing (Modbus over Serial Line V1.02 §2.5.2): a colon, the Unit ID, PDU and LRC as
///     two hexadecimal characters per byte, then CR LF. The LRC is the two's complement of the 8-bit sum of
///     the Unit ID and PDU bytes. Requests are sent in upper case; responses are accepted in either case.
/// </summary>
internal static class ModbusAscii
{
    public const byte Start = (byte)':';
    public const byte CarriageReturn = (byte)'\r';
    public const byte LineFeed = (byte)'\n';
    public const int LrcLength = 1;

    /// <summary>Unit ID, PDU and LRC: the RTU limit of 256 bytes less the two CRC bytes plus one LRC byte.</summary>
    public const int MaximumBinaryLength = ModbusRtu.MaximumFrameLength - ModbusRtu.CrcLength + LrcLength;

    /// <summary>Colon, two characters per binary byte, CR LF: 513 characters, as the specification states.</summary>
    public const int MaximumFrameLength = 1 + 2 * MaximumBinaryLength + 2;

    public static byte Lrc(ReadOnlySpan<byte> data)
    {
        byte sum = 0;
        foreach (var value in data)
            sum = unchecked((byte)(sum + value));
        return unchecked((byte)-sum);
    }

    /// <summary>Writes ":" + hex(unit, PDU, LRC) + CR LF and returns the number of characters.</summary>
    public static int WriteFrame(Span<byte> destination, byte unitId, ReadOnlySpan<byte> pdu)
    {
        var binary = 1 + pdu.Length + LrcLength;
        var total = 1 + 2 * binary + 2;
        if (pdu.IsEmpty || binary > MaximumBinaryLength || destination.Length < total)
            throw new ArgumentException("Invalid Modbus ASCII PDU length.", nameof(pdu));

        // LRC(unit, PDU) = -(unit + sum(PDU)) = LRC(PDU) - unit.
        var lrc = unchecked((byte)(Lrc(pdu) - unitId));
        destination[0] = Start;
        var position = 1;
        WriteHex(destination, ref position, unitId);
        foreach (var value in pdu)
            WriteHex(destination, ref position, value);
        WriteHex(destination, ref position, lrc);
        destination[position++] = CarriageReturn;
        destination[position++] = LineFeed;
        return position;
    }

    /// <summary>Decodes pairs of hexadecimal characters; false when a character is not a hexadecimal digit.</summary>
    public static bool TryDecodeHex(ReadOnlySpan<byte> characters, Span<byte> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            var high = Nibble(characters[2 * i]);
            var low = Nibble(characters[2 * i + 1]);
            if ((high | low) < 0)
                return false;
            destination[i] = (byte)(high << 4 | low);
        }

        return true;
    }

    /// <summary>True when the last byte of Unit ID, PDU and LRC is the LRC of the bytes before it.</summary>
    public static bool HasValidLrc(ReadOnlySpan<byte> binary) =>
        binary.Length >= 1 + 1 + LrcLength && binary[^1] == Lrc(binary[..^1]);

    private static void WriteHex(Span<byte> destination, ref int position, byte value)
    {
        destination[position++] = (byte)"0123456789ABCDEF"[value >> 4];
        destination[position++] = (byte)"0123456789ABCDEF"[value & 0xF];
    }

    private static int Nibble(byte character) => character switch
    {
        >= (byte)'0' and <= (byte)'9' => character - '0',
        >= (byte)'A' and <= (byte)'F' => character - 'A' + 10,
        >= (byte)'a' and <= (byte)'f' => character - 'a' + 10,
        _ => -1
    };
}
