using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Proxus.Drivers.OpcUa;

public sealed record OpcUaSubscriptionRuntimeOptions
{
    public int NotificationBufferCapacity { get; init; } = 256;
    public int MaximumRepublishGap { get; init; } = 1024;
    public int MaximumReconnectAttempts { get; init; } = 3;
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(1);

    internal DriverResult Validate(Uri endpoint)
    {
        if (NotificationBufferCapacity <= 0 || MaximumRepublishGap <= 0 || MaximumReconnectAttempts <= 0 ||
            ReconnectDelay < TimeSpan.Zero)
            return DriverResult.Failure(new DriverError(DriverErrorKind.Configuration,
                "UA_SUBSCRIPTION_RUNTIME_OPTIONS",
                "NotificationBufferCapacity, MaximumRepublishGap and MaximumReconnectAttempts must be positive; ReconnectDelay cannot be negative.",
                Operation: DriverOperation.Subscribe,
                Protocol: OpcUaProtocol.Name, Endpoint: endpoint.ToString(),
                Retry: DriverRetryDisposition.FixConfiguration,
                Connection: DriverConnectionDisposition.Unchanged));
        return DriverResult.Success();
    }
}

public sealed record OpcUaManagedSubscriptionPlan(
    OpcUaSubscriptionRequest Subscription,
    IReadOnlyList<OpcUaMonitoredItemRequest> MonitoredItems);

internal interface IOpcUaSubscriptionRuntimeTransport
{
    Uri Endpoint { get; }

    ValueTask<DriverResult<OpcUaPublishResponse>> PublishAsync(
        ReadOnlyMemory<OpcUaSubscriptionAcknowledgement> acknowledgements, CancellationToken cancellationToken);

    ValueTask<DriverResult<OpcUaPublishResponse>> RepublishAsync(uint subscriptionId, uint retransmitSequenceNumber,
        CancellationToken cancellationToken);

    ValueTask<DriverResult<OpcUaSubscriptionInfo>> RecreateManagedSubscriptionAsync(OpcUaManagedSubscriptionPlan plan,
        CancellationToken cancellationToken);
}

internal interface IOpcUaSubscriptionRuntimeRouter
{
    bool RegisterSubscriptionRuntime(uint subscriptionId, OpcUaSubscription subscription);
    void UnregisterSubscriptionRuntime(uint subscriptionId, OpcUaSubscription subscription);

    bool UpdateSubscriptionRuntime(uint previousSubscriptionId, uint revisedSubscriptionId,
        OpcUaSubscription subscription);

    bool TryRoutePublishResponse(OpcUaSubscription source, OpcUaPublishResponse response);
}

public sealed class OpcUaSubscription : IAsyncDisposable
{
    private readonly object _acknowledgementGate = new();
    private readonly ConcurrentQueue<OpcUaSubscriptionAcknowledgement> _acknowledgements = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _maximumReconnectAttempts;
    private readonly int _maximumRepublishGap;
    private readonly Channel<OpcUaPublishResponse> _notifications;
    private readonly TimeSpan _reconnectDelay;
    private readonly OpcUaManagedSubscriptionPlan? _recoveryPlan;
    private readonly Channel<OpcUaPublishResponse> _routedResponses;
    private readonly CancellationTokenSource _stop = new();
    private readonly IOpcUaSubscriptionRuntimeTransport _transport;
    private int _deliveredNotifications;
    private int _disposed;
    private uint _nextSequenceNumber;
    private int _publishCycles;
    private Task? _pump;
    private int _recreationCount;
    private int _started;
    private uint _subscriptionId;

    internal OpcUaSubscription(IOpcUaSubscriptionRuntimeTransport transport, uint subscriptionId,
        OpcUaSubscriptionRuntimeOptions options, OpcUaManagedSubscriptionPlan? recoveryPlan = null)
    {
        _transport = transport;
        _subscriptionId = subscriptionId;
        _maximumRepublishGap = options.MaximumRepublishGap;
        _recoveryPlan = recoveryPlan;
        _maximumReconnectAttempts = options.MaximumReconnectAttempts;
        _reconnectDelay = options.ReconnectDelay;
        _notifications = Channel.CreateBounded<OpcUaPublishResponse>(
            new BoundedChannelOptions(options.NotificationBufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
        _routedResponses = Channel.CreateBounded<OpcUaPublishResponse>(
            new BoundedChannelOptions(options.NotificationBufferCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                AllowSynchronousContinuations = false
            });
    }

    public uint SubscriptionId => Volatile.Read(ref _subscriptionId);
    public int RecreationCount => Volatile.Read(ref _recreationCount);
    public int PublishCycles => Volatile.Read(ref _publishCycles);
    public int DeliveredNotifications => Volatile.Read(ref _deliveredNotifications);
    public DriverError? Fault { get; private set; }
    public Task Completion => _completion.Task;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Cancel();
        if (_pump is not null) await _pump.ConfigureAwait(false);
        if (_transport is IOpcUaSubscriptionRuntimeRouter router)
            router.UnregisterSubscriptionRuntime(SubscriptionId, this);
        _stop.Dispose();
    }

    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("Subscription pump is already running.");
        if (_transport is IOpcUaSubscriptionRuntimeRouter router &&
            !router.RegisterSubscriptionRuntime(SubscriptionId, this))
        {
            Interlocked.Exchange(ref _started, 0);
            throw new InvalidOperationException(
                $"Subscription runtime {SubscriptionId} is already registered on this OPC UA client.");
        }

        _pump = PumpAsync();
    }

