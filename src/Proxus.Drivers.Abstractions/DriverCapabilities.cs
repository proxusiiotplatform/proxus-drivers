namespace Proxus.Drivers;

[Flags]
public enum DriverFeature : ulong
{
    None = 0,
    ReadBits = 1UL << 0,
    ReadRegisters = 1UL << 1,
    WriteBits = 1UL << 2,
    WriteRegisters = 1UL << 3,
    ReadMultipleContiguous = 1UL << 4,
    WriteMultipleContiguous = 1UL << 5,
    PersistentConnection = 1UL << 6,
    NativeAsyncIo = 1UL << 7,
    Cancellation = 1UL << 8,
    AddressCompilation = 1UL << 9,
    TypedArrays = 1UL << 10,
    ArbitraryBatchRead = 1UL << 11,
    ArbitraryBatchWrite = 1UL << 12,
    MaskWriteRegister = 1UL << 13,
    ReadWriteMultipleRegisters = 1UL << 14,
    CompiledReadPlans = 1UL << 15,
    CompiledWritePlans = 1UL << 16,
    DiagnosticsServices = 1UL << 17,
    FileRecordServices = 1UL << 18,
    FifoQueue = 1UL << 19,
    DeviceIdentification = 1UL << 20,
    RawProtocolPdu = 1UL << 21,
    EncapsulatedInterfaceTransport = 1UL << 22
}

public readonly record struct DriverCapabilities(
    string Protocol,
    DriverFeature Features,
    ushort MaximumReadRegisters,
    ushort MaximumWriteRegisters,
    ushort MaximumReadBits,
    ushort MaximumWriteBits,
    bool SupportsConcurrentRequests)
{
    public bool Supports(DriverFeature feature)
    {
        return feature == DriverFeature.None || (Features & feature) == feature;
    }
}
