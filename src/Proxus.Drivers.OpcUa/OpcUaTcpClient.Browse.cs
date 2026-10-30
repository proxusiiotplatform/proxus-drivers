namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    public async ValueTask<DriverResult<OpcUaBrowseResult>> BrowseAsync(
        OpcUaBrowseDescription description,
        uint requestedMaxReferencesPerNode = 0,
        CancellationToken cancellationToken = default)
    {
        var results = await BrowseAsync(new[] { description }, requestedMaxReferencesPerNode, cancellationToken)
            .ConfigureAwait(false);
        if (!results.IsSuccess) return DriverResult<OpcUaBrowseResult>.Failure(results.Error);
        return DriverResult<OpcUaBrowseResult>.Success(results.Value[0]);
    }

    public async ValueTask<DriverResult<OpcUaBrowseResult[]>> BrowseAsync(
        ReadOnlyMemory<OpcUaBrowseDescription> descriptions,
        uint requestedMaxReferencesPerNode = 0,
        CancellationToken cancellationToken = default)
    {
        if (descriptions.IsEmpty) return DriverResult<OpcUaBrowseResult[]>.Success([]);
        if (Session is not { IsActivated: true } session)
            return DriverResult<OpcUaBrowseResult[]>.Failure(SessionServiceUnavailable(DriverOperation.Browse, "Browse"));
        foreach (var item in descriptions.Span)
        {
            if (item.Direction is not (OpcUaBrowseDirection.Forward or OpcUaBrowseDirection.Inverse
                or OpcUaBrowseDirection.Both))
                return DriverResult<OpcUaBrowseResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_BROWSE_DIRECTION", $"Unknown BrowseDirection {(uint)item.Direction}.", DriverOperation.Browse,
                    DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
            if (((uint)item.NodeClassMask & ~(uint)OpcUaNodeClass.All) != 0)
                return DriverResult<OpcUaBrowseResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_NODECLASS_MASK",
                    $"Browse NodeClassMask 0x{(uint)item.NodeClassMask:X} contains unsupported bits.",
                    DriverOperation.Browse,
                    DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
            if (((uint)item.ResultMask & ~(uint)OpcUaBrowseResultMask.All) != 0)
                return DriverResult<OpcUaBrowseResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_BROWSE_RESULT_MASK",
                    $"Browse ResultMask 0x{(uint)item.ResultMask:X} contains unsupported bits.", DriverOperation.Browse,
                    DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
        }

        var socket = _socket;
        if (socket is null || SecureChannel is null)
            return DriverResult<OpcUaBrowseResult[]>.Failure(Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
                "SecureChannel closed before Browse.", DriverOperation.Browse,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.BrowseRequestBinary, session.AuthenticationToken,
            requestId, requestHandle);
        WriteNullViewDescription(writer);
        writer.WriteUInt32(requestedMaxReferencesPerNode);
        writer.WriteInt32(descriptions.Length);
        foreach (var item in descriptions.Span) WriteBrowseDescription(writer, item);
        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
            OpcUaProtocol.BrowseResponseBinary, DriverOperation.Browse, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaBrowseResult[]>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "Browse.Results", 12);
            if (count != descriptions.Length)
                throw new InvalidDataException(
                    $"Browse returned {count} results for {descriptions.Length} requested nodes.");
            var results = new OpcUaBrowseResult[count];
            for (var i = 0; i < count; i++) results[i] = ReadBrowseResult(ref reader);
            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaBrowseResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaBrowseResult[]>.Failure(Failure(DriverErrorKind.Protocol, "UA_BROWSE_DECODE",
                ex.Message, DriverOperation.Browse,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    public async ValueTask<DriverResult<OpcUaBrowseResult>> BrowseNextAsync(
        ReadOnlyMemory<byte> continuationPoint,
        bool releaseContinuationPoint = false,
        CancellationToken cancellationToken = default)
    {
        if (continuationPoint.IsEmpty)
            return DriverResult<OpcUaBrowseResult>.Failure(Failure(DriverErrorKind.Configuration,
                "UA_CONTINUATION_POINT", "BrowseNext requires a non-empty continuation point.", DriverOperation.Browse,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
        if (Session is not { IsActivated: true } session)
            return DriverResult<OpcUaBrowseResult>.Failure(SessionServiceUnavailable(DriverOperation.Browse,
                "BrowseNext"));

        var socket = _socket;
        if (socket is null || SecureChannel is null)
            return DriverResult<OpcUaBrowseResult>.Failure(Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
                "SecureChannel closed before BrowseNext.", DriverOperation.Browse,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.BrowseNextRequestBinary, session.AuthenticationToken,
            requestId, requestHandle);
        writer.WriteBoolean(releaseContinuationPoint);
        writer.WriteInt32(1);
        writer.WriteByteString(continuationPoint.Span);
        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
            OpcUaProtocol.BrowseNextResponseBinary, DriverOperation.Browse, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaBrowseResult>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "BrowseNext.Results", 12);
            if (count != 1)
                throw new InvalidDataException($"BrowseNext returned {count} results for one continuation point.");
            var result = ReadBrowseResult(ref reader);
            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaBrowseResult>.Success(result);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaBrowseResult>.Failure(Failure(DriverErrorKind.Protocol, "UA_BROWSE_NEXT_DECODE",
                ex.Message, DriverOperation.Browse,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    public async ValueTask<DriverResult<OpcUaReferenceDescription[]>> BrowseAllAsync(
        OpcUaBrowseDescription description,
        uint requestedMaxReferencesPerNode = 0,
        CancellationToken cancellationToken = default)
    {
        var first = await BrowseAsync(description, requestedMaxReferencesPerNode, cancellationToken)
            .ConfigureAwait(false);
        if (!first.IsSuccess) return DriverResult<OpcUaReferenceDescription[]>.Failure(first.Error);
        if (!first.Value.Quality.IsGood)
            return DriverResult<OpcUaReferenceDescription[]>.Failure(BrowseStatusFailure(first.Value.StatusCode,
                description.NodeId));

        var references = new List<OpcUaReferenceDescription>(first.Value.References.Count);
        references.AddRange(first.Value.References);
        var continuation = first.Value.ContinuationPoint;
        var hasContinuation = first.Value.HasContinuationPoint;
        var pages = 0;
        while (hasContinuation && continuation is { Length: > 0 })
        {
            if (++pages > _options.MaxBrowseContinuationPages)
                return DriverResult<OpcUaReferenceDescription[]>.Failure(Failure(DriverErrorKind.Protocol,
                    "UA_BROWSE_PAGE_LIMIT",
                    $"Browse exceeded the configured continuation-page limit {_options.MaxBrowseContinuationPages}.",
                    DriverOperation.Browse,
                    DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Healthy).Error);
            var next = await BrowseNextAsync(continuation, false, cancellationToken).ConfigureAwait(false);
            if (!next.IsSuccess) return DriverResult<OpcUaReferenceDescription[]>.Failure(next.Error);
            if (!next.Value.Quality.IsGood)
                return DriverResult<OpcUaReferenceDescription[]>.Failure(BrowseStatusFailure(next.Value.StatusCode,
                    description.NodeId));
            if ((long)references.Count + next.Value.References.Count > _options.MaxBrowseReferences)
                return DriverResult<OpcUaReferenceDescription[]>.Failure(Failure(DriverErrorKind.Protocol,
                    "UA_BROWSE_REFERENCE_LIMIT",
                    $"Browse exceeded the configured reference limit {_options.MaxBrowseReferences}.",
                    DriverOperation.Browse,
                    DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Healthy).Error);
            references.AddRange(next.Value.References);
            continuation = next.Value.ContinuationPoint;
            hasContinuation = next.Value.HasContinuationPoint;
        }

        return DriverResult<OpcUaReferenceDescription[]>.Success([.. references]);
    }

    public async ValueTask<DriverResult<OpcUaDiscoveredDataType>> ReadDataTypeDefinitionAsync(
        IndustrialNodeId dataTypeNodeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataTypeNodeId);
        var nodes = new[]
        {
            new OpcUaNodeDescriptor(dataTypeNodeId, OpcUaAttributeId.BrowseName),
            new OpcUaNodeDescriptor(dataTypeNodeId, OpcUaAttributeId.DataTypeDefinition)
        };
        var values = new DriverSample[2];
        var read = await ReadAsync(nodes, values, cancellationToken).ConfigureAwait(false);
        if (!read.IsSuccess) return DriverResult<OpcUaDiscoveredDataType>.Failure(read.Error);
        for (var i = 0; i < values.Length; i++)
            if (!values[i].Quality.IsGood)
                return DriverResult<OpcUaDiscoveredDataType>.Failure(ServiceFailure(values[i].Quality.NativeStatusCode,
                    "UA_DATATYPE_ATTRIBUTE",
                    $"Server returned a Bad StatusCode while reading {(i == 0 ? "BrowseName" : "DataTypeDefinition")} for DataType {dataTypeNodeId}.",
                    DriverOperation.Discover).Error);
        try
        {
            var browseName = values[0].Value.AsQualifiedName();
            if (values[1].Value.Type != IndustrialDataType.ExtensionObject)
                throw new InvalidDataException(
                    $"DataTypeDefinition attribute returned {values[1].Value.Type}, expected ExtensionObject.");
            var definition = OpcUaDataTypeDefinitionCodec.Decode(values[1].Value.AsExtensionObject());
            return DriverResult<OpcUaDiscoveredDataType>.Success(
                new OpcUaDiscoveredDataType(dataTypeNodeId, browseName, definition));
        }
        catch (Exception ex) when (IsDecodeFailure(ex) || ex is InvalidOperationException)
        {
            return DriverResult<OpcUaDiscoveredDataType>.Failure(new DriverError(
                DriverErrorKind.Data, "UA_DATATYPE_DEFINITION", ex.Message,
                Operation: DriverOperation.Discover, Protocol: OpcUaProtocol.Name,
                Endpoint: _options.EndpointUrl.ToString(),
                Address: dataTypeNodeId.ToString(),
                Cause:
                "The DataTypeDefinition attribute could not be decoded into a supported OPC UA structure or enumeration definition.",
                SuggestedAction:
                "Verify the DataTypeDefinition attribute and binary encoding TypeId; retain the raw ExtensionObject for unsupported vendor definitions.",
                Retry: DriverRetryDisposition.FixConfiguration, Connection: DriverConnectionDisposition.Healthy));
        }
    }

    private static void WriteNullViewDescription(UaBinaryWriter writer)
    {
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, 0));
        writer.WriteDateTime(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc));
        writer.WriteUInt32(0);
    }

    private static void WriteBrowseDescription(UaBinaryWriter writer, OpcUaBrowseDescription item)
    {
        writer.WriteNodeId(item.NodeId);
        writer.WriteUInt32((uint)item.Direction);
        writer.WriteNodeId(item.ReferenceTypeId ?? IndustrialNodeId.Numeric(0, 0));
        writer.WriteBoolean(item.IncludeSubtypes);
        writer.WriteUInt32((uint)item.NodeClassMask);
        writer.WriteUInt32((uint)item.ResultMask);
    }

    private static OpcUaBrowseResult ReadBrowseResult(ref UaBinaryReader reader)
    {
        var statusCode = reader.ReadUInt32();
        var continuationPoint = reader.ReadByteString();
        var count = ReadArrayLength(ref reader, "BrowseResult.References", 18);
        var references = new OpcUaReferenceDescription[count];
        for (var i = 0; i < count; i++)
            references[i] = new OpcUaReferenceDescription(
                reader.ReadNodeId(),
                reader.ReadBoolean(),
                reader.ReadExpandedNodeId(),
                reader.ReadQualifiedName(),
                reader.ReadLocalizedText(),
                (OpcUaNodeClass)reader.ReadUInt32(),
                reader.ReadExpandedNodeId());
        return new OpcUaBrowseResult(statusCode, continuationPoint, references);
    }

    private DriverError BrowseStatusFailure(uint statusCode, IndustrialNodeId nodeId)
    {
        return new DriverError(
            DriverErrorKind.Device,
            "UA_BROWSE_STATUS",
            $"Server returned a Bad StatusCode for Browse of {nodeId}.",
            unchecked((int)statusCode),
            DriverOperation.Browse,
            OpcUaProtocol.Name,
            _options.EndpointUrl.ToString(),
            nodeId.ToString(),
            NativeMeaning: $"OPC UA StatusCode 0x{statusCode:X8}",
            Cause: "The starting node, reference filter or continuation point was rejected by the server.",
            SuggestedAction:
            "Verify the NodeId and Browse filters; retry only after correcting invalid node/reference inputs.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Healthy);
    }
}
