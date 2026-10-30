using System.Buffers.Binary;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Proxus.Drivers.Core;

namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    public async ValueTask<DriverResult> RenewSecureChannelAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Failure(DriverErrorKind.Disposed, "UA_DISPOSED", "OPC UA client has been disposed.",
                DriverOperation.Negotiate);
        if (_socket is null || SecureChannel is null)
            return Failure(DriverErrorKind.Configuration, "UA_CHANNEL_REQUIRED",
                "An open SecureChannel is required before renewal.", DriverOperation.Negotiate,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        var socket = _socket;
        if (socket is null || SecureChannel is null)
            return Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST", "SecureChannel closed before renewal.",
                DriverOperation.Negotiate,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        return await RequestSecureChannelAsync(socket, OpcUaSecurityTokenRequestType.Renew, cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<DriverResult> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return Failure(DriverErrorKind.Disposed, "UA_DISPOSED", "OPC UA client has been disposed.",
                DriverOperation.Connect);
        if (!IsConnected || _socket is null || SecureChannel is null)
            return Failure(DriverErrorKind.Configuration, "UA_CHANNEL_REQUIRED",
                "A SecureChannel must be open before creating a Session.", DriverOperation.Handshake,
                DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        if (Session.HasValue) return DriverResult.Success();

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (Session.HasValue) return DriverResult.Success();
            var socket = _socket;
            if (socket is null || SecureChannel is null)
                return Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
                    "SecureChannel closed before CreateSession.", DriverOperation.Handshake);
            var requestId = NextRequestId();
            var requestHandle = requestId;
            using var writer = BeginServiceRequest(OpcUaProtocol.CreateSessionRequestBinary,
                IndustrialNodeId.Numeric(0, 0), requestId, requestHandle);
            WriteApplicationDescription(writer);
            writer.WriteString(null); // serverUri
            writer.WriteString(_options.EndpointUrl.ToString());
            writer.WriteString(_options.SessionName ?? $"Proxus-{requestHandle}");
            if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
            {
                writer.WriteNullByteString();
                writer.WriteNullByteString();
            }
            else
            {
                ReplaceSecret(ref _sessionClientNonce, RandomNumberGenerator.GetBytes(32));
                writer.WriteByteString(_sessionClientNonce);
                writer.WriteByteString(_options.ApplicationIdentity!.Certificate.RawDataMemory.Span);
            }

            writer.WriteDouble(_options.RequestedSessionTimeoutMilliseconds);
            writer.WriteUInt32(_options.MaxResponseMessageSize);
            var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
                    OpcUaProtocol.CreateSessionResponseBinary, DriverOperation.Handshake, cancellationToken)
                .ConfigureAwait(false);
            if (!envelope.IsSuccess) return DriverResult.Failure(envelope.Error);

            try
            {
                var reader = new UaBinaryReader(envelope.Value.Body.Span);
                var sessionId = reader.ReadNodeId();
                var authToken = reader.ReadNodeId();
                var revisedTimeout = reader.ReadDouble();
                var serverNonce = reader.ReadByteString();
                var serverCertificate = reader.ReadByteString();
                var endpointSelection = ReadEndpointArrayAndSelect(ref reader);
                SkipSignedSoftwareCertificates(ref reader);
                var serverSignature = ReadSignatureData(ref reader);
                var maxRequestMessageSize = reader.ReadUInt32();
                if (_options.SecurityMode != OpcUaMessageSecurityMode.None)
                {
                    ValidateCreateSessionSecurity(serverNonce, serverCertificate, serverSignature);
                    ReplaceSecret(ref _sessionServerNonce, serverNonce);
                }

                if (IsNullNodeId(sessionId) || IsNullNodeId(authToken))
                    throw new InvalidDataException("CreateSession returned a null SessionId or AuthenticationToken.");
                if (endpointSelection is null)
                    return Failure(DriverErrorKind.NotSupported, "UA_ENDPOINT_NOT_FOUND",
                        "CreateSession did not return the configured security endpoint.",
                        DriverOperation.Handshake,
                        DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Healthy);
                Session = new OpcUaSessionInfo(sessionId, authToken, revisedTimeout, maxRequestMessageSize,
                    endpointSelection.Value.AnonymousPolicyId ?? string.Empty, false,
                    endpointSelection.Value.UserIdentityTokens);
                return DriverResult.Success();
            }
            catch (Exception ex) when (IsDecodeFailure(ex) || ex is CryptographicException)
            {
                return Failure(DriverErrorKind.Protocol, "UA_CREATE_SESSION_DECODE", ex.Message,
                    DriverOperation.Handshake,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    public ValueTask<DriverResult> ActivateAnonymousSessionAsync(CancellationToken cancellationToken = default)
    {
        return ActivateSessionAsync(OpcUaUserTokenType.Anonymous, null, null, null, cancellationToken);
    }

    public ValueTask<DriverResult> ActivateUserNameSessionAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);
        ArgumentNullException.ThrowIfNull(password);
        return ActivateSessionAsync(OpcUaUserTokenType.UserName, userName, password, null, cancellationToken);
    }

    public ValueTask<DriverResult> ActivateCertificateSessionAsync(
        X509Certificate2 userCertificate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userCertificate);
        return ActivateSessionAsync(OpcUaUserTokenType.Certificate, null, null, userCertificate, cancellationToken);
    }

    // Activates the Session of a lost SecureChannel again when there is one (OPC 10000-4 5.6.3), else creates a new one.
    private async ValueTask<DriverResult> OpenSessionAsync(Func<CancellationToken, ValueTask<DriverResult>> activate,
        CancellationToken cancellationToken)
    {
        if (_lostSession is { } lost && Session is null && IsConnected)
        {
            _lostSession = null;
            var nonce = _lostSessionServerNonce;
            _lostSessionServerNonce = null;
            var kept = nonce?.ToArray();
            ReplaceSecret(ref _sessionServerNonce, nonce);
            Session = lost with { IsActivated = false };
            var activated = await activate(cancellationToken).ConfigureAwait(false);
            if (activated.IsSuccess)
            {
                ReplaceSecret(ref kept, null);
                SessionReactivated = true;
                await DeleteOrphanedSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
                return activated;
            }

            Session = null;
            if (!IsConnected)
            {
                // The new channel broke before the server answered: try the same Session on the next connection.
                _lostSession = lost;
                ReplaceSecret(ref _lostSessionServerNonce, kept);
                return activated;
            }

            // The server no longer knows the Session (BadSessionIdInvalid, BadSessionClosed, ...): create a new one.
            ReplaceSecret(ref kept, null);
        }

        SessionReactivated = false;
        _sessionSubscriptions.Clear();
        var create = await CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        if (!create.IsSuccess) return create;
        return await activate(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DriverResult> ActivateSessionAsync(
        OpcUaUserTokenType tokenType,
        string? userName,
        string? password,
        X509Certificate2? userCertificate,
        CancellationToken cancellationToken)
    {
        if (_socket is null || SecureChannel is null)
            return Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST", "SecureChannel is not available.",
                DriverOperation.Handshake, DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
        if (Session is not { } session)
            return Failure(DriverErrorKind.Configuration, "UA_SESSION_REQUIRED",
                "CreateSession must succeed before ActivateSession.", DriverOperation.Handshake,
                DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Healthy);
        if (session.IsActivated) return DriverResult.Success();

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session = Session!.Value;
            if (session.IsActivated) return DriverResult.Success();
            var tokenPolicy = session.UserIdentityTokens?.FirstOrDefault(policy => policy.TokenType == tokenType);
            if (tokenPolicy is null)
                return Failure(DriverErrorKind.NotSupported, "UA_IDENTITY_TOKEN_UNAVAILABLE",
                    $"Server endpoint does not advertise a {tokenType} user identity token.",
                    DriverOperation.Handshake, DriverRetryDisposition.FixConfiguration,
                    DriverConnectionDisposition.Healthy);
            // Build the identity-bearing body before BeginServiceRequest consumes a sequence number, so a
            // token or signature failure is reported without leaving a gap on the SecureChannel.
            using var parameters = new UaBinaryWriter(1024);
            try
            {
                if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
                {
                    WriteSignatureData(parameters, null, null);
                }
                else
                {
                    var signature = CreateActivateSessionSignature();
                    WriteSignatureData(parameters, GetSecurityPolicy().AsymmetricSignatureAlgorithmUri, signature);
                    CryptographicOperations.ZeroMemory(signature);
                }

                parameters.WriteInt32(0); // ClientSoftwareCertificates
                WriteStringArray(parameters, _options.PreferredLocales);
                WriteUserIdentityToken(parameters, tokenPolicy, userName, password, userCertificate);
                if (tokenType == OpcUaUserTokenType.Certificate)
                {
                    var userSignature = CreateUserTokenSignature(userCertificate!, tokenPolicy);
                    WriteSignatureData(parameters, userSignature.Algorithm, userSignature.Signature);
                    CryptographicOperations.ZeroMemory(userSignature.Signature);
                }
                else
                {
                    WriteSignatureData(parameters, null, null);
                }
            }
            catch (Exception ex) when (ex is CryptographicException or NotSupportedException
                                           or InvalidOperationException or ArgumentException)
            {
                CryptographicOperations.ZeroMemory(MemoryMarshal.AsMemory(parameters.WrittenMemory).Span);
                return Failure(ex is NotSupportedException ? DriverErrorKind.NotSupported : DriverErrorKind.Configuration,
                    "UA_ACTIVATE_SESSION_REQUEST", ex.Message, DriverOperation.Handshake,
                    DriverRetryDisposition.FixConfiguration, DriverConnectionDisposition.Healthy);
            }

            var requestId = NextRequestId();
            var requestHandle = requestId;
            using var writer = BeginServiceRequest(OpcUaProtocol.ActivateSessionRequestBinary,
                session.AuthenticationToken, requestId, requestHandle);
            writer.WriteBytes(parameters.WrittenSpan);
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsMemory(parameters.WrittenMemory).Span);
            DriverResult<OpcUaServiceEnvelope> envelope;
            _activationInFlight = true;
            try
            {
                envelope = await SendServiceAsync(_socket, writer, requestId, requestHandle,
                        OpcUaProtocol.ActivateSessionResponseBinary, DriverOperation.Handshake, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _activationInFlight = false;
            }

            if (!envelope.IsSuccess) return DriverResult.Failure(envelope.Error);
            try
            {
                var reader = new UaBinaryReader(envelope.Value.Body.Span);
                var serverNonce = reader.ReadByteString();
                if (_options.SecurityMode != OpcUaMessageSecurityMode.None)
                {
                    if (serverNonce is null || serverNonce.Length < 32)
                        throw new InvalidDataException("ActivateSession returned an invalid server nonce.");
                    ReplaceSecret(ref _sessionServerNonce, serverNonce);
                }

                var resultCount = ReadArrayLength(ref reader, "ActivateSession.Results", 4);
                for (var i = 0; i < resultCount; i++) _ = reader.ReadUInt32();
                SkipDiagnosticInfoArray(ref reader);
                Session = session with { IsActivated = true };
                return DriverResult.Success();
            }
            catch (Exception ex) when (IsDecodeFailure(ex) || ex is CryptographicException)
            {
                return Failure(DriverErrorKind.Protocol, "UA_ACTIVATE_SESSION_DECODE", ex.Message,
                    DriverOperation.Handshake,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken);
            }
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private void WriteUserIdentityToken(
        UaBinaryWriter writer,
        OpcUaUserTokenPolicy tokenPolicy,
        string? userName,
        string? password,
        X509Certificate2? userCertificate)
    {
        using var body = new UaBinaryWriter(256);
        body.WriteString(tokenPolicy.PolicyId);
        uint typeId;
        switch (tokenPolicy.TokenType)
        {
            case OpcUaUserTokenType.Anonymous:
                typeId = OpcUaProtocol.AnonymousIdentityTokenBinary;
                break;
            case OpcUaUserTokenType.UserName:
                typeId = OpcUaProtocol.UserNameIdentityTokenBinary;
                body.WriteString(userName);
                WriteUserNamePassword(body, tokenPolicy, password ?? string.Empty);
                break;
            case OpcUaUserTokenType.Certificate:
                typeId = OpcUaProtocol.X509IdentityTokenBinary;
                if (userCertificate is null || !userCertificate.HasPrivateKey)
                    throw new CryptographicException("The user identity certificate must contain its private key.");
                body.WriteByteString(userCertificate.RawDataMemory.Span);
                break;
            default:
                throw new NotSupportedException($"User token type {tokenPolicy.TokenType} is not implemented.");
        }

        writer.WriteExtensionObject(new DriverExtensionObject(
            new IndustrialExpandedNodeId(IndustrialNodeId.Numeric(0, typeId)),
            DriverExtensionObjectEncoding.Binary, body.WrittenSpan));
    }

    /// <summary>
    /// Part 4 UserTokenPolicy: an empty SecurityPolicyUri means the token is secured with the endpoint's
    /// (and therefore the SecureChannel's) policy, not with SecurityPolicy None.
    /// </summary>
    internal static string ResolveUserTokenSecurityPolicyUri(
        OpcUaUserTokenPolicy tokenPolicy,
        string endpointSecurityPolicyUri) =>
        string.IsNullOrEmpty(tokenPolicy.SecurityPolicyUri) ? endpointSecurityPolicyUri : tokenPolicy.SecurityPolicyUri;

    /// <summary>
    ///     Resolves the SecurityPolicy that signs an X509IdentityToken: the UserTokenPolicy's SecurityPolicyUri,
    ///     or the endpoint policy when it is empty. SecurityPolicy None cannot produce a token signature.
    /// </summary>
    internal static OpcUaSecurityPolicyProfile ResolveUserTokenSignaturePolicy(
        OpcUaUserTokenPolicy tokenPolicy,
        string endpointSecurityPolicyUri)
    {
        var uri = ResolveUserTokenSecurityPolicyUri(tokenPolicy, endpointSecurityPolicyUri);
        if (OpcUaSecurityPolicies.TryResolve(uri, out var policy) && policy is not null)
            return policy;
        throw new NotSupportedException(
            $"X509IdentityToken SecurityPolicyUri '{uri}' cannot sign the user token; Basic256Sha256, Aes128_Sha256_RsaOaep or Aes256_Sha256_RsaPss is required.");
    }

    /// <summary>
    ///     Builds the ActivateSession userTokenSignature. Part 4 X509IdentityToken: it follows the token policy,
    ///     which may differ from the SecureChannel policy.
    /// </summary>
    internal static (string Algorithm, byte[] Signature) BuildUserTokenSignature(
        OpcUaUserTokenPolicy tokenPolicy,
        string endpointSecurityPolicyUri,
        X509Certificate2 userCertificate,
        ReadOnlySpan<byte> serverCertificate,
        ReadOnlySpan<byte> serverNonce)
    {
        var tokenSecurityPolicy = ResolveUserTokenSignaturePolicy(tokenPolicy, endpointSecurityPolicyUri);
        return (tokenSecurityPolicy.AsymmetricSignatureAlgorithmUri,
            SignUserToken(userCertificate, serverCertificate, serverNonce, tokenSecurityPolicy));
    }

    /// <summary>Signs the server certificate and server nonce with the user key under the token policy.</summary>
    private static byte[] SignUserToken(
        X509Certificate2 userCertificate,
        ReadOnlySpan<byte> serverCertificate,
        ReadOnlySpan<byte> serverNonce,
        OpcUaSecurityPolicyProfile tokenSecurityPolicy)
    {
        var signedData = new byte[serverCertificate.Length + serverNonce.Length];
        try
        {
            serverCertificate.CopyTo(signedData);
            serverNonce.CopyTo(signedData.AsSpan(serverCertificate.Length));
            using var userKey = userCertificate.GetRSAPrivateKey()
                                ?? throw new CryptographicException(
                                    "User identity certificate does not contain an RSA private key.");
            return OpcUaSecurityCrypto.SignRsaSha256(userKey, signedData, tokenSecurityPolicy);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signedData);
        }
    }

    private void WriteUserNamePassword(UaBinaryWriter body, OpcUaUserTokenPolicy tokenPolicy, string password)
    {
        var tokenPolicyUri = ResolveUserTokenSecurityPolicyUri(tokenPolicy, _options.SecurityPolicyUri);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            if (string.Equals(tokenPolicyUri, OpcUaProtocol.SecurityPolicyNone, StringComparison.Ordinal))
            {
                if (_options.SecurityMode != OpcUaMessageSecurityMode.SignAndEncrypt)
                    throw new CryptographicException(
                        "A plaintext UserNameIdentityToken is allowed only inside a SignAndEncrypt channel.");
                body.WriteByteString(passwordBytes);
                body.WriteString(null);
                return;
            }

            if (!OpcUaSecurityPolicies.TryResolve(tokenPolicyUri, out var tokenSecurityPolicy) ||
                tokenSecurityPolicy is null)
                throw new NotSupportedException(
                    $"UserNameIdentityToken SecurityPolicyUri '{tokenPolicyUri}' is not supported.");
            var serverNonce = _sessionServerNonce ??
                              throw new InvalidOperationException("CreateSession server nonce is missing.");
            using var plain = new UaBinaryWriter(passwordBytes.Length + serverNonce.Length + 8);
            var combined = new byte[passwordBytes.Length + serverNonce.Length];
            try
            {
                passwordBytes.CopyTo(combined, 0);
                serverNonce.CopyTo(combined, passwordBytes.Length);
                plain.WriteByteString(combined);
                using var serverKey = (_options.GetEndpointServerCertificate()
                                       ?? throw new CryptographicException("Endpoint server certificate is missing."))
                                      .GetRSAPublicKey()
                                      ?? throw new CryptographicException(
                                          "Server certificate does not contain an RSA public key.");
                var encrypted = OpcUaSecurityCrypto.EncryptRsa(serverKey, plain.WrittenSpan, tokenSecurityPolicy);
                try
                {
                    body.WriteByteString(encrypted);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(encrypted);
                }

                body.WriteString(tokenSecurityPolicy.AsymmetricEncryptionAlgorithmUri);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(combined);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public async ValueTask<DriverResult> CloseSessionAsync(bool deleteSubscriptions = true,
        CancellationToken cancellationToken = default)
    {
        if (Session is null) return DriverResult.Success();
        if (_socket is null || SecureChannel is null)
        {
            Session = null;
            return DriverResult.Success();
        }

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await CloseSessionCoreAsync(deleteSubscriptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private async ValueTask<DriverResult> CloseSessionCoreAsync(bool deleteSubscriptions,
        CancellationToken cancellationToken)
    {
        if (Session is not { } session) return DriverResult.Success();
        var socket = _socket;
        if (socket is null || SecureChannel is null)
        {
            Session = null;
            return DriverResult.Success();
        }

        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.CloseSessionRequestBinary, session.AuthenticationToken,
            requestId, requestHandle);
        writer.WriteBoolean(deleteSubscriptions);
        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
                OpcUaProtocol.CloseSessionResponseBinary, DriverOperation.Disconnect, cancellationToken)
            .ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult.Failure(envelope.Error);
        Session = null;
        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> SendCloseSecureChannelCoreAsync(CancellationToken cancellationToken)
    {
        var socket = _socket;
        var channel = SecureChannel;
        if (socket is null || channel is null) return DriverResult.Success();
        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = new UaBinaryWriter(256);
        writer.WriteBytes("CLOF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(channel.Value.ChannelId);
        writer.WriteUInt32(channel.Value.TokenId);
        writer.WriteUInt32(0); // SequenceNumber, assigned under the send gate below
        writer.WriteUInt32(requestId);
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, OpcUaProtocol.CloseSecureChannelRequestBinary));
        WriteRequestHeader(writer, IndustrialNodeId.Numeric(0, 0), requestHandle, _options.RequestTimeout);
        try
        {
            using var timeout = TimeoutTokenScope.Create(cancellationToken, _options.RequestTimeout);
            await _sendGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                writer.PatchUInt32(16, NextSequenceNumber());
                FinalizeServiceMessage(writer);
                await SocketIo.SendAllAsync(socket, writer.WrittenMemory, timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }

            return DriverResult.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(DriverErrorKind.Cancelled, "UA_CLOSE_CHANNEL_CANCELLED",
                "CloseSecureChannel was cancelled by the caller.", DriverOperation.Disconnect,
                DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Broken);
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
        {
            return Failure(DriverErrorKind.Transport, "UA_CLOSE_CHANNEL_TRANSPORT", ex.Message,
                DriverOperation.Disconnect, DriverRetryDisposition.DoNotRetry, DriverConnectionDisposition.Broken);
        }
    }

    public ValueTask<DriverResult> OpenAnonymousSessionAsync(CancellationToken cancellationToken = default) =>
        OpenSessionAsync(ActivateAnonymousSessionAsync, cancellationToken);

    public ValueTask<DriverResult> OpenUserNameSessionAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default) =>
        OpenSessionAsync(token => ActivateUserNameSessionAsync(userName, password, token), cancellationToken);

    public async ValueTask<DriverResult> OpenCertificateSessionAsync(
        X509Certificate2 userCertificate,
        CancellationToken cancellationToken = default)
    {
        return await OpenSessionAsync(token => ActivateCertificateSessionAsync(userCertificate, token), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask<DriverResult<DriverSample>> ReadAsync(OpcUaNodeDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        var buffer = new DriverSample[1];
        var result = await ReadAsync(new[] { descriptor }, buffer, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? DriverResult<DriverSample>.Success(buffer[0])
            : DriverResult<DriverSample>.Failure(result.Error);
    }

    public async ValueTask<DriverResult<int>> ReadAsync(ReadOnlyMemory<OpcUaNodeDescriptor> nodes,
        Memory<DriverSample> destination, CancellationToken cancellationToken = default)
    {
        if (nodes.IsEmpty) return DriverResult<int>.Success(0);
        foreach (var node in nodes.Span)
            if (!IsStandardAttributeId(node.AttributeId))
                return DriverResult<int>.Failure(new DriverError(DriverErrorKind.Configuration, "UA_ATTRIBUTE_ID",
                    $"Unknown OPC UA AttributeId {(uint)node.AttributeId}.", Operation: DriverOperation.ReadBatch,
                    Protocol: OpcUaProtocol.Name, Endpoint: _options.EndpointUrl.ToString(), Address: node.ToString(),
                    SuggestedAction: "Use a standard OPC UA AttributeId (1..27).",
                    Retry: DriverRetryDisposition.FixConfiguration, Connection: DriverConnectionDisposition.Unchanged));
        if (destination.Length < nodes.Length)
            return DriverResult<int>.Failure(new DriverError(DriverErrorKind.Configuration, "UA_READ_DESTINATION",
                "Destination is smaller than NodesToRead.", Operation: DriverOperation.ReadBatch,
                Protocol: OpcUaProtocol.Name, Endpoint: _options.EndpointUrl.ToString(),
                SuggestedAction: "Provide at least one DriverSample slot per node.",
                Retry: DriverRetryDisposition.FixConfiguration, Connection: DriverConnectionDisposition.Unchanged));
        var socket = _socket;
        if (socket is null || SecureChannel is null || !IsSessionActive || Session is not { } session)
            return DriverResult<int>.Failure(SessionServiceUnavailable(DriverOperation.Read, "Read"));

        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.ReadRequestBinary, session.AuthenticationToken,
            requestId, requestHandle);
        writer.WriteDouble(0); // maxAge
        writer.WriteUInt32(2); // TimestampsToReturn.Both
        writer.WriteInt32(nodes.Length);
        foreach (var node in nodes.Span) WriteReadValueId(writer, node);
        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
            OpcUaProtocol.ReadResponseBinary, DriverOperation.ReadBatch, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult<int>.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "Read.Results");
            if (count != nodes.Length)
                throw new InvalidDataException(
                    $"Read returned {count} results for {nodes.Length} requested nodes.");
            for (var i = 0; i < count; i++) destination.Span[i] = OpcUaBinaryCodec.ReadDataValue(ref reader);
            SkipDiagnosticInfoArray(ref reader);
            return DriverResult<int>.Success(count);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<int>.Failure(Failure(DriverErrorKind.Protocol, "UA_READ_DECODE", ex.Message,
                DriverOperation.ReadBatch, DriverRetryDisposition.RetrySameConnection,
                DriverConnectionDisposition.Healthy).Error);
        }
    }

    public ValueTask<DriverResult> WriteAsync(OpcUaNodeDescriptor descriptor, DriverValue value,
        CancellationToken cancellationToken = default)
    {
        return WriteAsync(new[] { OpcUaWriteItem.Value(descriptor, value) }, cancellationToken);
    }

    public async ValueTask<DriverResult> WriteAsync(ReadOnlyMemory<OpcUaWriteItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items.IsEmpty) return DriverResult.Success();
        foreach (var item in items.Span)
            if (!IsStandardAttributeId(item.Descriptor.AttributeId))
                return Failure(DriverErrorKind.Configuration, "UA_ATTRIBUTE_ID",
                    $"Unknown OPC UA AttributeId {(uint)item.Descriptor.AttributeId}.", DriverOperation.Write);
        var socket = _socket;
        if (socket is null || SecureChannel is null || !IsSessionActive || Session is not { } session)
            return DriverResult.Failure(SessionServiceUnavailable(DriverOperation.Write, "Write"));
        var requestId = NextRequestId();
        var requestHandle = requestId;
        using var writer = BeginServiceRequest(OpcUaProtocol.WriteRequestBinary, session.AuthenticationToken,
            requestId, requestHandle);
        writer.WriteInt32(items.Length);
        foreach (var item in items.Span)
        {
            WriteWriteValuePrefix(writer, item.Descriptor);
            OpcUaBinaryCodec.WriteDataValue(writer, item.Sample);
        }

        var envelope = await SendServiceAsync(socket, writer, requestId, requestHandle,
            OpcUaProtocol.WriteResponseBinary, DriverOperation.WriteBatch, cancellationToken).ConfigureAwait(false);
        if (!envelope.IsSuccess) return DriverResult.Failure(envelope.Error);
        try
        {
            var reader = new UaBinaryReader(envelope.Value.Body.Span);
            var count = ReadArrayLength(ref reader, "Write.Results", 4);
            if (count != items.Length)
                throw new InvalidDataException(
                    $"Write returned {count} results for {items.Length} requested nodes.");
            for (var i = 0; i < count; i++)
            {
                var status = reader.ReadUInt32();
                if ((status & 0x80000000u) != 0)
                    return ServiceFailure(status, "UA_WRITE_STATUS",
                        $"Server rejected Write item {i} ({items.Span[i].Descriptor}).", DriverOperation.Write);
            }

            SkipDiagnosticInfoArray(ref reader);
            return DriverResult.Success();
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return Failure(DriverErrorKind.Protocol, "UA_WRITE_DECODE", ex.Message, DriverOperation.WriteBatch,
                DriverRetryDisposition.RetrySameConnection, DriverConnectionDisposition.Healthy);
        }
    }

    private UaBinaryWriter BeginServiceRequest(uint typeId, IndustrialNodeId authToken, uint requestId,
        uint requestHandle, TimeSpan? requestTimeout = null)
    {
        // The channel can close concurrently. A request built without one carries ChannelId 0, which
        // SendServiceAsync rejects as UA_CHANNEL_LOST before anything is sent.
        var channel = SecureChannel;
        var writer = new UaBinaryWriter(1024);
        writer.WriteBytes("MSGF"u8);
        writer.WriteUInt32(0);
        writer.WriteUInt32(channel?.ChannelId ?? 0);
        writer.WriteUInt32(channel?.TokenId ?? 0);
        // SequenceNumber placeholder: SendServiceAsync assigns it under the send gate, so numbers reach the wire in
        // order even when several requests are built at once (OPC 10000-6 6.7.2.4).
        writer.WriteUInt32(0);
        writer.WriteUInt32(requestId);
        writer.WriteNodeId(IndustrialNodeId.Numeric(0, typeId));
        WriteRequestHeader(writer, authToken, requestHandle, requestTimeout ?? _options.RequestTimeout);
        return writer;
    }

    private void FinalizeServiceMessage(UaBinaryWriter writer)
    {
        if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
        {
            writer.PatchUInt32(4, checked((uint)writer.Position));
            return;
        }

        var keys = _clientSymmetricKeys ??
                   throw new InvalidOperationException("SecureChannel client keys are not available.");
        var span = writer.WrittenSpan;
        if (span.Length < 24)
            throw new InvalidOperationException("Service message is shorter than its SecureConversation headers.");
        var protectedMessage = OpcUaSymmetricMessageCodec.Encode(
            span[..4],
            BinaryPrimitives.ReadUInt32LittleEndian(span[8..12]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[12..16]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[16..20]),
            BinaryPrimitives.ReadUInt32LittleEndian(span[20..24]),
            span[24..],
            _options.SecurityMode,
            keys,
            GetSecurityPolicy());
        writer.ReplaceWritten(protectedMessage);
        CryptographicOperations.ZeroMemory(protectedMessage);
    }

    /// <summary>
    ///     Numbers, protects and sends a request built by <see cref="BeginServiceRequest" />. The SequenceNumber is
    ///     assigned and the message signed or encrypted under the send gate, so concurrent requests reach the server
    ///     with increasing SequenceNumbers. The socket and channel are re-checked here, because the receive loop can
    ///     close them at any time; a lost channel is reported as UA_CHANNEL_LOST.
    /// </summary>
    private async ValueTask<DriverResult<OpcUaServiceEnvelope>> SendServiceAsync(Socket? socket, UaBinaryWriter writer,
        uint requestId, uint requestHandle, uint expectedTypeId, DriverOperation operation,
        CancellationToken cancellationToken, TimeSpan? responseTimeout = null)
    {
        var expectedChannelId = BinaryPrimitives.ReadUInt32LittleEndian(writer.WrittenSpan.Slice(8, 4));
        var expectedTokenId = BinaryPrimitives.ReadUInt32LittleEndian(writer.WrittenSpan.Slice(12, 4));
        if (socket is null || !ReferenceEquals(socket, _socket) || SecureChannel is not { } channel ||
            expectedChannelId == 0 || channel.ChannelId != expectedChannelId)
            return DriverResult<OpcUaServiceEnvelope>.Failure(ChannelLost(operation));

        DriverError? NumberAndProtect(UaBinaryWriter message)
        {
            message.PatchUInt32(16, NextSequenceNumber());
            try
            {
                FinalizeServiceMessage(message);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or CryptographicException)
            {
                // The symmetric keys were released by a concurrent channel close.
                return ChannelLost(operation);
            }
        }

        var dispatched = await SendAndAwaitResponseAsync(socket, writer, requestId, operation,
            responseTimeout ?? _options.RequestTimeout, cancellationToken, prepare: NumberAndProtect).ConfigureAwait(false);
        if (!dispatched.IsSuccess) return DriverResult<OpcUaServiceEnvelope>.Failure(dispatched.Error);
        try
        {
            var message = dispatched.Value;
            if (IsType(message, "ERR"u8))
                return DriverResult<OpcUaServiceEnvelope>.Failure(ParseTransportError(message, operation).Error);
            if (!IsType(message, "MSG"u8))
                return DriverResult<OpcUaServiceEnvelope>.Failure(Failure(DriverErrorKind.Protocol, "UA_MSG_EXPECTED",
                    "Server did not return an OPC UA SecureConversation MSG response.", operation,
                    DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
            if (message[3] != (byte)'F')
                return DriverResult<OpcUaServiceEnvelope>.Failure(Failure(DriverErrorKind.NotSupported,
                    "UA_CHUNKING_NOT_IMPLEMENTED",
                    $"Server returned OPC UA chunk type '{(char)message[3]}'; multi-chunk assembly is not enabled in this milestone.",
                    operation, DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error);
            var reader = new UaBinaryReader(message.AsSpan(8));
            var channelId = reader.ReadUInt32();
            var tokenId = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            var responseRequestId = reader.ReadUInt32();
            if (channelId != expectedChannelId || tokenId != expectedTokenId)
                throw new InvalidDataException(
                    $"Response channel/token {channelId}/{tokenId} does not match request {expectedChannelId}/{expectedTokenId}.");
            if (responseRequestId != requestId)
                throw new InvalidDataException(
                    $"Response RequestId {responseRequestId} does not match request {requestId}.");
            var typeId = reader.ReadNodeId();
            var isServiceFault = typeId.NamespaceIndex == 0 && typeId.Kind == IndustrialNodeIdKind.Numeric &&
                                 typeId.NumericIdentifier == OpcUaProtocol.ServiceFaultBinary;
            var isExpected = typeId.NamespaceIndex == 0 && typeId.Kind == IndustrialNodeIdKind.Numeric &&
                             typeId.NumericIdentifier == expectedTypeId;
            if (!isExpected && !isServiceFault)
                throw new InvalidDataException(
                    $"Unexpected service response TypeId {typeId}, expected i={expectedTypeId} or ServiceFault i={OpcUaProtocol.ServiceFaultBinary}.");
            var header = ReadResponseHeader(ref reader);
            if (header.RequestHandle != requestHandle)
                throw new InvalidDataException(
                    $"Response handle {header.RequestHandle} does not match request handle {requestHandle}.");
            if ((header.ServiceResult & 0x80000000u) != 0)
            {
                if (InvalidatesSessionOrChannel(header.ServiceResult))
                    Session = null;
                return DriverResult<OpcUaServiceEnvelope>.Failure(ServiceFailure(header.ServiceResult,
                    isServiceFault ? "UA_SERVICE_FAULT" : "UA_SERVICE_RESULT",
                    isServiceFault
                        ? "OPC UA server returned ServiceFault."
                        : "OPC UA service returned a Bad StatusCode.", operation).Error);
            }

            if (isServiceFault)
                throw new InvalidDataException("OPC UA server returned ServiceFault with a non-Bad serviceResult.");
            return DriverResult<OpcUaServiceEnvelope>.Success(
                new OpcUaServiceEnvelope(message.AsMemory(message.Length - reader.Remaining, reader.Remaining)));
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return DriverResult<OpcUaServiceEnvelope>.Failure(Failure(DriverErrorKind.Protocol, "UA_SERVICE_DECODE",
                    ex.Message, operation, DriverRetryDisposition.RetrySameConnection,
                    DriverConnectionDisposition.Healthy)
                .Error);
        }
    }

    private DriverError ChannelLost(DriverOperation operation)
    {
        return Failure(DriverErrorKind.Transport, "UA_CHANNEL_LOST",
            "The SecureChannel closed before the request could be sent.", operation,
            DriverRetryDisposition.ReconnectAndRetry, DriverConnectionDisposition.Broken).Error;
    }

    private void WriteApplicationDescription(UaBinaryWriter writer)
    {
        writer.WriteString(_options.ApplicationUri);
        writer.WriteString(_options.ProductUri);
        writer.WriteLocalizedText(new IndustrialLocalizedText(null, _options.ApplicationName));
        writer.WriteUInt32(1); // ApplicationType.Client
        writer.WriteString(null);
        writer.WriteString(null);
        writer.WriteInt32(0);
    }

    private OpcUaEndpointSelection? ReadEndpointArrayAndSelect(ref UaBinaryReader reader)
    {
        var count = ReadArrayLength(ref reader, "CreateSession.ServerEndpoints", 20);
        OpcUaEndpointSelection? selected = null;
        for (var i = 0; i < count; i++)
        {
            var endpoint = ReadEndpointDescription(ref reader);
            if (!endpoint.Supports(_options.SecurityMode, _options.SecurityPolicyUri)) continue;
            selected ??= new OpcUaEndpointSelection(
                endpoint.UserIdentityTokens.FirstOrDefault(static policy =>
                    policy.TokenType == OpcUaUserTokenType.Anonymous)?.PolicyId,
                endpoint.UserIdentityTokens);
        }

        return selected;
    }

    private static OpcUaEndpointDescription ReadEndpointDescription(ref UaBinaryReader reader)
    {
        var endpointUrl = reader.ReadString() ?? string.Empty;
        var server = ReadApplicationDescription(ref reader);
        var serverCertificate = reader.ReadByteString();
        var securityModeValue = reader.ReadUInt32();
        if (!Enum.IsDefined(typeof(OpcUaMessageSecurityMode), securityModeValue))
            throw new InvalidDataException($"Endpoint contains unknown MessageSecurityMode {securityModeValue}.");
        var securityPolicyUri = reader.ReadString() ?? string.Empty;
        var policyCount = ReadArrayLength(ref reader, "Endpoint.UserIdentityTokens", 20);
        var policies = new OpcUaUserTokenPolicy[policyCount];
        for (var p = 0; p < policyCount; p++)
        {
            var policyId = reader.ReadString() ?? string.Empty;
            var tokenTypeValue = reader.ReadUInt32();
            if (!IsKnownUserTokenType(tokenTypeValue))
                throw new InvalidDataException($"Endpoint contains unknown UserTokenType {tokenTypeValue}.");
            policies[p] = new OpcUaUserTokenPolicy(policyId, (OpcUaUserTokenType)tokenTypeValue,
                reader.ReadString(), reader.ReadString(), reader.ReadString());
        }

        return new OpcUaEndpointDescription(endpointUrl, server, serverCertificate,
            (OpcUaMessageSecurityMode)securityModeValue, securityPolicyUri, policies,
            reader.ReadString(), reader.ReadByte());
    }

    private static OpcUaApplicationDescription ReadApplicationDescription(ref UaBinaryReader reader)
    {
        var applicationUri = reader.ReadString();
        var productUri = reader.ReadString();
        var applicationName = reader.ReadLocalizedText();
        var typeValue = reader.ReadUInt32();
        if (!IsKnownApplicationType(typeValue))
            throw new InvalidDataException($"ApplicationDescription contains unknown ApplicationType {typeValue}.");
        var gatewayServerUri = reader.ReadString();
        var discoveryProfileUri = reader.ReadString();
        var count = ReadArrayLength(ref reader, "ApplicationDescription.DiscoveryUrls", 4);
        var discoveryUrls = new string[count];
        for (var i = 0; i < count; i++) discoveryUrls[i] = reader.ReadString() ?? string.Empty;
        return new OpcUaApplicationDescription(applicationUri, productUri, applicationName,
            (OpcUaApplicationType)typeValue, gatewayServerUri, discoveryProfileUri, discoveryUrls);
    }

    private static bool IsKnownApplicationType(uint value)
    {
        return value is
            (uint)OpcUaApplicationType.Server or
            (uint)OpcUaApplicationType.Client or
            (uint)OpcUaApplicationType.ClientAndServer or
            (uint)OpcUaApplicationType.DiscoveryServer;
    }

    private static bool IsKnownUserTokenType(uint value)
    {
        return value is
            (uint)OpcUaUserTokenType.Anonymous or
            (uint)OpcUaUserTokenType.UserName or
            (uint)OpcUaUserTokenType.Certificate or
            (uint)OpcUaUserTokenType.IssuedToken;
    }

    private static void SkipSignedSoftwareCertificates(ref UaBinaryReader reader)
    {
        var n = ReadArrayLength(ref reader, "ServerSoftwareCertificates", 8);
        for (var i = 0; i < n; i++)
        {
            _ = reader.ReadByteString();
            _ = reader.ReadByteString();
        }
    }

    private static OpcUaSignatureData ReadSignatureData(ref UaBinaryReader reader)
    {
        return new OpcUaSignatureData(reader.ReadString(), reader.ReadByteString());
    }

    private static void WriteSignatureData(UaBinaryWriter writer, string? algorithm, byte[]? signature)
    {
        writer.WriteString(algorithm);
        if (signature is not null) writer.WriteByteString(signature);
        else writer.WriteNullByteString();
    }

    private void ValidateCreateSessionSecurity(byte[]? serverNonce, byte[]? serverCertificate,
        OpcUaSignatureData signature)
    {
        if (serverNonce is null || serverNonce.Length < 32)
            throw new InvalidDataException("CreateSession returned an invalid server nonce.");
        var expectedServer = _options.GetEndpointServerCertificate()
                             ?? throw new CryptographicException("Endpoint server certificate is missing.");
        if (serverCertificate is null || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(serverCertificate), SHA256.HashData(expectedServer.RawDataMemory.Span)))
            throw new CryptographicException(
                "CreateSession server certificate does not match the trusted endpoint certificate.");
        var securityPolicy = GetSecurityPolicy();
        if (!string.Equals(signature.Algorithm, securityPolicy.AsymmetricSignatureAlgorithmUri,
                StringComparison.Ordinal) || signature.Signature is null)
            throw new CryptographicException("CreateSession returned an unsupported or missing server signature.");
        var clientCertificate = _options.ApplicationIdentity!.Certificate.RawDataMemory.Span;
        var clientNonce = _sessionClientNonce ??
                          throw new InvalidOperationException("CreateSession client nonce is missing.");
        var signedData = new byte[clientCertificate.Length + clientNonce.Length];
        try
        {
            clientCertificate.CopyTo(signedData);
            clientNonce.CopyTo(signedData.AsSpan(clientCertificate.Length));
            using var serverKey = expectedServer.GetRSAPublicKey()
                                  ?? throw new CryptographicException(
                                      "Trusted server certificate does not contain an RSA public key.");
            if (!OpcUaSecurityCrypto.VerifyRsaSha256(serverKey, signedData, signature.Signature, securityPolicy))
                throw new CryptographicException("CreateSession server signature verification failed.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signedData);
        }
    }

    private byte[] CreateActivateSessionSignature()
    {
        var serverCertificate = (_options.GetEndpointServerCertificate()
                                 ?? throw new CryptographicException("Endpoint server certificate is missing."))
            .RawDataMemory.Span;
        var serverNonce = _sessionServerNonce ??
                          throw new InvalidOperationException("CreateSession server nonce is missing.");
        var signedData = new byte[serverCertificate.Length + serverNonce.Length];
        try
        {
            serverCertificate.CopyTo(signedData);
            serverNonce.CopyTo(signedData.AsSpan(serverCertificate.Length));
            using var clientKey = _options.ApplicationIdentity!.Certificate.GetRSAPrivateKey()
                                  ?? throw new CryptographicException(
                                      "Client application certificate does not contain an RSA private key.");
            return OpcUaSecurityCrypto.SignRsaSha256(clientKey, signedData, GetSecurityPolicy());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(signedData);
        }
    }

    private (string Algorithm, byte[] Signature) CreateUserTokenSignature(
        X509Certificate2 userCertificate,
        OpcUaUserTokenPolicy tokenPolicy)
    {
        // The session server nonce and certificate are validated only on Sign or SignAndEncrypt channels.
        if (_options.SecurityMode == OpcUaMessageSecurityMode.None)
            throw new NotSupportedException(
                "X509IdentityToken activation requires a Sign or SignAndEncrypt SecureChannel in this client.");
        var serverCertificate = (_options.GetEndpointServerCertificate()
                                 ?? throw new CryptographicException("Endpoint server certificate is missing."))
            .RawDataMemory.Span;
        var serverNonce = _sessionServerNonce ??
                          throw new InvalidOperationException("CreateSession server nonce is missing.");
        return BuildUserTokenSignature(tokenPolicy, _options.SecurityPolicyUri, userCertificate, serverCertificate,
            serverNonce);
    }

    private static void WriteStringArray(UaBinaryWriter writer, string[]? values)
    {
        if (values is null)
        {
            writer.WriteInt32(-1);
            return;
        }

        writer.WriteInt32(values.Length);
        for (var i = 0; i < values.Length; i++) writer.WriteString(values[i]);
    }

    private static int ReadArrayLength(ref UaBinaryReader reader, string field, int minimumElementBytes = 1)
    {
        var length = reader.ReadArrayLength(field, minimumElementBytes);
        return length < 0 ? 0 : length;
    }

    private static void SkipDiagnosticInfoArray(ref UaBinaryReader reader)
    {
        var n = ReadArrayLength(ref reader, "DiagnosticInfos");
        for (var i = 0; i < n; i++) SkipDiagnosticInfo(ref reader, 0);
    }

    private static void WriteReadValueId(UaBinaryWriter writer, OpcUaNodeDescriptor node)
    {
        WriteReadValueIdPrefix(writer, node);
    }

    private static void WriteReadValueIdPrefix(UaBinaryWriter writer, OpcUaNodeDescriptor node)
    {
        writer.WriteNodeId(node.NodeId);
        writer.WriteUInt32((uint)node.AttributeId);
        writer.WriteString(node.IndexRange);
        // The default DataEncoding is the null QualifiedName (OPC 10000-4 §7.29). Encode an empty
        // name as a null String, as open62541, python-opcua and UA-.NETStandard do.
        if (node.DataEncoding.NamespaceIndex == 0 && string.IsNullOrEmpty(node.DataEncoding.Name))
        {
            writer.WriteUInt16(0);
            writer.WriteString(null);
        }
        else
        {
            writer.WriteQualifiedName(node.DataEncoding);
        }
    }

    private static void WriteWriteValuePrefix(UaBinaryWriter writer, OpcUaNodeDescriptor node)
    {
        writer.WriteNodeId(node.NodeId);
        writer.WriteUInt32((uint)node.AttributeId);
        writer.WriteString(node.IndexRange);
    }

    private static bool IsStandardAttributeId(OpcUaAttributeId value)
    {
        return value switch
        {
            OpcUaAttributeId.NodeId or OpcUaAttributeId.NodeClass or OpcUaAttributeId.BrowseName
                or OpcUaAttributeId.DisplayName or
                OpcUaAttributeId.Description or OpcUaAttributeId.WriteMask or OpcUaAttributeId.UserWriteMask
                or OpcUaAttributeId.IsAbstract or
                OpcUaAttributeId.Symmetric or OpcUaAttributeId.InverseName or OpcUaAttributeId.ContainsNoLoops
                or OpcUaAttributeId.EventNotifier or
                OpcUaAttributeId.Value or OpcUaAttributeId.DataType or OpcUaAttributeId.ValueRank
                or OpcUaAttributeId.ArrayDimensions or
                OpcUaAttributeId.AccessLevel or OpcUaAttributeId.UserAccessLevel
                or OpcUaAttributeId.MinimumSamplingInterval or OpcUaAttributeId.Historizing or
                OpcUaAttributeId.Executable or OpcUaAttributeId.UserExecutable or OpcUaAttributeId.DataTypeDefinition
                or OpcUaAttributeId.RolePermissions or
                OpcUaAttributeId.UserRolePermissions or OpcUaAttributeId.AccessRestrictions
                or OpcUaAttributeId.AccessLevelEx => true,
            _ => false
        };
    }

    private static bool IsNullNodeId(IndustrialNodeId value)
    {
        return value.Kind == IndustrialNodeIdKind.Numeric && value.NamespaceIndex == 0 && value.NumericIdentifier == 0;
    }
}

internal readonly record struct OpcUaServiceEnvelope(ReadOnlyMemory<byte> Body);
