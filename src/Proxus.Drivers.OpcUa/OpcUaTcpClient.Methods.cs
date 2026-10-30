namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private const int MaximumMethodArguments = 255;

    /// <summary>
    ///     Calls one method and returns its result. A method that the server rejects returns a successful
    ///     <see cref="DriverResult{T}" /> whose <see cref="OpcUaMethodResult.StatusCode" /> is Bad; transport,
    ///     Session and ServiceFault failures return a failed result. A method call is not idempotent in general:
    ///     after a timeout or a lost connection the outcome is unknown and the call is never repeated.
    /// </summary>
    public async ValueTask<DriverResult<OpcUaMethodResult>> CallAsync(
        IndustrialNodeId objectId,
        IndustrialNodeId methodId,
        ReadOnlyMemory<DriverValue> inputArguments = default,
        CancellationToken cancellationToken = default)
    {
        var results = await CallAsync(new[] { new OpcUaMethodCall(objectId, methodId, inputArguments.ToArray()) },
            cancellationToken).ConfigureAwait(false);
        return results.IsSuccess
            ? DriverResult<OpcUaMethodResult>.Success(results.Value[0])
            : DriverResult<OpcUaMethodResult>.Failure(results.Error);
    }

    /// <summary>Calls methods with the Call service (OPC 10000-4 §5.11.2), one result per call in order.</summary>
    public async ValueTask<DriverResult<OpcUaMethodResult[]>> CallAsync(
        ReadOnlyMemory<OpcUaMethodCall> calls,
        CancellationToken cancellationToken = default)
    {
        if (calls.IsEmpty) return DriverResult<OpcUaMethodResult[]>.Success([]);
        foreach (var call in calls.Span)
            if (call is null || call.InputArguments.Count > MaximumMethodArguments)
                return DriverResult<OpcUaMethodResult[]>.Failure(Failure(DriverErrorKind.Configuration,
                    "UA_CALL_REQUEST", $"Each method call needs at most {MaximumMethodArguments} input arguments.",
                    DriverOperation.Execute, DriverRetryDisposition.FixConfiguration,
                    DriverConnectionDisposition.Unchanged).Error);

        var socket = _socket;
        if (socket is null || SecureChannel is null || Session is not { IsActivated: true } session)
            return DriverResult<OpcUaMethodResult[]>.Failure(SessionServiceUnavailable(DriverOperation.Execute, "Call"));

        var requestId = NextRequestId();
        using var writer = BeginServiceRequest(OpcUaProtocol.CallRequestBinary, session.AuthenticationToken,
            requestId, requestId);
        writer.WriteInt32(calls.Length);
        foreach (var call in calls.Span)
        {
            writer.WriteNodeId(call.ObjectId);
            writer.WriteNodeId(call.MethodId);
            writer.WriteInt32(call.InputArguments.Count);
            foreach (var argument in call.InputArguments) OpcUaBinaryCodec.WriteVariant(writer, argument);
        }

        var envelope = await SendServiceAsync(socket, writer, requestId, requestId, OpcUaProtocol.CallResponseBinary,
            DriverOperation.Execute, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaMethodResult[]>.Failure(MethodOutcome(envelope.Error));
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "Call.Results", 16);
            if (count != calls.Length)
                throw new InvalidDataException($"Call returned {count} results for {calls.Length} calls.");
            var results = new OpcUaMethodResult[count];
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                var inputCount = ReadArrayLength(ref reader, "CallMethodResult.InputArgumentResults", 4);
                var inputResults = new uint[inputCount];
                for (var a = 0; a < inputCount; a++) inputResults[a] = reader.ReadUInt32();
                SkipDiagnosticInfoArray(ref reader);
                var outputCount = ReadArrayLength(ref reader, "CallMethodResult.OutputArguments", 1);
                var outputs = new DriverValue[outputCount];
                for (var o = 0; o < outputCount; o++) outputs[o] = OpcUaBinaryCodec.ReadVariant(ref reader);
                results[i] = new OpcUaMethodResult(status, inputResults, outputs);
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<OpcUaMethodResult[]>.Success(results);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            // The server answered, so the methods ran (or were rejected); only the reply is unreadable.
            return DriverResult<OpcUaMethodResult[]>.Failure(Failure(DriverErrorKind.Protocol, "UA_CALL_DECODE",
                ex.Message, DriverOperation.Execute, DriverRetryDisposition.DoNotRetry,
                DriverConnectionDisposition.Healthy).Error);
        }
    }

    // A timeout or a channel lost after the request was sent leaves the method outcome unknown: the
    // connection still needs to be re-established, but the call itself must not be repeated automatically.
    private static DriverError MethodOutcome(DriverError error)
    {
        return error.Code is "UA_SERVICE_TIMEOUT" or "UA_CHANNEL_CLOSED" or "UA_SERVICE_TRANSPORT"
            ? error with
            {
                Code = "UA_CALL_OUTCOME_UNKNOWN",
                Cause = "The connection failed after the Call request was sent; the server may have run the method.",
                SuggestedAction = "Reconnect, then check the method's effect before calling it again.",
                Retry = DriverRetryDisposition.DoNotRetry,
                Connection = DriverConnectionDisposition.Broken
            }
            : error;
    }
}
