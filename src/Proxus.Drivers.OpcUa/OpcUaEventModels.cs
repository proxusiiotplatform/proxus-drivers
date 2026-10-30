namespace Proxus.Drivers.OpcUa;

/// <summary>
///     One event field to return (a SimpleAttributeOperand select clause, OPC 10000-4 §7.4.4.5): the
///     BrowseName path from the event type to the property, read with <see cref="AttributeId" />.
/// </summary>
public sealed class OpcUaEventField
{
    public OpcUaEventField(IndustrialNodeId typeDefinitionId, OpcUaAttributeId attributeId,
        params IndustrialQualifiedName[] browsePath)
    {
        TypeDefinitionId = typeDefinitionId ?? throw new ArgumentNullException(nameof(typeDefinitionId));
        AttributeId = attributeId;
        ArgumentNullException.ThrowIfNull(browsePath);
        BrowsePath = [.. browsePath];
    }

    public IndustrialNodeId TypeDefinitionId { get; }
    public OpcUaAttributeId AttributeId { get; }
    public IReadOnlyList<IndustrialQualifiedName> BrowsePath { get; }

    /// <summary>A BaseEventType property path in namespace 0, for example <c>Property("Severity")</c>.</summary>
    public static OpcUaEventField Property(params string[] browseNames)
    {
        ArgumentNullException.ThrowIfNull(browseNames);
        return new OpcUaEventField(IndustrialNodeId.Numeric(0, OpcUaProtocol.BaseEventType), OpcUaAttributeId.Value,
            [.. browseNames.Select(static name => new IndustrialQualifiedName(0, name))]);
    }

    /// <summary>
    ///     The ConditionId of a condition event (the NodeId of the ConditionType instance, OPC 10000-9 §5.5.2).
    ///     Select it to acknowledge alarms; it is null for events that are not conditions.
    /// </summary>
    public static OpcUaEventField ConditionId { get; } =
        new(IndustrialNodeId.Numeric(0, OpcUaProtocol.ConditionType), OpcUaAttributeId.NodeId);
}

/// <summary>
///     EventFilter for an event monitored item (OPC 10000-4 §7.22.3): the fields each notification carries,
///     optionally restricted to some event types (OfType, including subtypes) and a minimum Severity.
/// </summary>
public sealed class OpcUaEventFilter
{
    public OpcUaEventFilter(params OpcUaEventField[] selectClauses)
    {
        ArgumentNullException.ThrowIfNull(selectClauses);
        SelectClauses = [.. selectClauses];
    }

    public IReadOnlyList<OpcUaEventField> SelectClauses { get; }

    /// <summary>Only events of these types or their subtypes; empty for every event type.</summary>
    public IReadOnlyList<IndustrialNodeId> EventTypes { get; init; } = [];

    /// <summary>Only events whose Severity is at least this value (1 to 1000).</summary>
    public ushort? MinimumSeverity { get; init; }

    /// <summary>
    ///     EventId, EventType, SourceNode, SourceName, Time, ReceiveTime, Message and Severity, in that order
    ///     (indexes 0 to 7 of <see cref="OpcUaEventNotification.Fields" />).
    /// </summary>
    public static OpcUaEventFilter BaseEventFields() => new(
        OpcUaEventField.Property("EventId"), OpcUaEventField.Property("EventType"),
        OpcUaEventField.Property("SourceNode"), OpcUaEventField.Property("SourceName"),
        OpcUaEventField.Property("Time"), OpcUaEventField.Property("ReceiveTime"),
        OpcUaEventField.Property("Message"), OpcUaEventField.Property("Severity"));
}

/// <summary>An event monitored item on an object whose EventNotifier attribute subscribes to events.</summary>
public sealed class OpcUaEventMonitoredItemRequest
{
    public OpcUaEventMonitoredItemRequest(IndustrialNodeId notifier, uint clientHandle, OpcUaEventFilter filter)
    {
        Notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        ClientHandle = clientHandle;
        Filter = filter ?? throw new ArgumentNullException(nameof(filter));
    }

    /// <summary>The event notifier, usually the Server object (i=2253) or an area or source object.</summary>
    public IndustrialNodeId Notifier { get; }

    public uint ClientHandle { get; }
    public OpcUaEventFilter Filter { get; }
    public OpcUaMonitoringMode MonitoringMode { get; init; } = OpcUaMonitoringMode.Reporting;

    /// <summary>Events the server queues between Publish responses; events are not sampled.</summary>
    public uint QueueSize { get; init; } = 1000;

    public bool DiscardOldest { get; init; } = true;
}

/// <summary>
///     CreateMonitoredItems result for an event item. <see cref="SelectClauseResults" /> holds one StatusCode
///     per select clause when the server reports problems (for example Bad_BrowseNameInvalid); it is empty
///     when every clause was accepted.
/// </summary>
public sealed class OpcUaEventMonitoredItemResult
{
    public OpcUaEventMonitoredItemResult(uint statusCode, uint monitoredItemId, uint revisedQueueSize,
        uint[] selectClauseResults)
    {
        StatusCode = statusCode;
        MonitoredItemId = monitoredItemId;
        RevisedQueueSize = revisedQueueSize;
        SelectClauseResults = selectClauseResults ?? throw new ArgumentNullException(nameof(selectClauseResults));
    }

    public uint StatusCode { get; }
    public bool IsGood => (StatusCode & 0xC0000000u) == 0;
    public uint MonitoredItemId { get; }
    public uint RevisedQueueSize { get; }
    public IReadOnlyList<uint> SelectClauseResults { get; }
}

/// <summary>One event: the monitored item's client handle and the selected fields in select-clause order.</summary>
public sealed class OpcUaEventNotification
{
    public OpcUaEventNotification(uint clientHandle, DriverValue[] fields)
    {
        ClientHandle = clientHandle;
        Fields = fields ?? throw new ArgumentNullException(nameof(fields));
    }

    public uint ClientHandle { get; }
    public IReadOnlyList<DriverValue> Fields { get; }
}
