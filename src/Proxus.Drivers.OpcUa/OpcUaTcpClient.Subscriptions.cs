using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private readonly ConcurrentDictionary<uint, OpcUaSubscription> _subscriptionRuntimes = new();

    // Subscriptions created in the current Session, so a reactivated Session can delete those nobody pumps any more.
    private readonly ConcurrentDictionary<uint, byte> _sessionSubscriptions = new();

    // After a Session was activated again, its subscriptions without a running pump would send notifications no runtime
    // can route; they are deleted. The result is ignored: the server deletes them with the Session in any case.
    private async ValueTask DeleteOrphanedSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var orphans = _sessionSubscriptions.Keys.Where(id => !_subscriptionRuntimes.ContainsKey(id)).ToArray();
        if (orphans.Length == 0) return;
        await DeleteSubscriptionsAsync(orphans, cancellationToken).ConfigureAwait(false);
        foreach (var id in orphans)
            _sessionSubscriptions.TryRemove(id, out _);
    }

    bool IOpcUaSubscriptionRuntimeRouter.RegisterSubscriptionRuntime(uint subscriptionId,
        OpcUaSubscription subscription)
    {
        return _subscriptionRuntimes.TryAdd(subscriptionId, subscription);
    }

    void IOpcUaSubscriptionRuntimeRouter.UnregisterSubscriptionRuntime(uint subscriptionId,
        OpcUaSubscription subscription)
    {
        _subscriptionRuntimes.TryRemove(new KeyValuePair<uint, OpcUaSubscription>(subscriptionId, subscription));
    }

    bool IOpcUaSubscriptionRuntimeRouter.UpdateSubscriptionRuntime(uint previousSubscriptionId,
        uint revisedSubscriptionId, OpcUaSubscription subscription)
    {
        _subscriptionRuntimes.TryRemove(
            new KeyValuePair<uint, OpcUaSubscription>(previousSubscriptionId, subscription));
        if (_subscriptionRuntimes.TryAdd(revisedSubscriptionId, subscription)) return true;
        _subscriptionRuntimes.TryAdd(previousSubscriptionId, subscription);
        return false;
    }

    bool IOpcUaSubscriptionRuntimeRouter.TryRoutePublishResponse(OpcUaSubscription source,
        OpcUaPublishResponse response)
    {
        return _subscriptionRuntimes.TryGetValue(response.SubscriptionId, out var target) &&
               !ReferenceEquals(source, target) && target.TryRoute(response);
    }

    Uri IOpcUaSubscriptionRuntimeTransport.Endpoint => _options.EndpointUrl;

    ValueTask<DriverResult<OpcUaSubscriptionInfo>> IOpcUaSubscriptionRuntimeTransport.RecreateManagedSubscriptionAsync(
        OpcUaManagedSubscriptionPlan plan, CancellationToken cancellationToken)
    {
        return RecreateManagedSubscriptionAsync(plan, cancellationToken);
    }

    public async ValueTask<DriverResult<OpcUaPublishResponse>> PublishAsync(
        ReadOnlyMemory<OpcUaSubscriptionAcknowledgement> acknowledgements,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<OpcUaPublishResponse>.Failure(error);
        foreach (var acknowledgement in acknowledgements.Span)
            if (acknowledgement.SubscriptionId == 0 || acknowledgement.SequenceNumber == 0)
                return SubscriptionFailure<OpcUaPublishResponse>("UA_PUBLISH_ACK_OPTIONS",
                    "Publish acknowledgements require non-zero SubscriptionId and SequenceNumber.",
                    DriverOperation.Publish);
        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.PublishRequestBinary, session.AuthenticationToken,
            requestId, requestId, _options.PublishRequestTimeout);
        writer.WriteInt32(acknowledgements.Length);
        foreach (var acknowledgement in acknowledgements.Span)
        {
            writer.WriteUInt32(acknowledgement.SubscriptionId);
            writer.WriteUInt32(acknowledgement.SequenceNumber);
        }

        var response = await SendServiceAsync(socket, writer, requestId, requestId, OpcUaProtocol.PublishResponseBinary,
            DriverOperation.Publish, cancellationToken, _options.PublishRequestTimeout).ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaPublishResponse>.Failure(response.Error);
        try
        {
            return DriverResult<OpcUaPublishResponse>.Success(ReadPublishResponse(response.Value.Body.Span));
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DecodeFailure<OpcUaPublishResponse>("UA_PUBLISH_DECODE", ex, DriverOperation.Publish);
        }
    }

    public async ValueTask<DriverResult<OpcUaPublishResponse>> RepublishAsync(uint subscriptionId,
        uint retransmitSequenceNumber,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || retransmitSequenceNumber == 0)
            return SubscriptionFailure<OpcUaPublishResponse>("UA_REPUBLISH_OPTIONS",
                "SubscriptionId and retransmit SequenceNumber must be non-zero.", DriverOperation.Republish);
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<OpcUaPublishResponse>.Failure(error);
        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.RepublishRequestBinary, session.AuthenticationToken,
            requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        writer.WriteUInt32(retransmitSequenceNumber);
        var response = await SendServiceAsync(socket, writer, requestId, requestId,
            OpcUaProtocol.RepublishResponseBinary, DriverOperation.Republish, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaPublishResponse>.Failure(response.Error);
        try
        {
            return DriverResult<OpcUaPublishResponse>.Success(ReadRepublishResponse(subscriptionId,
                response.Value.Body.Span));
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DecodeFailure<OpcUaPublishResponse>("UA_REPUBLISH_DECODE", ex, DriverOperation.Republish);
        }
    }

    public ValueTask<DriverResult<OpcUaSubscription>> StartSubscriptionPumpAsync(uint subscriptionId,
        OpcUaSubscriptionRuntimeOptions? options = null)
    {
        if (subscriptionId == 0)
            return ValueTask.FromResult(
                SubscriptionFailure<OpcUaSubscription>("UA_SUBSCRIPTION_ID", "SubscriptionId must be non-zero."));
        options ??= new OpcUaSubscriptionRuntimeOptions();
        var validation = options.Validate(_options.EndpointUrl);
        if (!validation.IsSuccess)
            return ValueTask.FromResult(DriverResult<OpcUaSubscription>.Failure(validation.Error));
        if (!IsSessionActive || _socket is null || SecureChannel is null)
            return ValueTask.FromResult(DriverResult<OpcUaSubscription>.Failure(
                SessionServiceUnavailable(DriverOperation.Subscribe, "subscription processing")));
        var subscription = new OpcUaSubscription(this, subscriptionId, options);
        subscription.Start();
        return ValueTask.FromResult(DriverResult<OpcUaSubscription>.Success(subscription));
    }

    public async ValueTask<DriverResult<OpcUaSubscription>> CreateManagedSubscriptionAsync(
        OpcUaManagedSubscriptionPlan plan,
        OpcUaSubscriptionRuntimeOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.MonitoredItems.Count == 0)
            return SubscriptionFailure<OpcUaSubscription>("UA_MANAGED_SUBSCRIPTION_ITEMS",
                "A managed subscription requires at least one monitored item.");
        options ??= new OpcUaSubscriptionRuntimeOptions();
        var validation = options.Validate(_options.EndpointUrl);
        if (!validation.IsSuccess) return DriverResult<OpcUaSubscription>.Failure(validation.Error);
        var created = await CreateSubscriptionAsync(plan.Subscription, cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess) return DriverResult<OpcUaSubscription>.Failure(created.Error);
        var monitored =
            await CreateMonitoredItemsAsync(created.Value.SubscriptionId, plan.MonitoredItems.ToArray(),
                cancellationToken).ConfigureAwait(false);
        if (!monitored.IsSuccess ||
            monitored.Value.Any(static item => (item.StatusCode & 0x80000000u) != 0 || item.MonitoredItemId == 0))
        {
            await DeleteSubscriptionsAsync(new[] { created.Value.SubscriptionId }, cancellationToken)
                .ConfigureAwait(false);
            return monitored.IsSuccess
                ? DriverResult<OpcUaSubscription>.Failure(Failure(DriverErrorKind.Device,
                    "UA_MANAGED_MONITORED_ITEM_STATUS", "Server rejected one or more managed monitored items.",
                    DriverOperation.Subscribe).Error)
                : DriverResult<OpcUaSubscription>.Failure(monitored.Error);
        }

        var subscription = new OpcUaSubscription(this, created.Value.SubscriptionId, options, plan);
        subscription.Start();
        return DriverResult<OpcUaSubscription>.Success(subscription);
    }

    internal async ValueTask<DriverResult<OpcUaSubscriptionInfo>> RecreateManagedSubscriptionAsync(
        OpcUaManagedSubscriptionPlan plan,
        CancellationToken cancellationToken)
    {
        var connect = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!connect.IsSuccess) return DriverResult<OpcUaSubscriptionInfo>.Failure(connect.Error);
        var session = await OpenAnonymousSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!session.IsSuccess) return DriverResult<OpcUaSubscriptionInfo>.Failure(session.Error);
        var created = await CreateSubscriptionAsync(plan.Subscription, cancellationToken).ConfigureAwait(false);
        if (!created.IsSuccess) return created;
        var monitored =
            await CreateMonitoredItemsAsync(created.Value.SubscriptionId, plan.MonitoredItems.ToArray(),
                cancellationToken).ConfigureAwait(false);
        if (monitored.IsSuccess &&
            monitored.Value.All(static item => (item.StatusCode & 0x80000000u) == 0 && item.MonitoredItemId != 0))
            return created;
        await DeleteSubscriptionsAsync(new[] { created.Value.SubscriptionId }, cancellationToken).ConfigureAwait(false);
        return monitored.IsSuccess
            ? DriverResult<OpcUaSubscriptionInfo>.Failure(Failure(DriverErrorKind.Device,
                "UA_MANAGED_MONITORED_ITEM_STATUS", "Server rejected one or more recreated monitored items.",
                DriverOperation.Subscribe).Error)
            : DriverResult<OpcUaSubscriptionInfo>.Failure(monitored.Error);
    }

    public async ValueTask<DriverResult<OpcUaSubscriptionInfo>> CreateSubscriptionAsync(
        OpcUaSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (request == default) request = OpcUaSubscriptionRequest.Default;
        if (_socket is null || SecureChannel is null || !IsSessionActive || Session is not { } session)
            return DriverResult<OpcUaSubscriptionInfo>.Failure(
                SessionServiceUnavailable(DriverOperation.Subscribe, "CreateSubscription"));
        if (request.RequestedPublishingInterval <= 0 || double.IsNaN(request.RequestedPublishingInterval) ||
            double.IsInfinity(request.RequestedPublishingInterval) || request.RequestedLifetimeCount == 0 ||
            request.RequestedMaxKeepAliveCount == 0)
            return DriverResult<OpcUaSubscriptionInfo>.Failure(Failure(DriverErrorKind.Configuration,
                "UA_SUBSCRIPTION_OPTIONS", "Publishing interval, lifetime count and keep-alive count must be positive.",
                DriverOperation.Subscribe,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
        var socket = _socket;
        if (socket is null)
            return DriverResult<OpcUaSubscriptionInfo>.Failure(Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
                "SecureChannel is not available.", DriverOperation.Subscribe,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.CreateSubscriptionRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteDouble(request.RequestedPublishingInterval);
        writer.WriteUInt32(request.RequestedLifetimeCount);
        writer.WriteUInt32(request.RequestedMaxKeepAliveCount);
        writer.WriteUInt32(request.MaxNotificationsPerPublish);
        writer.WriteBoolean(request.PublishingEnabled);
        writer.WriteByte(request.Priority);
        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.CreateSubscriptionResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaSubscriptionInfo>.Failure(response.Error);
        try
        {
            var reader = new UaBinaryReader(response.Value.Body.Span);
            var id = reader.ReadUInt32();
            var interval = reader.ReadDouble();
            var lifetime = reader.ReadUInt32();
            var keepAlive = reader.ReadUInt32();
            if (id == 0 || interval <= 0 || lifetime == 0 || keepAlive == 0)
                throw new InvalidDataException("CreateSubscription returned invalid revised parameters.");
            _sessionSubscriptions.TryAdd(id, 0);
            return DriverResult<OpcUaSubscriptionInfo>.Success(new OpcUaSubscriptionInfo(id, interval, lifetime,
                keepAlive));
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaSubscriptionInfo>.Failure(Failure(DriverErrorKind.Protocol,
                "UA_CREATE_SUBSCRIPTION_DECODE", ex.Message, DriverOperation.Subscribe,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    public async ValueTask<DriverResult<OpcUaMonitoredItemResult[]>> CreateMonitoredItemsAsync(
        uint subscriptionId, ReadOnlyMemory<OpcUaMonitoredItemRequest> items,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || items.IsEmpty)
            return DriverResult<OpcUaMonitoredItemResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                "UA_MONITORED_ITEMS", "A non-zero SubscriptionId and at least one monitored item are required.",
                DriverOperation.Subscribe,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
        if (SecureChannel is null || !IsSessionActive || Session is not { } session || _socket is not { } socket)
            return DriverResult<OpcUaMonitoredItemResult[]>.Failure(
                SessionServiceUnavailable(DriverOperation.Subscribe, "CreateMonitoredItems"));
        foreach (var item in items.Span)
            if (item.MonitoringMode is not (OpcUaMonitoringMode.Disabled or OpcUaMonitoringMode.Sampling
                    or OpcUaMonitoringMode.Reporting) || item.QueueSize == 0)
                return DriverResult<OpcUaMonitoredItemResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_MONITORED_ITEM_OPTIONS", "MonitoringMode must be valid and QueueSize must be positive.",
                    DriverOperation.Subscribe,
                    DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.CreateMonitoredItemsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        writer.WriteUInt32(2);
        writer.WriteInt32(items.Length);
        foreach (var item in items.Span)
        {
            WriteReadValueIdPrefix(writer, item.Node);
            writer.WriteUInt32((uint)item.MonitoringMode);
            writer.WriteUInt32(item.ClientHandle);
            writer.WriteDouble(item.SamplingInterval);
            writer.WriteExtensionObject(null);
            writer.WriteUInt32(item.QueueSize);
            writer.WriteBoolean(item.DiscardOldest);
        }

        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.CreateMonitoredItemsResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaMonitoredItemResult[]>.Failure(response.Error);
        try
        {
            var reader = new UaBinaryReader(response.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "CreateMonitoredItems.Results", 23);
            if (count != items.Length)
                throw new InvalidDataException(
                    $"CreateMonitoredItems returned {count} results for {items.Length} items.");
            var results = new OpcUaMonitoredItemResult[count];
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                var id = reader.ReadUInt32();
                var interval = reader.ReadDouble();
                var queue = reader.ReadUInt32();
                var filter = reader.ReadExtensionObject();
                results[i] = new OpcUaMonitoredItemResult(status, id, interval, queue, filter);
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaMonitoredItemResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaMonitoredItemResult[]>.Failure(Failure(DriverErrorKind.Protocol,
                "UA_CREATE_MONITORED_ITEMS_DECODE", ex.Message, DriverOperation.Subscribe,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    public async ValueTask<DriverResult<OpcUaMonitoredItemModificationResult[]>> ModifyMonitoredItemsAsync(
        uint subscriptionId, ReadOnlyMemory<OpcUaMonitoredItemModification> items,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || items.IsEmpty)
            return SubscriptionFailure<OpcUaMonitoredItemModificationResult[]>("UA_MONITORED_ITEMS",
                "A non-zero SubscriptionId and at least one monitored item are required.");
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<OpcUaMonitoredItemModificationResult[]>.Failure(error);
        foreach (var item in items.Span)
            if (item.MonitoredItemId == 0 || item.QueueSize == 0 || double.IsNaN(item.SamplingInterval) ||
                double.IsInfinity(item.SamplingInterval))
                return SubscriptionFailure<OpcUaMonitoredItemModificationResult[]>("UA_MONITORED_ITEM_OPTIONS",
                    "MonitoredItemId and QueueSize must be positive and SamplingInterval must be finite.");

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.ModifyMonitoredItemsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        writer.WriteUInt32(2);
        writer.WriteInt32(items.Length);
        foreach (var item in items.Span)
        {
            writer.WriteUInt32(item.MonitoredItemId);
            writer.WriteUInt32(item.ClientHandle);
            writer.WriteDouble(item.SamplingInterval);
            writer.WriteExtensionObject(null);
            writer.WriteUInt32(item.QueueSize);
            writer.WriteBoolean(item.DiscardOldest);
        }

        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.ModifyMonitoredItemsResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<OpcUaMonitoredItemModificationResult[]>.Failure(response.Error);
        try
        {
            var reader = new UaBinaryReader(response.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "ModifyMonitoredItems.Results", 19);
            if (count != items.Length)
                throw new InvalidDataException(
                    $"ModifyMonitoredItems returned {count} results for {items.Length} items.");
            var results = new OpcUaMonitoredItemModificationResult[count];
            for (var i = 0; i < count; i++)
                results[i] = new OpcUaMonitoredItemModificationResult(reader.ReadUInt32(), reader.ReadDouble(),
                    reader.ReadUInt32(), reader.ReadExtensionObject());
            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaMonitoredItemModificationResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DecodeFailure<OpcUaMonitoredItemModificationResult[]>("UA_MODIFY_MONITORED_ITEMS_DECODE", ex);
        }
    }

    public async ValueTask<DriverResult<uint[]>> SetMonitoringModeAsync(uint subscriptionId, OpcUaMonitoringMode mode,
        ReadOnlyMemory<uint> monitoredItemIds, CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || monitoredItemIds.IsEmpty || mode is not (OpcUaMonitoringMode.Disabled
                or OpcUaMonitoringMode.Sampling or OpcUaMonitoringMode.Reporting))
            return SubscriptionFailure<uint[]>("UA_MONITORING_MODE_OPTIONS",
                "SubscriptionId, MonitoringMode and monitored item identifiers must be valid.");
        if (monitoredItemIds.Span.IndexOf(0u) >= 0)
            return SubscriptionFailure<uint[]>("UA_MONITORING_MODE_ITEMS",
                "Monitored item identifiers must be non-zero.");
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<uint[]>.Failure(error);
        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.SetMonitoringModeRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        writer.WriteUInt32((uint)mode);
        WriteUInt32Array(writer, monitoredItemIds.Span);
        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.SetMonitoringModeResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccess
            ? ReadStatusResults(response.Value.Body.Span, monitoredItemIds.Length, "SetMonitoringMode",
                DriverOperation.Subscribe)
            : DriverResult<uint[]>.Failure(response.Error);
    }

    public async ValueTask<DriverResult<uint[]>> DeleteMonitoredItemsAsync(uint subscriptionId,
        ReadOnlyMemory<uint> monitoredItemIds,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionId == 0 || monitoredItemIds.IsEmpty || monitoredItemIds.Span.IndexOf(0u) >= 0)
            return SubscriptionFailure<uint[]>("UA_DELETE_MONITORED_ITEMS_OPTIONS",
                "SubscriptionId and monitored item identifiers must be non-zero.");
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<uint[]>.Failure(error);
        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.DeleteMonitoredItemsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        writer.WriteUInt32(subscriptionId);
        WriteUInt32Array(writer, monitoredItemIds.Span);
        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.DeleteMonitoredItemsResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        return response.IsSuccess
            ? ReadStatusResults(response.Value.Body.Span, monitoredItemIds.Length, "DeleteMonitoredItems",
                DriverOperation.Subscribe)
            : DriverResult<uint[]>.Failure(response.Error);
    }

    public async ValueTask<DriverResult<uint[]>> DeleteSubscriptionsAsync(ReadOnlyMemory<uint> subscriptionIds,
        CancellationToken cancellationToken = default)
    {
        if (subscriptionIds.IsEmpty || subscriptionIds.Span.IndexOf(0u) >= 0)
            return SubscriptionFailure<uint[]>("UA_DELETE_SUBSCRIPTIONS_OPTIONS",
                "At least one non-zero SubscriptionId is required.");
        if (!TryGetSubscriptionSession(out var session, out var socket, out var error))
            return DriverResult<uint[]>.Failure(error);
        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.DeleteSubscriptionsRequestBinary,
            session.AuthenticationToken, requestId, requestId);
        WriteUInt32Array(writer, subscriptionIds.Span);
        var response = await SendServiceAsync(socket, writer, requestId, requestId,
                OpcUaProtocol.DeleteSubscriptionsResponseBinary, DriverOperation.Subscribe, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccess) return DriverResult<uint[]>.Failure(response.Error);
        foreach (var id in subscriptionIds.Span)
            _sessionSubscriptions.TryRemove(id, out _);
        return ReadStatusResults(response.Value.Body.Span, subscriptionIds.Length, "DeleteSubscriptions", DriverOperation.Subscribe);
    }

    private bool TryGetSubscriptionSession(out OpcUaSessionInfo session, out Socket socket, out DriverError error)
    {
        if (IsSessionActive && Session is { } activeSession && _socket is { } activeSocket)
        {
            session = activeSession;
            socket = activeSocket;
            error = DriverError.None;
            return true;
        }

        session = default;
        socket = null!;
        error = SessionServiceUnavailable(DriverOperation.Subscribe, "subscription services");
        return false;
    }

    private DriverResult<T> SubscriptionFailure<T>(string code, string message,
        DriverOperation operation = DriverOperation.Subscribe)
    {
        return DriverResult<T>.Failure(Failure(DriverErrorKind.Configuration, code, message, operation,
            DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Unchanged).Error);
    }

    private DriverResult<T> DecodeFailure<T>(string code, Exception ex,
        DriverOperation operation = DriverOperation.Subscribe)
    {
        return DriverResult<T>.Failure(Failure(DriverErrorKind.Protocol, code, ex.Message, operation,
            DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
    }

    private DriverResult<uint[]> ReadStatusResults(ReadOnlySpan<byte> body, int expectedCount, string service,
        DriverOperation operation)
    {
        try
        {
            var reader = new UaBinaryReader(body);
            var count = ReadArrayLength(ref reader, $"{service}.Results", 4);
            if (count != expectedCount)
                throw new InvalidDataException(
                    $"{service} returned {count} results for {expectedCount} requested identifiers.");
            var results = new uint[count];
            for (var i = 0; i < count; i++) results[i] = reader.ReadUInt32();
            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<uint[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DecodeFailure<uint[]>($"UA_{service.ToUpperInvariant()}_DECODE", ex, operation);
        }
    }

    private static void WriteUInt32Array(UaBinaryWriter writer, ReadOnlySpan<uint> values)
    {
        writer.WriteInt32(values.Length);
        foreach (var value in values) writer.WriteUInt32(value);
    }

    private static OpcUaPublishResponse ReadPublishResponse(ReadOnlySpan<byte> body)
    {
        var reader = new UaBinaryReader(body);
        var subscriptionId = reader.ReadUInt32();
        if (subscriptionId == 0) throw new InvalidDataException("Publish returned an invalid SubscriptionId.");
        var available = ReadUInt32Array(ref reader, "Publish.AvailableSequenceNumbers");
        var moreNotifications = reader.ReadBoolean();
        var message = ReadNotificationMessage(ref reader);
        var acknowledgementResults = ReadUInt32Array(ref reader, "Publish.Results");
        SkipDiagnosticInfoArray(ref reader);
        return new OpcUaPublishResponse(subscriptionId, available, moreNotifications, message.SequenceNumber,
            message.PublishTimeUtc, message.Values, message.UnknownNotifications, acknowledgementResults,
            message.Events);
    }

    private static OpcUaPublishResponse ReadRepublishResponse(uint subscriptionId, ReadOnlySpan<byte> body)
    {
        var reader = new UaBinaryReader(body);
        var message = ReadNotificationMessage(ref reader);
        return new OpcUaPublishResponse(subscriptionId, [], false, message.SequenceNumber,
            message.PublishTimeUtc, message.Values, message.UnknownNotifications, null, message.Events);
    }

    private static (uint SequenceNumber, DateTime PublishTimeUtc, OpcUaMonitoredValue[] Values, DriverExtensionObject[]
        UnknownNotifications, OpcUaEventNotification[] Events) ReadNotificationMessage(ref UaBinaryReader reader)
    {
        var sequenceNumber = reader.ReadUInt32();
        var publishTimeUtc = reader.ReadDateTime();
        var count = ReadArrayLength(ref reader, "NotificationMessage.NotificationData", 3);
        var values = new List<OpcUaMonitoredValue>();
        var unknown = new List<DriverExtensionObject>();
        var events = new List<OpcUaEventNotification>();
        for (var i = 0; i < count; i++)
        {
            var notification = reader.ReadExtensionObject();
            if (IsDataChangeNotification(notification))
                ReadDataChangeNotification(notification.EncodedBody.Span, values);
            else if (IsNotification(notification, OpcUaProtocol.EventNotificationListBinary))
                ReadEventNotificationList(notification.EncodedBody.Span, events);
            else if (notification.Encoding != DriverExtensionObjectEncoding.None) unknown.Add(notification);
        }

        return (sequenceNumber, publishTimeUtc, [.. values], [.. unknown], [.. events]);
    }

    private static bool IsNotification(DriverExtensionObject notification, uint typeId)
    {
        return notification.Encoding == DriverExtensionObjectEncoding.Binary && notification.TypeId.NodeId is
            { NamespaceIndex: 0, Kind: IndustrialNodeIdKind.Numeric } id && id.NumericIdentifier == typeId;
    }

    private static bool IsDataChangeNotification(DriverExtensionObject notification)
    {
        return notification.Encoding == DriverExtensionObjectEncoding.Binary && notification.TypeId.NodeId is
        {
            NamespaceIndex: 0, Kind: IndustrialNodeIdKind.Numeric,
            NumericIdentifier: OpcUaProtocol.DataChangeNotificationBinary
        };
    }

    private static void ReadDataChangeNotification(ReadOnlySpan<byte> body, ICollection<OpcUaMonitoredValue> values)
    {
        var reader = new UaBinaryReader(body);
        var count = ReadArrayLength(ref reader, "DataChangeNotification.MonitoredItems", 5);
        for (var i = 0; i < count; i++)
            values.Add(new OpcUaMonitoredValue(reader.ReadUInt32(), OpcUaBinaryCodec.ReadDataValue(ref reader)));
        SkipDiagnosticInfoArray(ref reader);
    }

    private static uint[] ReadUInt32Array(ref UaBinaryReader reader, string field)
    {
        var count = ReadArrayLength(ref reader, field, 4);
        var values = new uint[count];
        for (var i = 0; i < count; i++) values[i] = reader.ReadUInt32();
        return values;
    }
}
