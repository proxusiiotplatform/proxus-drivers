using System.Buffers.Binary;

namespace Proxus.Drivers.Modbus;

/// <summary>
///     Modbus RTU framing (Modbus over Serial Line V1.02 §2.5): Unit ID, PDU and a CRC-16 sent low byte
///     first. Response boundaries are derived from the function code and its length fields rather than
///     from line silence, which serial gateways and operating-system buffers do not preserve.
/// </summary>
internal static class ModbusRtu
{
    public const int MaximumFrameLength = 256;
    public const int CrcLength = 2;
    public const byte MaximumUnitId = 247;

    /// <summary>Returned by <see cref="ResponseLength" /> when the function has no length rule.</summary>
    public const int UnknownLength = -1;

    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ 0xA001) : (ushort)(crc >> 1);
        }

        return crc;
    }

    public static int WriteFrame(Span<byte> destination, byte unitId, ReadOnlySpan<byte> pdu)
    {
        var total = 1 + pdu.Length + CrcLength;
        if (pdu.IsEmpty || total > MaximumFrameLength || destination.Length < total)
            throw new ArgumentException("Invalid Modbus RTU PDU length.", nameof(pdu));

        destination[0] = unitId;
        pdu.CopyTo(destination[1..]);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[(1 + pdu.Length)..],
            Crc16(destination[..(1 + pdu.Length)]));
        return total;
    }

    public static bool HasValidCrc(ReadOnlySpan<byte> frame)
    {
        return frame.Length >= 1 + 1 + CrcLength &&
               BinaryPrimitives.ReadUInt16LittleEndian(frame[^CrcLength..]) == Crc16(frame[..^CrcLength]);
    }

    /// <summary>
    ///     Computes the total response frame length (Unit ID through CRC) from the bytes received so far.
    ///     Returns the length when it is known, 0 with <paramref name="required" /> set to the number of bytes
    ///     needed before it can be decided, or <see cref="UnknownLength" /> for functions without a rule.
    /// </summary>
    public static int ResponseLength(ReadOnlySpan<byte> head, out int required)
    {
        required = 2;
        if (head.Length < 2)
            return 0;

        var function = head[1];
        if ((function & 0x80) != 0)
            return 1 + 2 + CrcLength;

        switch (function)
        {
            case ModbusProtocol.ReadCoils:
            case ModbusProtocol.ReadDiscreteInputs:
            case ModbusProtocol.ReadHoldingRegisters:
            case ModbusProtocol.ReadInputRegisters:
            case ModbusProtocol.GetCommEventLog:
            case ModbusProtocol.ReportServerId:
            case ModbusProtocol.ReadFileRecord:
            case ModbusProtocol.WriteFileRecord:
            case ModbusProtocol.ReadWriteMultipleRegisters:
                required = 3;
                return head.Length < 3 ? 0 : 3 + head[2] + CrcLength;
            case ModbusProtocol.WriteSingleCoil:
            case ModbusProtocol.WriteSingleRegister:
            case ModbusProtocol.WriteMultipleCoils:
            case ModbusProtocol.WriteMultipleRegisters:
            case ModbusProtocol.Diagnostics:
            case ModbusProtocol.GetCommEventCounter:
                return 1 + 5 + CrcLength;
            case ModbusProtocol.ReadExceptionStatus:
                return 1 + 2 + CrcLength;
            case ModbusProtocol.MaskWriteRegister:
                return 1 + 7 + CrcLength;
            case ModbusProtocol.ReadFifoQueue:
                required = 4;
                return head.Length < 4 ? 0 : 4 + BinaryPrimitives.ReadUInt16BigEndian(head[2..]) + CrcLength;
            case ModbusProtocol.EncapsulatedInterfaceTransport:
                return DeviceIdentificationLength(head, out required);
            default:
                return UnknownLength;
        }
    }

    // Unit, 0x2B, 0x0E, ReadDevId code, conformity, more follows, next object id, object count, then
    // (object id, length, value) per object.
    private static int DeviceIdentificationLength(ReadOnlySpan<byte> head, out int required)
    {
        required = 3;
        if (head.Length < 3)
            return 0;
        if (head[2] != ModbusProtocol.MeiReadDeviceIdentification)
            return UnknownLength;

        required = 8;
        if (head.Length < 8)
            return 0;

        var position = 8;
        for (var index = 0; index < head[7]; index++)
        {
            required = position + 2;
            if (head.Length < required)
                return 0;
            position += 2 + head[position + 1];
        }

        return position + CrcLength;
    }
}
