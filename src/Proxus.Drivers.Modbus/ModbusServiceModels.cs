using System.Text;

namespace Proxus.Drivers.Modbus;

public readonly record struct ModbusCommEventCounter(ushort Status, ushort EventCount)
{
    public bool IsBusy => Status == 0xFFFF;
}

public sealed record ModbusCommEventLog(
    ushort Status,
    ushort EventCount,
    ushort MessageCount,
    byte[] Events)
{
    public bool IsBusy => Status == 0xFFFF;
}

/// <summary>
///     FC17 (0x11) Report Server ID response. Modbus Application Protocol V1.1b3 section 6.13 makes the
///     Server ID length device specific; the Run Indicator Status (0x00 OFF, 0xFF ON) follows it.
/// </summary>
/// <param name="ServerId">First payload byte, the Server ID under the common one-byte layout.</param>
/// <param name="RunIndicator">
///     Run Indicator Status under the one-byte layout; <see langword="false" /> when
///     <see cref="HasOneByteServerIdLayout" /> is <see langword="false" />.
/// </param>
/// <param name="AdditionalData">Bytes after the Run Indicator under the one-byte layout; otherwise empty.</param>
public sealed record ModbusServerId(
    byte ServerId,
    bool RunIndicator,
    byte[] AdditionalData)
{
    /// <summary>The complete device-specific payload that follows the byte count.</summary>
    public byte[] Data { get; init; } = [];

    /// <summary>
    ///     <see langword="true" /> when the second payload byte is a valid Run Indicator (0x00 or 0xFF), so the
    ///     reply is interpreted with a one-byte Server ID. <see langword="false" /> when the device uses a
    ///     longer Server ID; read the vendor-defined layout from <see cref="Data" />.
    /// </summary>
    public bool HasOneByteServerIdLayout { get; init; } = true;
}

public readonly record struct ModbusDeviceIdObject(byte ObjectId, byte[] Value)
{
    public string AsAscii()
    {
        return Encoding.ASCII.GetString(Value);
    }
}

public sealed record ModbusDeviceIdentification(
    ModbusDeviceIdReadCode ReadDeviceIdCode,
    ModbusDeviceIdConformity ConformityLevel,
    bool MoreFollows,
    byte NextObjectId,
    ModbusDeviceIdObject[] Objects);

public readonly record struct ModbusFileRecordReadRequest(
    ushort FileNumber,
    ushort RecordNumber,
    ushort RecordLength);

public sealed record ModbusFileRecordReadResult(
    ushort FileNumber,
    ushort RecordNumber,
    ushort[] Values);

public readonly record struct ModbusFileRecordWriteRequest(
    ushort FileNumber,
    ushort RecordNumber,
    ReadOnlyMemory<ushort> Values);
