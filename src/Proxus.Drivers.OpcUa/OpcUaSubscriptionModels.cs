namespace Proxus.Drivers.OpcUa;

public readonly record struct OpcUaSubscriptionRequest(
    double RequestedPublishingInterval = 1000,
    uint RequestedLifetimeCount = 60,
    uint RequestedMaxKeepAliveCount = 10,
    uint MaxNotificationsPerPublish = 0,
    bool PublishingEnabled = true,
    byte Priority = 0)
{
    public static OpcUaSubscriptionRequest Default { get; } = new(1000);
}

public readonly record struct OpcUaSubscriptionInfo(
    uint SubscriptionId,
    double RevisedPublishingInterval,
    uint RevisedLifetimeCount,
    uint RevisedMaxKeepAliveCount);

public enum OpcUaMonitoringMode : uint
{
    Disabled = 0,
    Sampling = 1,
    Reporting = 2
}

public readonly record struct OpcUaMonitoredItemRequest(
    OpcUaNodeDescriptor Node,
    uint ClientHandle,
    OpcUaMonitoringMode MonitoringMode = OpcUaMonitoringMode.Reporting,
    double SamplingInterval = -1,
    uint QueueSize = 1,
    bool DiscardOldest = true);

public readonly record struct OpcUaMonitoredItemResult(
    uint StatusCode,
    uint MonitoredItemId,
    double RevisedSamplingInterval,
    uint RevisedQueueSize,
    DriverExtensionObject FilterResult);

public readonly record struct OpcUaMonitoredItemModification(
    uint MonitoredItemId,
    uint ClientHandle,
    double SamplingInterval = -1,
    uint QueueSize = 1,
    bool DiscardOldest = true);

public readonly record struct OpcUaMonitoredItemModificationResult(
    uint StatusCode,
    double RevisedSamplingInterval,
    uint RevisedQueueSize,
    DriverExtensionObject FilterResult);

public readonly record struct OpcUaSubscriptionAcknowledgement(uint SubscriptionId, uint SequenceNumber);

public readonly record struct OpcUaMonitoredValue(uint ClientHandle, DriverSample Sample);

public sealed class OpcUaPublishResponse
{
    public OpcUaPublishResponse(uint subscriptionId, uint[] availableSequenceNumbers, bool moreNotifications,
        uint sequenceNumber,
        DateTime publishTimeUtc, OpcUaMonitoredValue[] values, DriverExtensionObject[] unknownNotifications,
        uint[]? acknowledgementResults = null, OpcUaEventNotification[]? events = null)
    {
        SubscriptionId = subscriptionId;
        AvailableSequenceNumbers = availableSequenceNumbers ??
                                   throw new ArgumentNullException(nameof(availableSequenceNumbers));
        MoreNotifications = moreNotifications;
        SequenceNumber = sequenceNumber;
        PublishTimeUtc = publishTimeUtc;
        Values = values ?? throw new ArgumentNullException(nameof(values));
        UnknownNotifications = unknownNotifications ?? throw new ArgumentNullException(nameof(unknownNotifications));
        AcknowledgementResults = acknowledgementResults ?? [];
        Events = events ?? [];
    }

    public uint SubscriptionId { get; }
    public IReadOnlyList<uint> AvailableSequenceNumbers { get; }
    public bool MoreNotifications { get; }
    public uint SequenceNumber { get; }
    public DateTime PublishTimeUtc { get; }
    public IReadOnlyList<OpcUaMonitoredValue> Values { get; }
    public IReadOnlyList<DriverExtensionObject> UnknownNotifications { get; }
    public IReadOnlyList<uint> AcknowledgementResults { get; }

    /// <summary>Event notifications from event monitored items, in server order.</summary>
    public IReadOnlyList<OpcUaEventNotification> Events { get; }

    public bool IsKeepAlive => SequenceNumber == 0 || (Values.Count == 0 && Events.Count == 0);
}