    internal bool TryRoute(OpcUaPublishResponse response)
    {
        return _routedResponses.Writer.TryWrite(response);
    }

    public async IAsyncEnumerable<OpcUaPublishResponse> ReadNotificationsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        OpcUaPublishResponse? delivered = null;
        try
        {
            await foreach (var notification in _notifications.Reader.ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                QueueAcknowledgement(delivered);
                delivered = notification;
                yield return notification;
            }
        }
        finally
        {
            QueueAcknowledgement(delivered);
        }
    }

    private async Task PumpAsync()
    {
        Exception? completionError = null;
        Task<DriverResult<OpcUaPublishResponse>>? pendingPublish = null;
        Task<bool>? routedResponseAvailable = null;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var routedAny = false;
                while (_routedResponses.Reader.TryRead(out var routed))
                {
                    routedAny = true;
                    await ProcessOrRoutePublishResponseAsync(routed, _stop.Token).ConfigureAwait(false);
                }

                if (routedAny) routedResponseAvailable = null;

                pendingPublish ??= _transport.PublishAsync(DrainAcknowledgements(), _stop.Token).AsTask();
                routedResponseAvailable ??= _routedResponses.Reader.WaitToReadAsync(_stop.Token).AsTask();
                var completed = await Task.WhenAny(pendingPublish, routedResponseAvailable).ConfigureAwait(false);
                if (ReferenceEquals(completed, routedResponseAvailable))
                {
                    if (!await routedResponseAvailable.ConfigureAwait(false)) break;
                    routedResponseAvailable = null;
                    continue;
                }

                var response = await pendingPublish.ConfigureAwait(false);
                pendingPublish = null;
                Interlocked.Increment(ref _publishCycles);
                if (!response.IsSuccess)
                {
                    if (!await TryRecoverAsync(response.Error, _stop.Token).ConfigureAwait(false))
                        throw new OpcUaSubscriptionFaultException(Fault ?? response.Error);
                    continue;
                }

                await ProcessOrRoutePublishResponseAsync(response.Value, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (OpcUaSubscriptionFaultException ex)
        {
            Fault = ex.Error;
            completionError = ex;
        }
        catch (Exception ex)
        {
            Fault = new DriverError(DriverErrorKind.Protocol, "UA_SUBSCRIPTION_PUMP", ex.Message,
                Operation: DriverOperation.Subscribe, Protocol: OpcUaProtocol.Name,
                Endpoint: _transport.Endpoint.ToString(),
                Retry: DriverRetryDisposition.ReconnectAndRetry, Connection: DriverConnectionDisposition.Broken);
            completionError = ex;
        }
        finally
        {
            if (_transport is IOpcUaSubscriptionRuntimeRouter router)
                router.UnregisterSubscriptionRuntime(SubscriptionId, this);
            _notifications.Writer.TryComplete(completionError);
            if (completionError is null) _completion.TrySetResult();
            else _completion.TrySetException(completionError);
        }
    }

    private async Task ProcessOrRoutePublishResponseAsync(OpcUaPublishResponse response,
        CancellationToken cancellationToken)
    {
        if (response.SubscriptionId == SubscriptionId)
        {
            await ProcessPublishResponseAsync(response, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (_transport is IOpcUaSubscriptionRuntimeRouter router &&
            router.TryRoutePublishResponse(this, response)) return;
        throw new OpcUaSubscriptionFaultException(new DriverError(DriverErrorKind.Protocol,
            "UA_PUBLISH_SUBSCRIPTION_ROUTE",
            $"Publish response for SubscriptionId {response.SubscriptionId} could not be routed from runtime {SubscriptionId}.",
            Operation: DriverOperation.Publish, Protocol: OpcUaProtocol.Name, Endpoint: _transport.Endpoint.ToString(),
            Retry: DriverRetryDisposition.ReconnectAndRetry, Connection: DriverConnectionDisposition.Broken));
    }

    private async Task ProcessPublishResponseAsync(OpcUaPublishResponse response, CancellationToken cancellationToken)
    {
        if (response.SequenceNumber == 0) return;
        if (_nextSequenceNumber == 0) _nextSequenceNumber = response.SequenceNumber;
        if (response.SequenceNumber != _nextSequenceNumber)
        {
            if (!IsAhead(_nextSequenceNumber, response.SequenceNumber)) return;
            for (var recovered = 0; _nextSequenceNumber != response.SequenceNumber; recovered++)
            {
                if (recovered >= _maximumRepublishGap)
                    throw new OpcUaSubscriptionFaultException(new DriverError(DriverErrorKind.Protocol,
                        "UA_SEQUENCE_GAP_LIMIT",
                        $"Publish sequence gap exceeded configured maximum {_maximumRepublishGap}.",
                        Operation: DriverOperation.Republish,
                        Protocol: OpcUaProtocol.Name, Endpoint: _transport.Endpoint.ToString(),
                        Retry: DriverRetryDisposition.ReconnectAndRetry,
                        Connection: DriverConnectionDisposition.Broken));
                var replay = await _transport.RepublishAsync(SubscriptionId, _nextSequenceNumber, cancellationToken)
                    .ConfigureAwait(false);
                if (!replay.IsSuccess) throw new OpcUaSubscriptionFaultException(replay.Error);
                if (replay.Value.SequenceNumber != _nextSequenceNumber)
                    throw new OpcUaSubscriptionFaultException(new DriverError(DriverErrorKind.Protocol,
                        "UA_REPUBLISH_SEQUENCE",
                        $"Republish returned sequence {replay.Value.SequenceNumber}, expected {_nextSequenceNumber}.",
                        Operation: DriverOperation.Republish,
                        Protocol: OpcUaProtocol.Name, Endpoint: _transport.Endpoint.ToString(),
                        Retry: DriverRetryDisposition.ReconnectAndRetry,
                        Connection: DriverConnectionDisposition.Broken));
                await DeliverAsync(replay.Value, cancellationToken).ConfigureAwait(false);
            }
        }

        await DeliverAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task DeliverAsync(OpcUaPublishResponse notification, CancellationToken cancellationToken)
    {
        await _notifications.Writer.WriteAsync(notification, cancellationToken).ConfigureAwait(false);
        Interlocked.Increment(ref _deliveredNotifications);
        _nextSequenceNumber = NextSequence(notification.SequenceNumber);
    }

    private OpcUaSubscriptionAcknowledgement[] DrainAcknowledgements()
    {
        lock (_acknowledgementGate)
        {
            var acknowledgements = new List<OpcUaSubscriptionAcknowledgement>();
            while (_acknowledgements.TryDequeue(out var acknowledgement)) acknowledgements.Add(acknowledgement);
            return [.. acknowledgements];
        }
    }

    private void QueueAcknowledgement(OpcUaPublishResponse? delivered)
    {
        if (delivered is null || delivered.SequenceNumber == 0) return;
        lock (_acknowledgementGate)
        {
            if (delivered.SubscriptionId == SubscriptionId)
                _acknowledgements.Enqueue(
                    new OpcUaSubscriptionAcknowledgement(delivered.SubscriptionId, delivered.SequenceNumber));
        }
    }

    private static uint NextSequence(uint sequenceNumber)
    {
        return sequenceNumber == uint.MaxValue ? 1u : sequenceNumber + 1u;
    }

    private static bool IsAhead(uint expected, uint actual)
    {
        return unchecked(actual - expected) is > 0 and <= 0x7FFFFFFF;
    }

    private async ValueTask<bool> TryRecoverAsync(DriverError error, CancellationToken cancellationToken)
    {
        if (_recoveryPlan is null || error.Retry != DriverRetryDisposition.ReconnectAndRetry) return false;
        DriverError? lastFailure = null;
        for (var attempt = 0; attempt < _maximumReconnectAttempts; attempt++)
        {
            if (attempt > 0 && _reconnectDelay > TimeSpan.Zero)
                await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
            var recreated = await _transport.RecreateManagedSubscriptionAsync(_recoveryPlan, cancellationToken)
                .ConfigureAwait(false);
            if (!recreated.IsSuccess)
            {
                lastFailure = recreated.Error;
                continue;
            }

            var previousSubscriptionId = SubscriptionId;
            if (_transport is IOpcUaSubscriptionRuntimeRouter router &&
                !router.UpdateSubscriptionRuntime(previousSubscriptionId, recreated.Value.SubscriptionId, this))
            {
                lastFailure = new DriverError(DriverErrorKind.Protocol, "UA_SUBSCRIPTION_ROUTE_COLLISION",
                    $"Recreated SubscriptionId {recreated.Value.SubscriptionId} is already owned by another runtime.",
                    Operation: DriverOperation.Subscribe, Protocol: OpcUaProtocol.Name,
                    Endpoint: _transport.Endpoint.ToString(),
                    Retry: DriverRetryDisposition.ReconnectAndRetry, Connection: DriverConnectionDisposition.Broken);
                continue;
            }

            lock (_acknowledgementGate)
            {
                Volatile.Write(ref _subscriptionId, recreated.Value.SubscriptionId);
                while (_acknowledgements.TryDequeue(out _))
                {
                }
            }

            Interlocked.Increment(ref _recreationCount);
            _nextSequenceNumber = 0;
            return true;
        }

        Fault = lastFailure;
        return false;
    }
}

public sealed class OpcUaSubscriptionFaultException(DriverError error) : Exception(error.Message)
{
    public DriverError Error { get; } = error;
}
