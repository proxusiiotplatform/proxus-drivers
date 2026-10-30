namespace Proxus.Drivers;

/// <summary>One heterogeneous read inside an arbitrary batch operation.</summary>
public readonly record struct DriverReadBatchItem<TDescriptor>(
    int DestinationOffset,
    TDescriptor Descriptor)
    where TDescriptor : struct;

/// <summary>
///     One heterogeneous write inside an arbitrary batch operation. SourceOffset points at
///     the first DriverValue for the descriptor; arrays consume their protocol descriptor's
///     element count. Batch execution is ordered but is not a cross-device transaction.
/// </summary>
public readonly record struct DriverWriteBatchItem<TDescriptor>(
    int SourceOffset,
    TDescriptor Descriptor)
    where TDescriptor : struct;
