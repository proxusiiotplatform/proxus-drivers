namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private const int MaximumSelectClauses = 64;

    /// <summary>
    ///     Creates event monitored items (AttributeId EventNotifier with an EventFilter, OPC 10000-4 §5.12.2 and
    ///     §7.22.3). Event notifications arrive in <see cref="OpcUaPublishResponse.Events" />.
    /// </summary>
    public async ValueTask<DriverResult<OpcUaEventMonitoredItemResult[]>> CreateEventMonitoredItemsAsync(
        uint subscriptionId,
        ReadOnlyMemory<OpcUaEventMonitoredItemRequest> items,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || items.IsEmpty)
            return SubscriptionFailure<OpcUaEventMonitoredItemResult[]>("UA_MONITORED_ITEMS",
                "A non-zero SubscriptionId and at least one event monitored item are required.");
        foreach (var item in items.Span)
        {
            if (item is null || item.Filter.SelectClauses.Count is 0 or > MaximumSelectClauses)
                return SubscriptionFailure<OpcUaEventMonitoredItemResult[]>("UA_EVENT_FILTER",
                    $"An event filter needs between 1 and {MaximumSelectClauses} select clauses.");
            if (item.MonitoringMode is not (OpcUaMonitoringMode.Disabled or OpcUaMonitoringMode.Sampling
                    or OpcUaMonitoringMode.Reporting) || item.QueueSize == 0)
                return SubscriptionFailure<OpcUaEventMonitoredItemResult[]>("UA_MONITORED_ITEM_OPTIONS",
                    "MonitoringMode must be valid and QueueSize must be positive.");
            if (item.Filter.MinimumSeverity is 0 or > 1000)
                return SubscriptionFailure<OpcUaEventMonitoredItemResult[]>("UA_EVENT_FILTER",
                    "MinimumSeverity must be between 1 and 1000.");
        }

        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<OpcUaEventMonitoredItemResult[]>.Failure(error);

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.CreateMonitoredItemsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        writer.WriteUInt32(2); // TimestampsToReturn.Both
        writer.WriteInt32(items.Length);
        foreach (var item in items.Span)
        {
            writer.WriteNodeId(item.Notifier);
            writer.WriteUInt32((uint)OpcUaAttributeId.EventNotifier);
            writer.WriteString(null);
            writer.WriteUInt16(0);
            writer.WriteString(null);
            writer.WriteUInt32((uint)item.MonitoringMode);
            writer.WriteUInt32(item.ClientHandle);
            writer.WriteDouble(0); // events are not sampled
            using (var filter = new UaBinaryWriter(256))
            {
                WriteEventFilter(filter, item.Filter);
                writer.WriteExtensionObject(new DriverExtensionObject(
                    new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.EventFilterBinary)),
                    DriverExtensionObjectEncoding.Binary, filter.WrittenSpan));
            }

            writer.WriteUInt32(item.QueueSize);
            writer.WriteBoolean(item.DiscardOldest);
        }

        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.CreateMonitoredItemsResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaEventMonitoredItemResult[]>.Failure(response.Error);
        try
        {
            var reader = new UaBinaryReader(response.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "CreateMonitoredItems.Results", 23);
            if (count != items.Length)
                throw new InvalidDataException(
                    $"CreateMonitoredItems returned {count} results for {items.Length} event items.");
            var results = new OpcUaEventMonitoredItemResult[count];
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                var id = reader.ReadUInt32();
                _ = reader.ReadDouble();
                var queue = reader.ReadUInt32();
                var filterResult = reader.ReadExtensionObject();
                results[i] = new OpcUaEventMonitoredItemResult(status, id, queue, ReadSelectClauseResults(filterResult));
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaEventMonitoredItemResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaEventMonitoredItemResult[]>.Failure(Failure(DriverErrorKind.Protocol,
                "UA_CREATE_MONITORED_ITEMS_DECODE", ex.Message, DriverOperation.Subscribe,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    /// <summary>
    ///     Asks the server to resend the current state of every condition to the subscription (ConditionRefresh,
    ///     OPC 10000-9 §5.5.7). The server brackets the resent events with RefreshStartEventType and
    ///     RefreshEndEventType events.
    /// </summary>
    public async ValueTask<DriverResult> RefreshConditionsAsync(uint subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var call = await CallAsync(IndustrialNodeId.Numeric(0, OpcUaProtocol.ConditionType),
            IndustrialNodeId.Numeric(0, OpcUaProtocol.ConditionRefreshMethod),
            new[] { DriverValue.FromUInt32(subscriptionId) }, cancellationToken).ConfigureAwait(false);
        return CallStatus(call, "UA_CONDITION_REFRESH");
    }

    /// <summary>
    ///     Acknowledges a condition event (AcknowledgeableConditionType.Acknowledge, OPC 10000-9 §5.7.3).
    ///     <paramref name="conditionId" /> comes from <see cref="OpcUaEventField.ConditionId" /> and
    ///     <paramref name="eventId" /> from the EventId field of the event being acknowledged.
    /// </summary>
    public async ValueTask<DriverResult> AcknowledgeConditionAsync(IndustrialNodeId conditionId,
        ReadOnlyMemory<byte> eventId, IndustrialLocalizedText comment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conditionId);
        if (eventId.IsEmpty)
            return DriverResult.Failure(Failure(DriverErrorKind.Configuration, "UA_CONDITION_EVENT_ID",
                "Acknowledge needs the EventId of the event being acknowledged.", DriverOperation.Execute,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
        var call = await CallAsync(conditionId, IndustrialNodeId.Numeric(0, OpcUaProtocol.AcknowledgeMethod),
            new[] { DriverValue.FromBytes(eventId.Span), DriverValue.FromLocalizedText(comment) }, cancellationToken)
            .ConfigureAwait(false);
        return CallStatus(call, "UA_CONDITION_ACKNOWLEDGE");
    }

    private DriverResult CallStatus(DriverResult<OpcUaMethodResult> call, string code)
    {
        if (!call.IsSuccess) return DriverResult.Failure(call.Error);
        if (call.Value.IsGood) return DriverResult.Success();
        return DriverResult.Failure(new DriverError(DriverErrorKind.Device, code,
            $"The server rejected the method with StatusCode 0x{call.Value.StatusCode:X8}.",
            unchecked((int)call.Value.StatusCode), DriverOperation.Execute, OpcUaProtocol.Name,
            _options.EndpointUrl.ToString(), NativeMeaning: $"OPC UA StatusCode 0x{call.Value.StatusCode:X8}",
            Cause: "The condition or subscription is unknown, already in the requested state, or not acknowledgeable.",
            SuggestedAction: "Check the condition state and the EventId; refresh conditions if the state is stale.",
            Retry: DriverRetryDisposition.DoNotRetry, Connection: DriverConnectionDisposition.Healthy));
    }

    /// <summary>
    ///     EventFilter body (OPC 10000-4 §7.22.3): select clauses, then a ContentFilter (§7.7) for
    ///     (OfType(t1) OR OfType(t2) ...) AND Severity &gt;= minimum. And and Or take exactly two operands, so
    ///     several types nest to the right. Element 0 is the root; children are referenced by ElementOperand.
    /// </summary>
    internal static void WriteEventFilter(UaBinaryWriter writer, OpcUaEventFilter filter)
    {
        writer.WriteInt32(filter.SelectClauses.Count);
        foreach (var field in filter.SelectClauses) WriteSimpleAttributeOperand(writer, field);

        FilterNode? types = null;
        for (var i = filter.EventTypes.Count - 1; i >= 0; i--)
        {
            var ofType = FilterNode.Leaf(14, LiteralOperand(DriverValue.FromNodeId(filter.EventTypes[i])));
            types = types is null ? ofType : FilterNode.Branch(11, ofType, types); // Or
        }

        FilterNode? severity = filter.MinimumSeverity is { } minimum
            ? FilterNode.Leaf(4, SimpleAttributeOperand(OpcUaEventField.Property("Severity")),
                LiteralOperand(DriverValue.FromUInt16(minimum))) // GreaterThanOrEqual
            : null;
        var root = types is not null && severity is not null ? FilterNode.Branch(10, types, severity) : types ?? severity;

        var elements = new List<(uint Operator, DriverExtensionObject[] Operands)>();
        if (root is not null) Emit(root, elements);
        writer.WriteInt32(elements.Count);
        foreach (var (filterOperator, operands) in elements) WriteElement(writer, filterOperator, operands);
    }

    private static int Emit(FilterNode node, List<(uint Operator, DriverExtensionObject[] Operands)> elements)
    {
        var index = elements.Count;
        elements.Add(default);
        if (node.Children is null)
        {
            elements[index] = (node.Operator, node.Operands!);
            return index;
        }

        var operands = new DriverExtensionObject[node.Children.Length];
        for (var i = 0; i < operands.Length; i++) operands[i] = ElementOperand(Emit(node.Children[i], elements));
        elements[index] = (node.Operator, operands);
        return index;
    }

    private sealed record FilterNode(uint Operator, FilterNode[]? Children, DriverExtensionObject[]? Operands)
    {
        public static FilterNode Leaf(uint filterOperator, params DriverExtensionObject[] operands) =>
            new(filterOperator, null, operands);

        public static FilterNode Branch(uint filterOperator, FilterNode left, FilterNode right) =>
            new(filterOperator, [left, right], null);
    }

    private static void WriteElement(UaBinaryWriter writer, uint filterOperator, params DriverExtensionObject[] operands)
    {
        writer.WriteUInt32(filterOperator);
        writer.WriteInt32(operands.Length);
        foreach (var operand in operands) writer.WriteExtensionObject(operand);
    }

    private static DriverExtensionObject ElementOperand(int index)
    {
        using var body = new UaBinaryWriter(8);
        body.WriteUInt32(checked((uint)index));
        return Operand(OpcUaProtocol.ElementOperandBinary, body);
    }

    private static DriverExtensionObject LiteralOperand(DriverValue value)
    {
        using var body = new UaBinaryWriter(32);
        OpcUaBinaryCodec.WriteVariant(body, value);
        return Operand(OpcUaProtocol.LiteralOperandBinary, body);
    }

    private static DriverExtensionObject SimpleAttributeOperand(OpcUaEventField field)
    {
        using var body = new UaBinaryWriter(64);
        WriteSimpleAttributeOperand(body, field);
        return Operand(OpcUaProtocol.SimpleAttributeOperandBinary, body);
    }

    private static DriverExtensionObject Operand(uint typeId, UaBinaryWriter body) => new(
        new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, typeId)), DriverExtensionObjectEncoding.Binary,
        body.WrittenSpan);

    private static void WriteSimpleAttributeOperand(UaBinaryWriter writer, OpcUaEventField field)
    {
        writer.WriteNodeId(field.TypeDefinitionId);
        writer.WriteInt32(field.BrowsePath.Count);
        foreach (var name in field.BrowsePath) writer.WriteQualifiedName(name);
        writer.WriteUInt32((uint)field.AttributeId);
        writer.WriteString(null);
    }

    // EventFilterResult: selectClauseResults, selectClauseDiagnosticInfos, whereClauseResult (not exposed).
    private static uint[] ReadSelectClauseResults(DriverExtensionObject filterResult)
    {
        if (filterResult.Encoding != DriverExtensionObjectEncoding.Binary ||
            filterResult.TypeId.NodeId is not
            { NamespaceIndex: 0, Kind: IndustrialNodeIdKind.Numeric, NumericIdentifier: OpcUaProtocol.EventFilterResultBinary })
            return [];
        var reader = new UaBinaryReader(filterResult.EncodedBody.Span);
        var count = ReadArrayLength(ref reader, "EventFilterResult.SelectClauseResults", 4);
        var results = new uint[count];
        for (var i = 0; i < count; i++) results[i] = reader.ReadUInt32();
        return results;
    }

    private static void ReadEventNotificationList(ReadOnlySpan<byte> body, ICollection<OpcUaEventNotification> events)
    {
        var reader = new UaBinaryReader(body);
        var count = ReadArrayLength(ref reader, "EventNotificationList.Events", 8);
        for (var i = 0; i < count; i++)
        {
            var clientHandle = reader.ReadUInt32();
            var fieldCount = ReadArrayLength(ref reader, "EventFieldList.EventFields", 1);
            var fields = new DriverValue[fieldCount];
            for (var f = 0; f < fieldCount; f++) fields[f] = OpcUaBinaryCodec.ReadVariant(ref reader);
            events.Add(new OpcUaEventNotification(clientHandle, fields));
        }
    }
}
