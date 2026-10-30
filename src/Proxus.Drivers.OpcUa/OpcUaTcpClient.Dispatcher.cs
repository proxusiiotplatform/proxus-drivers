using System.Collections.Concurrent;
using System.Net.Sockets;
using Proxus.Drivers.Core;

namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private const int MaximumDiscardedRequests = 1024;
    private readonly ConcurrentDictionary<uint, byte> _discardedRequestIds = new();
    private readonly ConcurrentDictionary<uint, PendingServiceRequest> _pendingRequests = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private Task? _receiveLoop;
    private CancellationTokenSource? _receiveLoopCancellation;
    internal int PendingRequestCount => _pendingRequests.Count;
    internal int SendGateCount => _sendGate.CurrentCount;

    internal string PendingRequestSnapshot => string.Join(",",
        _pendingRequests.Select(static pair =>
            $"{pair.Key}:{pair.Value.Operation}:{pair.Value.Timeout.TotalMilliseconds:R}"));

    private void StartReceiveLoop(Socket socket)
    {
        var cancellation = new CancellationTokenSource();
        _receiveLoopCancellation = cancellation;
        _receiveLoop = ReceiveLoopAsync(socket, cancellation.Token);
    }

    private async Task ReceiveLoopAsync(Socket socket, CancellationToken cancellationToken)
    {
        DriverError? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[] message;
                try
                {
                    // Individual pending requests own their deadlines. The receive loop must
                    // remain parked indefinitely when the connection is healthy but idle.
                    message = await ReceiveMessageAsync(socket, cancellationToken, Timeout.InfiniteTimeSpan)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested &&
                                                         _pendingRequests.IsEmpty)
                {
                    continue;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("OPC UA receive loop timed out while service responses were pending.");
                }

                if (IsType(message, "ERR"u8))
                    throw new InvalidDataException(
                        "OPC UA server returned a UA-TCP ERR message after the SecureChannel was opened.");
                if (!IsType(message, "MSG"u8) && !IsType(message, "OPN"u8))
                    throw new InvalidDataException("OPC UA dispatcher requires a MSG or OPN response.");
                var readRequestId = IsType(message, "OPN"u8)
                    ? TryReadOpenResponseRequestId(message, out var requestId)
                    : TryReadResponseRequestId(message, out requestId);
                if (!readRequestId)
                    throw new InvalidDataException("OPC UA dispatcher could not read the response RequestId.");

                if (_pendingRequests.TryRemove(requestId, out var pending))
                {
                    if (pending.ExpectsOpenSecureChannel != IsType(message, "OPN"u8))
                        throw new InvalidDataException(
                            $"OPC UA response RequestId {requestId} does not match the pending message kind.");
                    pending.Complete(message);
                    continue;
                }

                // A caller can abandon its waiter while its request remains valid on the wire.
                // Consume the late reply so it cannot be mistaken for a later request's response.
                if (_discardedRequestIds.TryRemove(requestId, out _)) continue;
                throw new InvalidDataException($"OPC UA response RequestId {requestId} has no pending request.");
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            failure = new DriverError(
                ex is SocketException or IOException or EndOfStreamException
                    ? DriverErrorKind.Transport
                    : DriverErrorKind.Protocol,
                ex is SocketException or IOException or EndOfStreamException
                    ? "UA_RECEIVE_LOOP_TRANSPORT"
                    : "UA_RECEIVE_LOOP_PROTOCOL",
                ex.Message,
                Operation: DriverOperation.ReadBatch,
                Protocol: OpcUaProtocol.Name,
                Endpoint: _options.EndpointUrl.ToString(),
                Cause: "The single OPC UA receive loop could no longer safely route service responses.",
                SuggestedAction: "Reconnect the SecureChannel before issuing another service request.",
                Retry: DriverRetryDisposition.ReconnectAndRetry,
                Connection: DriverConnectionDisposition.Broken);
        }
        finally
        {
            if (failure is { } error)
            {
                CloseSocketCore(socket);
                CompletePendingRequests(error);
            }
        }
    }

    private async ValueTask<DriverResult<byte[]>> SendAndAwaitResponseAsync(
        Socket socket,
        UaBinaryWriter writer,
        uint requestId,
        DriverOperation operation,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        bool expectsOpenSecureChannel = false,
        Func<UaBinaryWriter, DriverError?>? prepare = null)
    {
        var pending = new PendingServiceRequest(expectsOpenSecureChannel, operation, timeout);
        if (!_pendingRequests.TryAdd(requestId, pending))
            return DriverResult<byte[]>.Failure(Failure(DriverErrorKind.Protocol, "UA_REQUEST_ID_DUPLICATE",
                $"OPC UA RequestId {requestId} is already pending.", operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
        _ = WatchPendingRequestAsync(socket, requestId, pending, timeout);
        try
        {
            using var sendGateTimeout = TimeoutTokenScope.Create(cancellationToken, timeout);
            await _sendGate.WaitAsync(sendGateTimeout.Token).ConfigureAwait(false);
            try
            {
                // Numbering and protection happen here, inside the gate, so SequenceNumbers leave in order.
                if (prepare?.Invoke(writer) is { } prepareError)
                {
                    AbandonPending(requestId, pending);
                    return DriverResult<byte[]>.Failure(prepareError);
                }

                using var sendTimeout = TimeoutTokenScope.Create(cancellationToken, timeout);
                await SocketIo.SendAllAsync(socket, writer.WrittenMemory, sendTimeout.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }

            try
            {
                using var waitTimeout = TimeoutTokenScope.Create(cancellationToken, timeout);
                return DriverResult<byte[]>.Success(await pending.Completion.Task.WaitAsync(waitTimeout.Token)
                    .ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AbandonPending(requestId, pending);
                return DriverResult<byte[]>.Failure(Failure(DriverErrorKind.Cancelled, "UA_SERVICE_CANCELLED",
                    "OPC UA service was cancelled by the caller.", operation,
                    DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Healthy).Error);
            }
            catch (OperationCanceledException)
            {
                AbandonPending(requestId, pending);
                CloseSocketCore(socket);
                return DriverResult<byte[]>.Failure(Failure(DriverErrorKind.Timeout, "UA_SERVICE_TIMEOUT",
                    "OPC UA service timed out.", operation,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            AbandonPending(requestId, pending);
            return DriverResult<byte[]>.Failure(Failure(DriverErrorKind.Cancelled, "UA_SERVICE_CANCELLED",
                "OPC UA service was cancelled by the caller.", operation,
                DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Healthy).Error);
        }
        catch (OperationCanceledException)
        {
            AbandonPending(requestId, pending);
            CloseSocketCore(socket);
            return DriverResult<byte[]>.Failure(Failure(DriverErrorKind.Timeout, "UA_SERVICE_TIMEOUT",
                "OPC UA service timed out.", operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
        }
        catch (OpcUaPendingRequestException ex)
        {
            return DriverResult<byte[]>.Failure(ex.Error);
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
        {
            _pendingRequests.TryRemove(requestId, out _);
            CloseSocketCore(socket);
            var error = Failure(DriverErrorKind.Transport, "UA_SERVICE_TRANSPORT", ex.Message, operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error;
            CompletePendingRequests(error);
            return DriverResult<byte[]>.Failure(error);
        }
    }

    private void AbandonPending(uint requestId, PendingServiceRequest pending)
    {
        if (_pendingRequests.TryRemove(new KeyValuePair<uint, PendingServiceRequest>(requestId, pending)))
        {
            pending.Abandon();
            if (_discardedRequestIds.Count >= MaximumDiscardedRequests)
            {
                CloseSocket();
                return;
            }

            _discardedRequestIds.TryAdd(requestId, 0);
        }
    }

    private async Task WatchPendingRequestAsync(Socket socket, uint requestId, PendingServiceRequest pending,
        TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan) return;
        try
        {
            await Task.Delay(timeout, pending.WatchCancellation).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!_pendingRequests.TryRemove(new KeyValuePair<uint, PendingServiceRequest>(requestId, pending))) return;
        var error = Failure(DriverErrorKind.Timeout, "UA_SERVICE_TIMEOUT",
            "OPC UA service timed out before a response arrived.", pending.Operation,
            DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error;
        pending.Fail(error);
        CloseSocketCore(socket);
    }

    private void CompletePendingRequests(DriverError error)
    {
        foreach (var pair in _pendingRequests)
            if (_pendingRequests.TryRemove(pair.Key, out var pending))
                pending.Fail(pending.Operation == DriverOperation.Unknown
                    ? error
                    : error with { Operation = pending.Operation });
        _discardedRequestIds.Clear();
    }

    private static bool TryReadResponseRequestId(byte[] message, out uint requestId)
    {
        requestId = 0;
        if (message.Length < 24) return false;
        try
        {
            var reader = new UaBinaryReader(message.AsSpan(8));
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            requestId = reader.ReadUInt32();
            return requestId != 0;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    private static bool TryReadOpenResponseRequestId(byte[] message, out uint requestId)
    {
        requestId = 0;
        try
        {
            var reader = new UaBinaryReader(message.AsSpan(8));
            _ = reader.ReadUInt32();
            _ = reader.ReadString();
            _ = reader.ReadByteString();
            _ = reader.ReadByteString();
            _ = reader.ReadUInt32();
            requestId = reader.ReadUInt32();
            return requestId != 0;
        }
        catch (EndOfStreamException)
        {
            return false;
        }
    }

    private sealed class PendingServiceRequest(
        bool expectsOpenSecureChannel,
        DriverOperation operation,
        TimeSpan timeout)
    {
        private CancellationTokenSource? _watchCancellation = new();
        public bool ExpectsOpenSecureChannel { get; } = expectsOpenSecureChannel;
        public DriverOperation Operation { get; } = operation;
        public TimeSpan Timeout { get; } = timeout;

        public TaskCompletionSource<byte[]> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken WatchCancellation => _watchCancellation?.Token ?? new CancellationToken(true);

        public void Complete(byte[] message)
        {
            Completion.TrySetResult(message);
            StopWatch();
        }

        public void Fail(DriverError error)
        {
            Completion.TrySetException(new OpcUaPendingRequestException(error));
            StopWatch();
        }

        public void Abandon()
        {
            StopWatch();
        }

        private void StopWatch()
        {
            var cancellation = Interlocked.Exchange(ref _watchCancellation, null);
            if (cancellation is null) return;
            cancellation.Cancel();
            cancellation.Dispose();
        }
    }

    private sealed class OpcUaPendingRequestException(DriverError error) : Exception(error.Message)
    {
        public DriverError Error { get; } = error;
    }
}
