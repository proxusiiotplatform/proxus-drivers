namespace Proxus.Drivers;

/// <summary>
///     Reflection-free industrial driver contract suitable for static registration and Native AOT.
///     Scalar, typed-array and heterogeneous batch operations are part of the common contract;
///     protocol implementations may additionally expose raw block and compiled-plan APIs.
/// </summary>
public interface IIndustrialDriver<TDescriptor> : IAsyncDisposable
    where TDescriptor : struct
{
    DriverCapabilities Capabilities { get; }
    bool IsConnected { get; }

    ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default);
    ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default);

    ValueTask<DriverResult<DriverValue>> ReadValueAsync(
        TDescriptor descriptor,
        CancellationToken cancellationToken = default);

    ValueTask<DriverResult<int>> ReadValuesAsync(
        TDescriptor descriptor,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default);

    ValueTask<DriverResult<int>> ReadBatchAsync(
        ReadOnlyMemory<DriverReadBatchItem<TDescriptor>> items,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default);

    ValueTask<DriverResult> WriteValueAsync(
        TDescriptor descriptor,
        DriverValue value,
        CancellationToken cancellationToken = default);

    ValueTask<DriverResult> WriteValuesAsync(
        TDescriptor descriptor,
        ReadOnlyMemory<DriverValue> values,
        CancellationToken cancellationToken = default);

    ValueTask<DriverResult<int>> WriteBatchAsync(
        ReadOnlyMemory<DriverWriteBatchItem<TDescriptor>> items,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default);
}
