using System.Buffers.Binary;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Proxus.Drivers.Core;

namespace Proxus.Drivers.OpcUa;

/// <summary>
///     Native UA-TCP / UA Binary client with SecurityPolicy#None SecureChannel, anonymous Session,
///     typed Read/Write/Browse/Subscription services, DataTypeDefinition discovery and lossless DataValue
///     quality/timestamp mapping. Secure policies are a separate milestone.
/// </summary>
public sealed partial class OpcUaTcpClient : IAsyncDisposable, IOpcUaSubscriptionRuntimeTransport,
    IOpcUaSubscriptionRuntimeRouter
{
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly OpcUaTcpOptions _options;
    private byte[]? _clientSecureChannelNonce;
    private OpcUaSymmetricKeySet? _clientSymmetricKeys;
    private bool _disposed;
    private OpcUaSymmetricKeySet? _previousClientSymmetricKeys;
    private OpcUaSymmetricKeySet? _previousServerSymmetricKeys;
    private DateTime _previousTokenExpiresUtc;
    private uint _previousTokenId;
    private uint _requestId;
    private uint _sequenceNumber;
    private byte[]? _serverSecureChannelNonce;
    private OpcUaSymmetricKeySet? _serverSymmetricKeys;
    private byte[]? _sessionClientNonce;
    private byte[]? _sessionServerNonce;
    // The activated Session of a lost SecureChannel and its last server nonce, kept for reactivation.
    private OpcUaSessionInfo? _lostSession;
    private byte[]? _lostSessionServerNonce;
    // An ActivateSession request is on the wire: the server may activate the Session even if its response is lost.
    private volatile bool _activationInFlight;
    private Socket? _socket;

    public OpcUaTcpClient(OpcUaTcpOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool IsConnected => _socket is { Connected: true } && SecureChannel.HasValue;
    internal Uri Endpoint => _options.EndpointUrl;
    public OpcUaConnectionLimits? NegotiatedLimits { get; private set; }
    public OpcUaSecureChannelInfo? SecureChannel { get; private set; }
    public OpcUaSessionInfo? Session { get; private set; }
    public bool IsSessionActive => Session is { IsActivated: true };

    /// <summary>
    ///     True when the last Open*SessionAsync activated the Session of a lost SecureChannel again instead of creating a
    ///     new one (<see cref="OpcUaTcpOptions.ReactivateSessionOnReconnect" />).
    /// </summary>
    public bool SessionReactivated { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _ioGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            CloseSocket();
            _lostSession = null;
            ReplaceSecret(ref _lostSessionServerNonce, null);
        }
        finally
        {
            _ioGate.Release();
            _ioGate.Dispose();
        }
    }

    public async ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Failure(DriverErrorKind.Disposed, "UA_DISPOSED", "OPC UA client has been disposed.",
                DriverOperation.Connect);
        var validation = _options.Validate();
        if (!validation.IsSuccess) return validation;
        if (IsConnected) return DriverResult.Success();

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected) return DriverResult.Success();
            CloseSocket();
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            _socket = socket;

            try
            {
                using (var timeout = TimeoutTokenScope.Create(cancellationToken, _options.ConnectTimeout))
                {
                    await socket.ConnectAsync(_options.EndpointUrl.Host, _options.EndpointUrl.Port, timeout.Token)
                        .ConfigureAwait(false);
                }

                var ack = await ExchangeHelloAsync(socket, cancellationToken).ConfigureAwait(false);
                if (!ack.IsSuccess)
                {
                    CloseSocket();
                    return ack;
                }

                var open = await RequestSecureChannelAsync(socket, OpcUaSecurityTokenRequestType.Issue,
                    cancellationToken).ConfigureAwait(false);
                if (!open.IsSuccess)
                {
                    CloseSocket();
                    return open;
                }

                StartReceiveLoop(socket);
                StartSecureChannelRenewal();
                return DriverResult.Success();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CloseSocket();
                return Failure(DriverErrorKind.Cancelled, "UA_CONNECT_CANCELLED",
                    "OPC UA connection was cancelled by the caller.", DriverOperation.Connect,
                    DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Broken);
            }
            catch (OperationCanceledException)
            {
                CloseSocket();
                return Failure(DriverErrorKind.Timeout, "UA_CONNECT_TIMEOUT",
                    "OPC UA connection or handshake timed out.", DriverOperation.Connect,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
            }
            catch (Exception ex) when (ex is SocketException or IOException or EndOfStreamException)
            {
                CloseSocket();
                return Failure(DriverErrorKind.Transport, "UA_CONNECT_TRANSPORT",
                    $"OPC UA transport failed: {ex.Message}", DriverOperation.Connect,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
            }
            catch (InvalidDataException ex)
            {
                CloseSocket();
                return Failure(DriverErrorKind.Protocol, "UA_HANDSHAKE_PROTOCOL", ex.Message, DriverOperation.Handshake,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public async ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed) return DriverResult.Success();
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var firstFailure = DriverResult.Success();
            if (Session is not null)
            {
                var closeSession = await CloseSessionCoreAsync(true, cancellationToken).ConfigureAwait(false);
                if (!closeSession.IsSuccess) firstFailure = closeSession;
            }

            if (_socket is not null && SecureChannel is not null)
            {
                var closeChannel = await SendCloseSecureChannelCoreAsync(cancellationToken).ConfigureAwait(false);
                if (firstFailure.IsSuccess && !closeChannel.IsSuccess) firstFailure = closeChannel;
            }

            CloseSocket();
            return firstFailure;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async ValueTask<DriverResult> ExchangeHelloAsync(Socket socket, CancellationToken cancellationToken)
    {
        using var writer = new UaBinaryWriter(256);
        writer.WriteBytes("HELF"u8);
        writer.WriteUInt32(0); // patched below
        writer.WriteUInt32(OpcUaProtocol.ProtocolVersion);
        writer.WriteUInt32(_options.ReceiveBufferSize);
        writer.WriteUInt32(_options.SendBufferSize);
        writer.WriteUInt32(_options.MaxMessageSize);
        writer.WriteUInt32(_options.MaxChunkCount);
        writer.WriteString(_options.EndpointUrl.ToString());
        writer.PatchUInt32(4, checked((uint)writer.Position));

        using (var timeout = TimeoutTokenScope.Create(cancellationToken, _options.RequestTimeout))
        {
            await SocketIo.SendAllAsync(socket, writer.WrittenMemory, timeout.Token).ConfigureAwait(false);
        }

        var message = await ReceiveMessageAsync(socket, cancellationToken).ConfigureAwait(false);
        if (IsType(message, "ERR"u8)) return ParseTransportError(message, DriverOperation.Handshake);
        if (!IsType(message, "ACK"u8))
            return Failure(DriverErrorKind.Protocol, "UA_ACK_EXPECTED",
                "Server did not return an OPC UA Acknowledge message.", DriverOperation.Handshake,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        if (message.Length != 28)
            return Failure(DriverErrorKind.Protocol, "UA_ACK_LENGTH",
                $"OPC UA Acknowledge message length was {message.Length}, expected 28.", DriverOperation.Handshake,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);

        var reader = new UaBinaryReader(message.AsSpan(8));
        var version = reader.ReadUInt32();
        var serverReceive = reader.ReadUInt32();
        var serverSend = reader.ReadUInt32();
        var maxMessage = reader.ReadUInt32();
        var maxChunks = reader.ReadUInt32();
        if (version > OpcUaProtocol.ProtocolVersion)
            return Failure(DriverErrorKind.Protocol, "UA_PROTOCOL_VERSION",
                $"Server selected unsupported UACP protocol version {version}.", DriverOperation.Negotiate,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Broken);
        if (serverReceive < OpcUaProtocol.MinimumBufferSize || serverSend < OpcUaProtocol.MinimumBufferSize)
            return Failure(DriverErrorKind.Protocol, "UA_ACK_BUFFER",
                $"Server negotiated invalid buffers receive={serverReceive}, send={serverSend}.",
                DriverOperation.Negotiate,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Broken);
        if (serverReceive > _options.SendBufferSize || serverSend > _options.ReceiveBufferSize)
            return Failure(DriverErrorKind.Protocol, "UA_ACK_BUFFER_LIMIT",
                "Server Acknowledge exceeds buffer limits proposed by the client.", DriverOperation.Negotiate,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Broken);

        NegotiatedLimits = new OpcUaConnectionLimits(serverSend, serverReceive, maxMessage, maxChunks);
        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> RequestSecureChannelAsync(Socket socket,
        OpcUaSecurityTokenRequestType requestType, CancellationToken cancellationToken)
    {
        var sequence = NextSequenceNumber();
        var requestId = NextRequestId();
        var requestHandle = requestId;

        using var body = new UaBinaryWriter();
        body.WriteNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.OpenSecureChannelRequestBinary));
        WriteRequestHeader(body, IndustrialNodeId.Numeric(0, 0), requestHandle, _options.RequestTimeout);
        body.WriteUInt32(OpcUaProtocol.ProtocolVersion);
        body.WriteUInt32((uint)requestType);
        body.WriteUInt32((uint)_options.SecurityMode);
        if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
        {
            body.WriteNullByteString();
        }
        else
        {
            var securityPolicy = GetSecurityPolicy();
            ReplaceSecret(ref _clientSecureChannelNonce,
                RandomNumberGenerator.GetBytes(securityPolicy.NonceLength));
            body.WriteByteString(_clientSecureChannelNonce);
        }

        body.WriteUInt32(_options.RequestedSecureChannelLifetimeMilliseconds);

        using var writer = new UaBinaryWriter(body.Position + 2048);
        if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
        {
            writer.WriteBytes("OPNF"u8);
            writer.WriteUInt32(0);
            writer.WriteUInt32(requestType == OpcUaSecurityTokenRequestType.Issue
                ? 0u
                : SecureChannel?.ChannelId ?? 0u);
            writer.WriteString(OpcUaProtocol.SecurityPolicyNone);
            writer.WriteNullByteString();
            writer.WriteNullByteString();
            writer.WriteUInt32(sequence);
            writer.WriteUInt32(requestId);
            writer.WriteBytes(body.WrittenSpan);
            writer.PatchUInt32(4, checked((uint)writer.Position));
        }
        else
        {
            var clientCertificate = _options.ApplicationIdentity!.Certificate;
            var serverCertificate = _options.GetEndpointServerCertificate()
                                    ?? throw new InvalidOperationException("Endpoint server certificate is missing.");
            var protectedMessage = OpcUaAsymmetricOpenCodec.Encode(
                requestType == OpcUaSecurityTokenRequestType.Issue ? 0u : SecureChannel?.ChannelId ?? 0u,
                sequence,
                requestId,
                body.WrittenSpan,
                clientCertificate,
                serverCertificate,
                GetSecurityPolicy());
            writer.WriteBytes(protectedMessage);
            CryptographicOperations.ZeroMemory(protectedMessage);
        }

        byte[] message;
        if (_receiveLoopCancellation is not null && _receiveLoop is { IsCompleted: false })
        {
            var dispatched = await SendAndAwaitResponseAsync(socket, writer, requestId, DriverOperation.Negotiate,
                _options.RequestTimeout, cancellationToken, true).ConfigureAwait(false);
            if (!dispatched.IsSuccess) return DriverResult.Failure(dispatched.Error);
            message = dispatched.Value;
        }
        else
        {
            using (var timeout = TimeoutTokenScope.Create(cancellationToken, _options.RequestTimeout))
            {
                await SocketIo.SendAllAsync(socket, writer.WrittenMemory, timeout.Token).ConfigureAwait(false);
            }

            message = await ReceiveMessageAsync(socket, cancellationToken).ConfigureAwait(false);
        }

        if (IsType(message, "ERR"u8)) return ParseTransportError(message, DriverOperation.Handshake);
        if (!IsType(message, "OPN"u8))
            return Failure(DriverErrorKind.Protocol, "UA_OPN_EXPECTED",
                "Server did not return an OpenSecureChannel response.", DriverOperation.Handshake,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);

        try
        {
            var reader = new UaBinaryReader(message.AsSpan(8));
            var headerChannelId = reader.ReadUInt32();
            var policy = reader.ReadString();
            _ = reader.ReadByteString(); // sender certificate; secure responses were already validated before dispatch
            _ = reader.ReadByteString(); // receiver thumbprint
            _ = reader.ReadUInt32(); // server sequence
            var responseRequestId = reader.ReadUInt32();
            if (responseRequestId != requestId)
                throw new InvalidDataException(
                    $"OpenSecureChannel response RequestId {responseRequestId} does not match request {requestId}.");
            var typeId = reader.ReadNodeId();
            if (typeId.NamespaceIndex != 0 || typeId.Kind != IndustrialNodeIdKind.Numeric ||
                typeId.NumericIdentifier != OpcUaProtocol.OpenSecureChannelResponseBinary)
                throw new InvalidDataException($"Unexpected OpenSecureChannel response TypeId {typeId}.");
            var response = ReadResponseHeader(ref reader);
            if (response.RequestHandle != requestHandle)
                throw new InvalidDataException(
                    $"OpenSecureChannel response handle {response.RequestHandle} does not match request handle {requestHandle}.");
            if (response.ServiceResult != 0)
                return ServiceFailure(response.ServiceResult, "UA_OPEN_SECURE_CHANNEL",
                    "Server rejected OpenSecureChannel.", DriverOperation.Handshake);
            var serverProtocolVersion = reader.ReadUInt32();
            var tokenChannelId = reader.ReadUInt32();
            var tokenId = reader.ReadUInt32();
            var createdAt = reader.ReadDateTime();
            var revisedLifetime = reader.ReadUInt32();
            var serverNonce = reader.ReadByteString();

            if (!string.Equals(policy, _options.SecurityPolicyUri, StringComparison.Ordinal))
                throw new InvalidDataException($"Server responded with unexpected SecurityPolicyUri '{policy}'.");
            if (headerChannelId == 0 || tokenChannelId == 0 || headerChannelId != tokenChannelId)
                throw new InvalidDataException(
                    $"Invalid SecureChannel identifiers header={headerChannelId}, token={tokenChannelId}.");
            if (tokenId == 0 || revisedLifetime == 0)
                throw new InvalidDataException("Server returned an invalid SecureChannel token or lifetime.");

            var previousChannel = SecureChannel;
            SecureChannel = new OpcUaSecureChannelInfo(tokenChannelId, tokenId, createdAt, revisedLifetime,
                serverProtocolVersion);
            if (_options.SecurityMode != OpcUaMessageSecurityMode.None)
            {
                var securityPolicy = GetSecurityPolicy();
                if (serverNonce is null || serverNonce.Length != securityPolicy.NonceLength ||
                    _clientSecureChannelNonce is null)
                    throw new InvalidDataException(
                        "Secure OpenSecureChannel response returned an invalid server nonce.");
                ReplaceSecret(ref _serverSecureChannelNonce, serverNonce);
                _previousClientSymmetricKeys?.Dispose();
                _previousServerSymmetricKeys?.Dispose();
                _previousClientSymmetricKeys = _clientSymmetricKeys;
                _previousServerSymmetricKeys = _serverSymmetricKeys;
                _previousTokenId = previousChannel?.TokenId ?? 0;
                _previousTokenExpiresUtc = previousChannel is { } old
                    ? old.CreatedAtUtc.AddMilliseconds(old.RevisedLifetimeMilliseconds)
                    : DateTime.MinValue;
                (_clientSymmetricKeys, _serverSymmetricKeys) =
                    OpcUaSecurityCrypto.DeriveKeys(_clientSecureChannelNonce, _serverSecureChannelNonce,
                        securityPolicy);
            }

            return DriverResult.Success();
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return Failure(DriverErrorKind.Protocol, "UA_OPN_DECODE", ex.Message, DriverOperation.Handshake,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        }
    }

    internal static void WriteRequestHeader(UaBinaryWriter writer, IndustrialNodeId authenticationToken,
        uint requestHandle, TimeSpan timeout)
    {
        writer.WriteNodeId(authenticationToken);
        writer.WriteDateTime(DateTime.UtcNow);
        writer.WriteUInt32(requestHandle);
        writer.WriteUInt32(0); // returnDiagnostics
        writer.WriteString(null); // auditEntryId
        writer.WriteUInt32(timeout == Timeout.InfiniteTimeSpan
            ? 0u
            : checked((uint)Math.Ceiling(timeout.TotalMilliseconds)));
        writer.WriteExtensionObject(null); // additionalHeader
    }

    internal static OpcUaResponseHeader ReadResponseHeader(ref UaBinaryReader reader)
    {
        var timestamp = reader.ReadDateTime();
        var requestHandle = reader.ReadUInt32();
        var serviceResult = reader.ReadUInt32();
        SkipDiagnosticInfo(ref reader, 0);
        var stringCount = reader.ReadInt32();
        if (stringCount < -1) throw new InvalidDataException("Invalid OPC UA response string table length.");
        for (var i = 0; i < stringCount; i++) _ = reader.ReadString();
        _ = reader.ReadExtensionObject();
        return new OpcUaResponseHeader(timestamp, requestHandle, serviceResult);
    }

    /// <summary>
    ///     Exceptions the UA Binary decoders raise for malformed, truncated or unsupported input. Every response
    ///     decode site maps these to a structured <see cref="DriverError" /> instead of letting them escape.
    /// </summary>
    internal static bool IsDecodeFailure(Exception ex) =>
        ex is InvalidDataException or EndOfStreamException or NotSupportedException;

    private static void SkipDiagnosticInfo(ref UaBinaryReader reader, int depth)
    {
        OpcUaBinaryCodec.SkipDiagnosticInfo(ref reader, depth);
    }

    private async ValueTask<byte[]> ReceiveMessageAsync(Socket socket, CancellationToken cancellationToken,
        TimeSpan? timeoutOverride = null)
    {
        var timeoutDuration = timeoutOverride ?? _options.RequestTimeout;
        var firstChunk =
            UnprotectIncomingChunk(await ReceiveChunkAsync(socket, cancellationToken, timeoutDuration)
                .ConfigureAwait(false));
        if (firstChunk[3] == (byte)'F') return firstChunk;
        if (firstChunk[3] != (byte)'C' || (!IsType(firstChunk, "MSG"u8) && !IsType(firstChunk, "OPN"u8)))
            throw new InvalidDataException(
                $"OPC UA message chunk type '{(char)firstChunk[3]}' is invalid for {Encoding.ASCII.GetString(firstChunk, 0, 3)}.");

        var payloadOffset = GetSecureConversationPayloadOffset(firstChunk);
        var channelId = BinaryPrimitives.ReadUInt32LittleEndian(firstChunk.AsSpan(8, 4));
        var assembled = new List<byte>(firstChunk.Length);
        assembled.AddRange([.. firstChunk.AsSpan(0, payloadOffset)]);
        assembled.AddRange([.. firstChunk.AsSpan(payloadOffset)]);
        var maximumChunks = GetMaximumChunkCount();
        for (var chunkIndex = 1;; chunkIndex++)
        {
            if (chunkIndex >= maximumChunks)
                throw new InvalidDataException(
                    $"OPC UA response exceeded negotiated maximum chunk count {maximumChunks}.");
            var chunk = UnprotectIncomingChunk(await ReceiveChunkAsync(socket, cancellationToken, timeoutDuration)
                .ConfigureAwait(false));
            if (!chunk.AsSpan(0, 3).SequenceEqual(firstChunk.AsSpan(0, 3)) || chunk[3] is not ((byte)'C' or (byte)'F'))
                throw new InvalidDataException(
                    "OPC UA response chunks changed message type or have an invalid chunk type.");
            if (BinaryPrimitives.ReadUInt32LittleEndian(chunk.AsSpan(8, 4)) != channelId)
                throw new InvalidDataException("OPC UA response chunks changed SecureChannelId.");
            var chunkPayloadOffset = GetSecureConversationPayloadOffset(chunk);
            if (!chunk.AsSpan(chunkPayloadOffset - 4, 4).SequenceEqual(firstChunk.AsSpan(payloadOffset - 4, 4)))
                throw new InvalidDataException("OPC UA response chunks changed RequestId.");
            if (assembled.Count > GetMessageHardLimit() - (chunk.Length - chunkPayloadOffset))
                throw new InvalidDataException(
                    $"OPC UA assembled response exceeds configured maximum {GetMessageHardLimit()}.");
            assembled.AddRange([.. chunk.AsSpan(chunkPayloadOffset)]);
            if (chunk[3] == (byte)'F') break;
        }

        var message = assembled.ToArray();
        message[3] = (byte)'F';
        BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4, 4), checked((uint)message.Length));
        return message;
    }

    private async ValueTask<byte[]> ReceiveChunkAsync(Socket socket, CancellationToken cancellationToken,
        TimeSpan timeoutDuration)
    {
        var header = new byte[8];
        using (var timeout = TimeoutTokenScope.Create(cancellationToken, timeoutDuration))
        {
            await SocketIo.ReceiveExactlyAsync(socket, header, timeout.Token).ConfigureAwait(false);
        }

        var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
        if (size < 8) throw new InvalidDataException($"OPC UA message size {size} is smaller than the 8-byte header.");
        var hardLimit = GetMessageHardLimit();
        if (size > hardLimit)
            throw new InvalidDataException($"OPC UA message size {size} exceeds configured maximum {hardLimit}.");
        if (size > int.MaxValue) throw new InvalidDataException("OPC UA message size exceeds Int32 capacity.");
        var message = new byte[(int)size];
        header.CopyTo(message, 0);
        if (message.Length > 8)
        {
            using var timeout = TimeoutTokenScope.Create(cancellationToken, timeoutDuration);
            await SocketIo.ReceiveExactlyAsync(socket, message.AsMemory(8), timeout.Token).ConfigureAwait(false);
        }

        return message;
    }

    private int GetSecureConversationPayloadOffset(byte[] chunk)
    {
        if (chunk.Length < 24)
            throw new InvalidDataException("OPC UA SecureConversation chunk is shorter than its required headers.");
        if (IsType(chunk, "MSG"u8)) return 24;
        if (!IsType(chunk, "OPN"u8)) throw new InvalidDataException("Only MSG and OPN chunks can be assembled.");
        var reader = new UaBinaryReader(chunk.AsSpan(8));
        _ = reader.ReadUInt32();
        _ = reader.ReadString();
        _ = reader.ReadByteString();
        _ = reader.ReadByteString();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        return 8 + reader.Position;
    }

    private int GetMaximumChunkCount()
    {
        var negotiated = NegotiatedLimits?.MaxChunkCount ?? 0;
        var configured = _options.MaxChunkCount;
        var limit = negotiated == 0 ? configured : configured == 0 ? negotiated : Math.Min(negotiated, configured);
        return limit == 0 ? 4096 : checked((int)Math.Min(limit, int.MaxValue));
    }

    private uint GetMessageHardLimit()
    {
        return _options.MaxMessageSize == 0 ? 64u * 1024 * 1024 : _options.MaxMessageSize;
    }

    private byte[] UnprotectIncomingChunk(byte[] message)
    {
        if (_options.SecurityMode == OpcUaMessageSecurityMode.None) return message;
        if (IsType(message, "OPN"u8))
        {
            var clientCertificate = _options.ApplicationIdentity?.Certificate
                                    ?? throw new InvalidDataException(
                                        "Secure OPN response requires the configured client certificate.");
            var serverCertificate = _options.GetEndpointServerCertificate()
                                    ?? throw new InvalidDataException("Endpoint server certificate is missing.");
            var trust = OpcUaCertificateValidator.ValidateServerCertificate(serverCertificate,
                _options.EndpointUrl.Host, _options.CertificateTrust, GetSecurityPolicy());
            if (!trust.IsSuccess) throw new InvalidDataException(trust.Error.ToUserMessage());
            var decoded = OpcUaAsymmetricOpenCodec.Decode(message, clientCertificate, serverCertificate,
                GetSecurityPolicy());
            return OpcUaAsymmetricOpenCodec.ToUnsecuredEnvelope(decoded);
        }

        if (!IsType(message, "MSG"u8) && !IsType(message, "CLO"u8)) return message;
        if (message.Length < 16) throw new InvalidDataException("Secure MSG is shorter than its security header.");
        var tokenId = BinaryPrimitives.ReadUInt32LittleEndian(message.AsSpan(12, 4));
        OpcUaSymmetricKeySet keys;
        if (SecureChannel?.TokenId == tokenId && _serverSymmetricKeys is not null)
            keys = _serverSymmetricKeys;
        else if (_previousTokenId == tokenId && DateTime.UtcNow <= _previousTokenExpiresUtc &&
                 _previousServerSymmetricKeys is not null)
            keys = _previousServerSymmetricKeys;
        else
            throw new InvalidDataException($"Secure MSG uses unknown or expired SecurityTokenId {tokenId}.");
        return OpcUaSymmetricMessageCodec.DecodeToUnsecuredEnvelope(message, _options.SecurityMode, keys,
            GetSecurityPolicy());
    }

    private OpcUaSecurityPolicyProfile GetSecurityPolicy()
    {
        if (OpcUaSecurityPolicies.TryResolve(_options.SecurityPolicyUri, out var policy) && policy is not null)
            return policy;
        throw new InvalidOperationException($"SecurityPolicyUri '{_options.SecurityPolicyUri}' is not supported.");
    }

    private DriverResult ParseTransportError(byte[] message, DriverOperation operation)
    {
        try
        {
            var reader = new UaBinaryReader(message.AsSpan(8));
            var status = reader.ReadUInt32();
            var reason = reader.ReadString() ?? "Server returned an OPC UA transport error.";
            return DriverResult.Failure(new DriverError(
                DriverErrorKind.Protocol, "UA_TCP_ERROR", reason, unchecked((int)status), operation,
                OpcUaProtocol.Name, _options.EndpointUrl.ToString(),
                NativeMeaning: $"OPC UA StatusCode 0x{status:X8}",
                Cause: "The server rejected the UA-TCP connection or negotiated parameters.",
                SuggestedAction:
                "Verify endpoint URL, protocol limits and server availability; inspect the native StatusCode.",
                Retry: DriverRetryDisposition.FixConfiguration,
                Connection: DriverConnectionDisposition.Broken));
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return Failure(DriverErrorKind.Protocol, "UA_TCP_ERROR_DECODE", ex.Message, operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        }
    }

    private DriverResult ServiceFailure(uint status, string code, string message, DriverOperation operation)
    {
        if (InvalidatesSessionOrChannel(status))
            return DriverResult.Failure(new DriverError(
                DriverErrorKind.Device, code, message, unchecked((int)status), operation,
                OpcUaProtocol.Name, _options.EndpointUrl.ToString(), NativeMeaning: $"OPC UA StatusCode 0x{status:X8}",
                Cause: "The server no longer accepts this client's Session or SecureChannel.",
                SuggestedAction: "Reconnect, create and activate a new Session, then retry requests that are safe to repeat.",
                Retry: DriverRetryDisposition.ReconnectAndRetry, Connection: DriverConnectionDisposition.Broken));
        return DriverResult.Failure(new DriverError(
            DriverErrorKind.Device, code, message, unchecked((int)status), operation,
            OpcUaProtocol.Name, _options.EndpointUrl.ToString(), NativeMeaning: $"OPC UA StatusCode 0x{status:X8}",
            Cause: "The OPC UA service returned a bad StatusCode.",
            SuggestedAction: "Inspect the StatusCode and server diagnostics.",
            Retry: DriverRetryDisposition.FixConfiguration, Connection: DriverConnectionDisposition.Healthy));
    }

    private DriverResult Failure(DriverErrorKind kind, string code, string message, DriverOperation operation,
        DriverRetryDisposition retry = DriverRetryDisposition.Unknown,
        DriverConnectionDisposition connection = DriverConnectionDisposition.Unknown)
    {
        return DriverResult.Failure(new DriverError(
            kind, code, message, Operation: operation, Protocol: OpcUaProtocol.Name,
            Endpoint: _options.EndpointUrl.ToString(),
            Cause: "OPC UA transport or SecureChannel negotiation did not satisfy the protocol contract.",
            SuggestedAction: "Verify endpoint/server settings and inspect the structured error fields.", Retry: retry,
            Connection: connection));
    }

    /// <summary>
    ///     StatusCodes after which the Session or SecureChannel cannot serve further requests
    ///     (OPC 10000-4 §7.38): BadSecureChannelIdInvalid, BadSessionIdInvalid, BadSessionClosed,
    ///     BadSessionNotActivated, BadSecureChannelClosed and BadSecureChannelTokenUnknown.
    /// </summary>
    internal static bool InvalidatesSessionOrChannel(uint status) =>
        (status & 0xFFFF0000u) is 0x80220000u or 0x80250000u or 0x80260000u or 0x80270000u or 0x80860000u
            or 0x80870000u;

    /// <summary>
    ///     The error a Session-bound service reports when it cannot run. A lost SecureChannel is a
    ///     transport failure; a live channel without an activated Session also needs the caller to
    ///     reconnect and open a Session. Both tell the caller to reconnect.
    /// </summary>
    private DriverError SessionServiceUnavailable(DriverOperation operation, string service)
    {
        return _socket is null || SecureChannel is null
            ? Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
                $"SecureChannel is not available for {service}.", operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error
            : Failure(DriverErrorKind.Configuration, "UA_SESSION_NOT_ACTIVE",
                $"An activated Session is required for {service}.", operation,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error;
    }

    private static bool IsType(byte[] message, ReadOnlySpan<byte> type)
    {
        return message.Length >= 4 && message.AsSpan(0, 3).SequenceEqual(type);
    }

    private uint NextSequenceNumber()
    {
        var next = unchecked(++_sequenceNumber);
        if (next == 0) next = ++_sequenceNumber;
        return next;
    }

    private uint NextRequestId()
    {
        var next = unchecked(++_requestId);
        if (next == 0) next = ++_requestId;
        return next;
    }

    private void CloseSocket()
    {
        StopSecureChannelRenewal();
        var cancellation = Interlocked.Exchange(ref _receiveLoopCancellation, null);
        Interlocked.Exchange(ref _receiveLoop, null);
        if (cancellation is not null)
        {
            cancellation.Cancel();
            cancellation.Dispose();
        }

        // A Session the server still holds is kept, so the next connection can activate it again instead of leaving it
        // orphaned until its timeout; so is one whose ActivateSession response was lost, because the server may have
        // activated it.
        if (Session is { } active && (active.IsActivated || _activationInFlight) && _options.ReactivateSessionOnReconnect && !_disposed)
        {
            _lostSession = active;
            ReplaceSecret(ref _lostSessionServerNonce, _sessionServerNonce?.ToArray());
        }

        Session = null;
        SecureChannel = null;
        NegotiatedLimits = null;
        ReplaceSecret(ref _clientSecureChannelNonce, null);
        ReplaceSecret(ref _serverSecureChannelNonce, null);
        ReplaceSecret(ref _sessionClientNonce, null);
        ReplaceSecret(ref _sessionServerNonce, null);
        _clientSymmetricKeys?.Dispose();
        _clientSymmetricKeys = null;
        _serverSymmetricKeys?.Dispose();
        _serverSymmetricKeys = null;
        _previousClientSymmetricKeys?.Dispose();
        _previousClientSymmetricKeys = null;
        _previousServerSymmetricKeys?.Dispose();
        _previousServerSymmetricKeys = null;
        _previousTokenId = 0;
        _previousTokenExpiresUtc = DateTime.MinValue;
        CompletePendingRequests(new DriverError(
            DriverErrorKind.Transport, "UA_CHANNEL_CLOSED",
            "The OPC UA SecureChannel was closed before the service response arrived.",
            Operation: DriverOperation.Disconnect, Protocol: OpcUaProtocol.Name,
            Endpoint: _options.EndpointUrl.ToString(),
            Cause: "The connection was closed or replaced while requests were pending.",
            SuggestedAction: "Reconnect and retry requests that are safe to repeat.",
            Retry: DriverRetryDisposition.ReconnectAndRetry,
            Connection: DriverConnectionDisposition.Broken));
        var socket = Interlocked.Exchange(ref _socket, null);
        if (socket is null) return;
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        socket.Dispose();
    }

    private void CloseSocketCore(Socket expectedSocket)
    {
        if (!ReferenceEquals(_socket, expectedSocket)) return;
        CloseSocket();
    }

    private static void ReplaceSecret(ref byte[]? destination, byte[]? replacement)
    {
        if (destination is not null) CryptographicOperations.ZeroMemory(destination);
        destination = replacement;
    }
}

internal readonly record struct OpcUaResponseHeader(DateTime TimestampUtc, uint RequestHandle, uint ServiceResult);
