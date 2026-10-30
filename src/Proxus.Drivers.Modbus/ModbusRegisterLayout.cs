namespace Proxus.Drivers.Modbus;

/// <summary>
///     Describes vendor-specific ordering inside one scalar value. For arrays the layout
///     is applied independently to each element and never reverses the whole array.
/// </summary>
public readonly record struct ModbusRegisterLayout(
    bool SwapBytesInWord,
    bool ReverseWordOrder)
{
    public static ModbusRegisterLayout Standard => default;
    public static ModbusRegisterLayout ByteSwapped => new(true, false);
    public static ModbusRegisterLayout WordSwapped => new(false, true);
    public static ModbusRegisterLayout LittleEndian => new(true, true);
}
