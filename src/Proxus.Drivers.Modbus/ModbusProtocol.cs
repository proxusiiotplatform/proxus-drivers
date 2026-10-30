using System.Buffers.Binary;

namespace Proxus.Drivers.Modbus;

internal static class ModbusProtocol
{
    public const int MbapHeaderLength = 7;
    public const int MaximumPduLength = 253;
    public const int MaximumAduLength = MbapHeaderLength + MaximumPduLength;
    public const ushort MaximumReadRegisters = 125;
    public const ushort MaximumWriteRegisters = 123;
    public const ushort MaximumReadBits = 2000;
    public const ushort MaximumWriteBits = 1968;
    public const ushort MaximumReadWriteWriteRegisters = 121;

    public const byte ReadCoils = 0x01;
    public const byte ReadDiscreteInputs = 0x02;
    public const byte ReadHoldingRegisters = 0x03;
    public const byte ReadInputRegisters = 0x04;
    public const byte WriteSingleCoil = 0x05;
    public const byte WriteSingleRegister = 0x06;
    public const byte ReadExceptionStatus = 0x07;
    public const byte Diagnostics = 0x08;
    public const byte GetCommEventCounter = 0x0B;
    public const byte GetCommEventLog = 0x0C;
    public const byte ReportServerId = 0x11;
    public const byte ReadFileRecord = 0x14;
    public const byte WriteFileRecord = 0x15;
    public const byte WriteMultipleCoils = 0x0F;
    public const byte WriteMultipleRegisters = 0x10;
    public const byte MaskWriteRegister = 0x16;
    public const byte ReadWriteMultipleRegisters = 0x17;
    public const byte ReadFifoQueue = 0x18;
    public const byte EncapsulatedInterfaceTransport = 0x2B;
    public const byte MeiReadDeviceIdentification = 0x0E;

    public static int WriteAdu(
        Span<byte> destination,
        ushort transactionId,
        byte unitId,
        ReadOnlySpan<byte> pdu)
    {
        var totalLength = MbapHeaderLength + pdu.Length;
        if (pdu.IsEmpty || pdu.Length > MaximumPduLength || destination.Length < totalLength)
            throw new ArgumentException("Invalid Modbus PDU length.", nameof(pdu));

        BinaryPrimitives.WriteUInt16BigEndian(destination, transactionId);
        BinaryPrimitives.WriteUInt16BigEndian(destination[2..], 0);
        BinaryPrimitives.WriteUInt16BigEndian(destination[4..], checked((ushort)(pdu.Length + 1)));
        destination[6] = unitId;
        pdu.CopyTo(destination[MbapHeaderLength..]);
        return totalLength;
    }

    public static DriverError? ValidateResponseHeader(
        ReadOnlySpan<byte> header,
        ushort expectedTransactionId,
        byte expectedUnitId,
        out int pduLength)
    {
        pduLength = 0;
        if (header.Length != MbapHeaderLength)
            return ProtocolError("MODBUS_MBAP_SIZE", "MBAP header must contain 7 bytes.");

        var transactionId = BinaryPrimitives.ReadUInt16BigEndian(header);
        if (transactionId != expectedTransactionId)
            return ProtocolError(
                "MODBUS_TRANSACTION",
                $"Expected transaction {expectedTransactionId}, received {transactionId}.");

        var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header[2..]);
        if (protocolId != 0)
            return ProtocolError("MODBUS_PROTOCOL_ID", $"Protocol identifier must be 0, received {protocolId}.");

        var length = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
        if (length is < 2 or > MaximumPduLength + 1)
            return ProtocolError("MODBUS_LENGTH", $"Invalid MBAP length {length}.");

        if (header[6] != expectedUnitId)
            return ProtocolError(
                "MODBUS_UNIT",
                $"Expected unit {expectedUnitId}, received {header[6]}.");

        pduLength = length - 1;
        return null;
    }

    public static DriverError? ValidateFunctionResponse(
        ReadOnlySpan<byte> pdu,
        byte expectedFunction)
    {
        if (pdu.IsEmpty)
            return ProtocolError("MODBUS_EMPTY_PDU", "Response PDU is empty.");

        if (pdu[0] == (byte)(expectedFunction | 0x80))
        {
            if (pdu.Length != 2)
                return ProtocolError(
                    "MODBUS_EXCEPTION_LENGTH",
                    $"Exception response must contain exactly 2 bytes, received {pdu.Length}.");

            var exception = pdu[1];
            return ModbusDiagnostics.DeviceException(exception);
        }

        if (pdu[0] != expectedFunction)
            return ProtocolError(
                "MODBUS_FUNCTION",
                $"Expected function 0x{expectedFunction:X2}, received 0x{pdu[0]:X2}.");

        return null;
    }

    public static DriverError ProtocolError(string code, string message)
    {
        return new DriverError(
            DriverErrorKind.Protocol, code, message,
            Protocol: "Modbus TCP",
            Cause:
            "The received Modbus TCP frame violates the expected request/response contract or does not match the in-flight transaction.",
            SuggestedAction:
            "Retry on a fresh connection. If this repeats, check for multiple masters sharing a connection, gateway/proxy corruption, Unit ID mismatch, or a non-conformant device.",
            Retry: DriverRetryDisposition.ReconnectAndRetry,
            Connection: DriverConnectionDisposition.Broken);
    }
}
