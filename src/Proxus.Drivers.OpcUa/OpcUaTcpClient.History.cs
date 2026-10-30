namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private const int MaximumBrowsePathElements = 64;

    /// <summary>
    ///     Resolves browse paths to NodeIds with TranslateBrowsePathsToNodeIds (OPC 10000-4 §5.8.4). Each path
    ///     gets its own status; a path that matches nothing returns a Bad status (for example Bad_NoMatch)
    ///     instead of failing the call.
    /// </summary>
    public async ValueTask<DriverResult<OpcUaBrowsePathResult[]>> TranslateBrowsePathsToNodeIdsAsync(
        ReadOnlyMemory<OpcUaBrowsePath> paths,
        CancellationToken cancellationToken = default)
    {
        if (paths.IsEmpty) return DriverResult<OpcUaBrowsePathResult[]>.Success([]);
        foreach (var path in paths.Span)
            if (path is null || path.Elements.Count is 0 or > MaximumBrowsePathElements)
                return DriverResult<OpcUaBrowsePathResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_BROWSE_PATH",
                    $"Each browse path needs between 1 and {MaximumBrowsePathElements} elements.",
                    DriverOperation.Browse, DriverRetryDisposition.FixConfiguration,
                    DriverConnectionDisposition.Unchanged).Error);

        var socket = _socket;
        if (socket is null || SecureChannel is null || Session is not { IsActivated: true } session)
            return DriverResult<OpcUaBrowsePathResult[]>.Failure(
                SessionServiceUnavailable(DriverOperation.Browse, "TranslateBrowsePathsToNodeIds"));

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.TranslateBrowsePathsToNodeIdsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteInt32(paths.Length);
        foreach (var path in paths.Span)
        {
            writer.WriteNodeId(path.StartingNode);
            writer.WriteInt32(path.Elements.Count);
            foreach (var element in path.Elements)
            {
                writer.WriteNodeId(element.ReferenceTypeId ??
                                   IndustrialNodeId.Numeric(0, OpcUaProtocol.HierarchicalReferences));
                writer.WriteBoolean(element.IsInverse);
                writer.WriteBoolean(element.IncludeSubtypes);
                writer.WriteQualifiedName(element.TargetName);
            }
        }

        var envelope = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.TranslateBrowsePathsToNodeIdsResponseBinary, DriverOperation.Browse, cancellationToken)
            .ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaBrowsePathResult[]>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "TranslateBrowsePathsToNodeIds.Results", 8);
            if (count != paths.Length)
                throw new InvalidDataException(
                    $"TranslateBrowsePathsToNodeIds returned {count} results for {paths.Length} paths.");
            var results = new OpcUaBrowsePathResult[count];
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                var targetCount = ReadArrayLength(ref reader, "BrowsePathResult.Targets", 6);
                var targets = new OpcUaBrowsePathTarget[targetCount];
                for (var t = 0; t < targetCount; t++)
                    targets[t] = new OpcUaBrowsePathTarget(reader.ReadExpandedNodeId(), reader.ReadUInt32());
                results[i] = new OpcUaBrowsePathResult(status, targets);
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaBrowsePathResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaBrowsePathResult[]>.Failure(Failure(DriverErrorKind.Protocol,
                "UA_TRANSLATE_DECODE", ex.Message, DriverOperation.Browse,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    /// <summary>
    ///     Reads raw (or modified) historical values with HistoryRead and ReadRawModifiedDetails
    ///     (OPC 10000-11 §6.4.3). A result with a continuation point has more values; call again with that
    ///     continuation point, or release it.
    /// </summary>
    public ValueTask<DriverResult<OpcUaHistoryReadResult[]>> HistoryReadRawAsync(
        ReadOnlyMemory<OpcUaHistoryReadNode> nodes,
        OpcUaHistoryReadRawRequest request,
        CancellationToken cancellationToken = default)
    {
        var invalid = ValidateRawRequest(request);
        if (invalid is { } error)
            return ValueTask.FromResult(DriverResult<OpcUaHistoryReadResult[]>.Failure(error));

        using var details = new UaBinaryWriter(64);
        details.WriteBoolean(request.IsReadModified);
        details.WriteDateTime(request.StartTime);
        details.WriteDateTime(request.EndTime);
        details.WriteUInt32(request.NumValuesPerNode);
        details.WriteBoolean(request.ReturnBounds);
        return HistoryReadCoreAsync(OpcUaProtocol.ReadRawModifiedDetailsBinary, details.WrittenSpan.ToArray(),
            request.TimestampsToReturn, false, nodes, cancellationToken);
    }

    /// <summary>
    ///     Reads values at the given times with HistoryRead and ReadAtTimeDetails (OPC 10000-11 §6.4.5). The
    ///     server interpolates between stored values when <paramref name="useSimpleBounds" /> is true.
    /// </summary>
    public ValueTask<DriverResult<OpcUaHistoryReadResult[]>> HistoryReadAtTimeAsync(
        ReadOnlyMemory<OpcUaHistoryReadNode> nodes,
        ReadOnlyMemory<DateTime> requestedTimes,
        bool useSimpleBounds = true,
        OpcUaTimestampsToReturn timestampsToReturn = OpcUaTimestampsToReturn.Both,
        CancellationToken cancellationToken = default)
    {
        if (requestedTimes.IsEmpty)
            return ValueTask.FromResult(DriverResult<OpcUaHistoryReadResult[]>.Failure(
                HistoryConfiguration("UA_HISTORY_TIMES", "At least one requested time is required.")));

        using var details = new UaBinaryWriter(16 + requestedTimes.Length * 8);
        details.WriteInt32(requestedTimes.Length);
        foreach (var time in requestedTimes.Span) details.WriteDateTime(time);
        details.WriteBoolean(useSimpleBounds);
        return HistoryReadCoreAsync(OpcUaProtocol.ReadAtTimeDetailsBinary, details.WrittenSpan.ToArray(),
            timestampsToReturn, false, nodes, cancellationToken);
    }

    /// <summary>
    ///     Releases the server resources of history continuation points without reading more values
    ///     (HistoryRead with releaseContinuationPoints set).
    /// </summary>
    public async ValueTask<DriverResult> ReleaseHistoryContinuationPointsAsync(
        ReadOnlyMemory<OpcUaHistoryReadNode> nodes,
        CancellationToken cancellationToken = default)
    {
        using var details = new UaBinaryWriter(32);
        details.WriteBoolean(false);
        details.WriteDateTime(DateTime.MinValue);
        details.WriteDateTime(DateTime.MinValue);
        details.WriteUInt32(0);
        details.WriteBoolean(false);
        var released = await HistoryReadCoreAsync(OpcUaProtocol.ReadRawModifiedDetailsBinary,
            details.WrittenSpan.ToArray(), OpcUaTimestampsToReturn.Both, true, nodes, cancellationToken)
            .ConfigureAwait(false);
        return released.IsSuccess ? DriverResult.Success() : DriverResult.Failure(released.Error);
    }

    private async ValueTask<DriverResult<OpcUaHistoryReadResult[]>> HistoryReadCoreAsync(
        uint detailsTypeId,
        byte[] detailsBody,
        OpcUaTimestampsToReturn timestampsToReturn,
        bool releaseContinuationPoints,
        ReadOnlyMemory<OpcUaHistoryReadNode> nodes,
        CancellationToken cancellationToken)
    {
        if (nodes.IsEmpty) return DriverResult<OpcUaHistoryReadResult[]>.Success([]);
        if (timestampsToReturn is not (OpcUaTimestampsToReturn.Source or OpcUaTimestampsToReturn.Server
            or OpcUaTimestampsToReturn.Both))
            return DriverResult<OpcUaHistoryReadResult[]>.Failure(HistoryConfiguration("UA_HISTORY_TIMESTAMPS",
                $"TimestampsToReturn {(uint)timestampsToReturn} is not valid for HistoryRead."));

        var socket = _socket;
        if (socket is null || SecureChannel is null || Session is not { IsActivated: true } session)
            return DriverResult<OpcUaHistoryReadResult[]>.Failure(
                SessionServiceUnavailable(DriverOperation.Read, "HistoryRead"));

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.HistoryReadRequestBinary, session.AuthenticationToken,
            requestId, requestId);
        writer.WriteExtensionObject(new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, detailsTypeId)),
            DriverExtensionObjectEncoding.Binary, detailsBody));
        writer.WriteUInt32((uint)timestampsToReturn);
        writer.WriteBoolean(releaseContinuationPoints);
        writer.WriteInt32(nodes.Length);
        foreach (var node in nodes.Span)
        {
            writer.WriteNodeId(node.NodeId);
            writer.WriteString(node.IndexRange);
            writer.WriteUInt16(0);
            writer.WriteString(null); // null DataEncoding
            if (node.ContinuationPoint.IsEmpty)
                writer.WriteNullByteString();
            else
                writer.WriteByteString(node.ContinuationPoint.Span);
        }

        var envelope = await SendServiceAsync(socket, writer, requestId, requestId,
            OpcUaProtocol.HistoryReadResponseBinary, DriverOperation.Read, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaHistoryReadResult[]>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "HistoryRead.Results", 12);
            if (count != nodes.Length)
                throw new InvalidDataException($"HistoryRead returned {count} results for {nodes.Length} nodes.");
            var results = new OpcUaHistoryReadResult[count];
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                var continuationPoint = reader.ReadByteString();
                var historyData = reader.ReadExtensionObject();
                results[i] = new OpcUaHistoryReadResult(status, continuationPoint, DecodeHistoryData(historyData));
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaHistoryReadResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaHistoryReadResult[]>.Failure(Failure(DriverErrorKind.Protocol,
                "UA_HISTORY_READ_DECODE", ex.Message, DriverOperation.Read,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    // HistoryData and HistoryModifiedData both start with the DataValue array; the ModificationInfo array that
    // follows in HistoryModifiedData is not exposed.
    private static DriverSample[] DecodeHistoryData(DriverExtensionObject historyData)
    {
        if (historyData.Encoding == DriverExtensionObjectEncoding.None)
            return [];
        var typeId = historyData.TypeId.NodeId;
        if (historyData.Encoding != DriverExtensionObjectEncoding.Binary || typeId.NamespaceIndex != 0 ||
            typeId.Kind != IndustrialNodeIdKind.Numeric ||
            typeId.NumericIdentifier is not (OpcUaProtocol.HistoryDataBinary or OpcUaProtocol.HistoryModifiedDataBinary))
            throw new NotSupportedException($"HistoryRead returned unsupported history data type {typeId}.");
        var body = new UaBinaryReader(historyData.EncodedBody.Span);
        var count = ReadArrayLength(ref body, "HistoryData.DataValues", 1);
        var values = new DriverSample[count];
        for (var i = 0; i < count; i++) values[i] = OpcUaBinaryCodec.ReadDataValue(ref body);
        return values;
    }

    private DriverError? ValidateRawRequest(OpcUaHistoryReadRawRequest request)
    {
        var hasStart = request.StartTime != DateTime.MinValue;
        var hasEnd = request.EndTime != DateTime.MinValue;
        if (!hasStart && !hasEnd)
            return HistoryConfiguration("UA_HISTORY_INTERVAL", "StartTime, EndTime or both must be set.");
        if ((!hasStart || !hasEnd) && request.NumValuesPerNode == 0)
            return HistoryConfiguration("UA_HISTORY_INTERVAL",
                "An open interval needs a non-zero NumValuesPerNode.");
        return null;
    }

    private DriverError HistoryConfiguration(string code, string message)
    {
        return Failure(DriverErrorKind.Configuration, code, message, DriverOperation.Read,
            DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error;
    }
}
