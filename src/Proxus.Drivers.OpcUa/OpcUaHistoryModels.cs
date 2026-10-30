namespace Proxus.Drivers.OpcUa;

/// <summary>
///     One element of a RelativePath (OPC 10000-4 §7.31): follow references of
///     <see cref="ReferenceTypeId" /> (default HierarchicalReferences, i=33) to a target whose BrowseName is
///     <see cref="TargetName" />.
/// </summary>
public readonly record struct OpcUaRelativePathElement(
    IndustrialQualifiedName TargetName,
    IndustrialNodeId? ReferenceTypeId = null,
    bool IsInverse = false,
    bool IncludeSubtypes = true);

/// <summary>A starting node and the RelativePath to resolve from it with TranslateBrowsePathsToNodeIds.</summary>
public sealed class OpcUaBrowsePath
{
    public OpcUaBrowsePath(IndustrialNodeId startingNode, params OpcUaRelativePathElement[] elements)
    {
        StartingNode = startingNode ?? throw new ArgumentNullException(nameof(startingNode));
        ArgumentNullException.ThrowIfNull(elements);
        Elements = [.. elements];
    }

    public IndustrialNodeId StartingNode { get; }
    public IReadOnlyList<OpcUaRelativePathElement> Elements { get; }

    /// <summary>
    ///     Builds a path that follows hierarchical references through the given BrowseNames, for example
    ///     <c>OpcUaBrowsePath.FromBrowseNames(objectsFolder, new(0, "Server"), new(0, "ServerStatus"))</c>.
    /// </summary>
    public static OpcUaBrowsePath FromBrowseNames(IndustrialNodeId startingNode,
        params IndustrialQualifiedName[] browseNames)
    {
        ArgumentNullException.ThrowIfNull(browseNames);
        return new OpcUaBrowsePath(startingNode,
            [.. browseNames.Select(static name => new OpcUaRelativePathElement(name))]);
    }
}

/// <summary>
///     A node matched by a browse path. <see cref="RemainingPathIndex" /> is <see cref="uint.MaxValue" /> when
///     the whole path was resolved; otherwise the target is in another server and resolution stopped at
///     that element.
/// </summary>
public readonly record struct OpcUaBrowsePathTarget(IndustrialExpandedNodeId TargetId, uint RemainingPathIndex)
{
    public bool IsComplete => RemainingPathIndex == uint.MaxValue;
}

/// <summary>Per-path TranslateBrowsePathsToNodeIds result. A Bad status (for example Bad_NoMatch) has no targets.</summary>
public sealed class OpcUaBrowsePathResult
{
    public OpcUaBrowsePathResult(uint statusCode, OpcUaBrowsePathTarget[] targets)
    {
        StatusCode = statusCode;
        Targets = targets ?? throw new ArgumentNullException(nameof(targets));
    }

    public uint StatusCode { get; }
    public DriverQuality Quality => OpcUaBinaryCodec.MapQuality(StatusCode);
    public IReadOnlyList<OpcUaBrowsePathTarget> Targets { get; }
}

/// <summary>Which timestamps a HistoryRead returns (OPC 10000-4 §7.40, TimestampsToReturn).</summary>
public enum OpcUaTimestampsToReturn : uint
{
    Source = 0,
    Server = 1,
    Both = 2
}

/// <summary>A node to read history for, with the continuation point of a previous partial read, if any.</summary>
public readonly record struct OpcUaHistoryReadNode(
    IndustrialNodeId NodeId,
    ReadOnlyMemory<byte> ContinuationPoint = default,
    string? IndexRange = null)
{
    public IndustrialNodeId NodeId { get; init; } = NodeId ?? throw new ArgumentNullException(nameof(NodeId));
}

/// <summary>
///     ReadRawModifiedDetails (OPC 10000-11 §6.4.3). Leave <see cref="StartTime" /> or <see cref="EndTime" />
///     unset (<see cref="DateTime.MinValue" />) for an open interval; at least one must be set, and
///     <see cref="NumValuesPerNode" /> must be non-zero when one is unset.
/// </summary>
public readonly record struct OpcUaHistoryReadRawRequest(
    DateTime StartTime,
    DateTime EndTime,
    uint NumValuesPerNode = 0,
    bool ReturnBounds = false,
    bool IsReadModified = false,
    OpcUaTimestampsToReturn TimestampsToReturn = OpcUaTimestampsToReturn.Both);

/// <summary>
///     Per-node HistoryRead result. A non-empty <see cref="ContinuationPoint" /> means more values exist; pass
///     it back in <see cref="OpcUaHistoryReadNode.ContinuationPoint" />, or release it with
///     <see cref="OpcUaTcpClient.ReleaseHistoryContinuationPointsAsync" />.
/// </summary>
public sealed class OpcUaHistoryReadResult
{
    public OpcUaHistoryReadResult(uint statusCode, byte[]? continuationPoint, DriverSample[] values)
    {
        StatusCode = statusCode;
        ContinuationPoint = continuationPoint is null ? null : (byte[])continuationPoint.Clone();
        Values = values ?? throw new ArgumentNullException(nameof(values));
    }

    public uint StatusCode { get; }
    public DriverQuality Quality => OpcUaBinaryCodec.MapQuality(StatusCode);
    public byte[]? ContinuationPoint { get; }
    public bool HasContinuationPoint => ContinuationPoint is { Length: > 0 };
    public IReadOnlyList<DriverSample> Values { get; }
}
