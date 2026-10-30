namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    public async ValueTask<DriverResult<OpcUaEndpointDescription[]>> GetEndpointsAsync(
        IReadOnlyList<string>? profileUris = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return DriverResult<OpcUaEndpointDescription[]>.Failure(Failure(
                    DriverErrorKind.Disposed, "UA_DISPOSED", "OPC UA client has been disposed.",
                    DriverOperation.Negotiate)
                .Error);
        if (_socket is null || SecureChannel is null)
            return DriverResult<OpcUaEndpointDescription[]>.Failure(Failure(
                DriverErrorKind.Configuration, "UA_CHANNEL_REQUIRED",
                "An open SecureChannel is required for GetEndpoints.", DriverOperation.Negotiate).Error);

        var socket = _socket;
        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.GetEndpointsRequestBinary,
            IndustrialNodeId.Numeric(0, 0), requestId, requestHandle);
        writer.WriteString(_options.EndpointUrl.ToString());
        WriteStringArray(writer, _options.PreferredLocales);
        WriteStringArray(writer, profileUris?.ToArray() ?? []);
        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
                OpcUaProtocol.GetEndpointsResponseBinary, DriverOperation.Negotiate, cancellationToken)
            .ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<OpcUaEndpointDescription[]>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "GetEndpoints.Endpoints", 20);
            var endpoints = new OpcUaEndpointDescription[count];
            for (var i = 0; i < count; i++) endpoints[i] = ReadEndpointDescription(ref reader);
            return DriverResult<OpcUaEndpointDescription[]>.Success(endpoints);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaEndpointDescription[]>.Failure(Failure(
                DriverErrorKind.Protocol, "UA_GET_ENDPOINTS_DECODE", ex.Message, DriverOperation.Negotiate,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy).Error);
        }
    }

    public static async ValueTask<DriverResult<OpcUaEndpointDescription[]>> DiscoverEndpointsAsync(
        Uri discoveryUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(discoveryUrl);
        await using var client = new OpcUaTcpClient(new OpcUaTcpOptions { EndpointUrl = discoveryUrl });
        var connected = await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
        if (!connected.IsSuccess) return DriverResult<OpcUaEndpointDescription[]>.Failure(connected.Error);
        return await client.GetEndpointsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
