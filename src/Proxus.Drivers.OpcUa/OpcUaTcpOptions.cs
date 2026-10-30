using System.Security.Cryptography.X509Certificates;

namespace Proxus.Drivers.OpcUa;

public sealed record OpcUaTcpOptions
{
    public required Uri EndpointUrl { get; init; }
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan PublishRequestTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public uint ReceiveBufferSize { get; init; } = 64 * 1024;
    public uint SendBufferSize { get; init; } = 64 * 1024;
    public uint MaxMessageSize { get; init; } = 16 * 1024 * 1024;
    public uint MaxChunkCount { get; init; } = 4096;
    public uint RequestedSecureChannelLifetimeMilliseconds { get; init; } = 10 * 60 * 1000;
    public OpcUaMessageSecurityMode SecurityMode { get; init; } = OpcUaMessageSecurityMode.None;
    public string SecurityPolicyUri { get; init; } = OpcUaProtocol.SecurityPolicyNone;
    public OpcUaApplicationIdentity? ApplicationIdentity { get; init; }
    public X509Certificate2? EndpointServerCertificate { get; init; }
    public OpcUaCertificateTrustOptions CertificateTrust { get; init; } = new();
    public string ApplicationUri { get; init; } = "urn:proxus:drivers:opcua:client";
    public string ProductUri { get; init; } = "urn:proxus:drivers";
    public string ApplicationName { get; init; } = "Proxus.Drivers OPC UA Client";
    public string? SessionName { get; init; }
    public double RequestedSessionTimeoutMilliseconds { get; init; } = 60_000;

    /// <summary>
    ///     When the SecureChannel is lost while a Session is active, the next Open*SessionAsync on a new channel first
    ///     activates that Session again (OPC 10000-4 5.6.3) instead of creating a new one, so the server does not keep an
    ///     orphaned Session until its timeout. The Session's subscriptions survive; those without a running pump are
    ///     deleted. When the server no longer knows the Session, a new one is created. Default true.
    /// </summary>
    public bool ReactivateSessionOnReconnect { get; init; } = true;
    public uint MaxResponseMessageSize { get; init; } = 16 * 1024 * 1024;
    public int MaxBrowseContinuationPages { get; init; } = 1024;
    public int MaxBrowseReferences { get; init; } = 1_000_000;
    public string[] PreferredLocales { get; init; } = ["en-US"];

    internal DriverResult Validate()
    {
        if (!string.Equals(EndpointUrl.Scheme, "opc.tcp", StringComparison.OrdinalIgnoreCase))
            return Failure("UA_ENDPOINT_SCHEME", "Endpoint URL must use the opc.tcp scheme.");
        if (EndpointUrl.Port is <= 0 or > 65535)
            return Failure("UA_ENDPOINT_PORT", "Endpoint URL must contain a valid TCP port.");
        if (ReceiveBufferSize < OpcUaProtocol.MinimumBufferSize || SendBufferSize < OpcUaProtocol.MinimumBufferSize)
            return Failure("UA_BUFFER_TOO_SMALL",
                $"OPC UA TCP buffers must be at least {OpcUaProtocol.MinimumBufferSize} bytes for non-ECC profiles.");
        if (MaxMessageSize != 0 && MaxMessageSize < ReceiveBufferSize)
            return Failure("UA_MAX_MESSAGE_SIZE",
                "MaxMessageSize must be zero (unlimited) or at least ReceiveBufferSize.");
        switch (SecurityMode)
        {
            case OpcUaMessageSecurityMode.None:
                if (!string.Equals(SecurityPolicyUri, OpcUaProtocol.SecurityPolicyNone, StringComparison.Ordinal))
                    return Failure("UA_SECURITY_POLICY_MODE", "MessageSecurityMode=None requires SecurityPolicy#None.");
                break;
            case OpcUaMessageSecurityMode.Sign:
            case OpcUaMessageSecurityMode.SignAndEncrypt:
                if (!OpcUaSecurityPolicies.TryResolve(SecurityPolicyUri, out var securityPolicy) ||
                    securityPolicy is null)
                    return Failure("UA_SECURITY_POLICY", $"SecurityPolicyUri '{SecurityPolicyUri}' is not supported.",
                        DriverErrorKind.NotSupported);
                var identity =
                    OpcUaCertificateValidator.ValidateApplicationIdentity(ApplicationIdentity, ApplicationUri,
                        securityPolicy);
                if (!identity.IsSuccess) return identity;
                var endpointCertificate = GetEndpointServerCertificate();
                if (endpointCertificate is null)
                    return Failure("UA_SERVER_CERT_REQUIRED",
                        "Secure OpenSecureChannel requires the endpoint server certificate returned by GetEndpoints or exactly one trusted peer certificate.");
                var server =
                    OpcUaCertificateValidator.ValidateServerCertificate(endpointCertificate, EndpointUrl.Host,
                        CertificateTrust, securityPolicy);
                if (!server.IsSuccess) return server;
                break;
            default:
                return Failure("UA_SECURITY_MODE", $"Unknown OPC UA MessageSecurityMode value {(uint)SecurityMode}.");
        }

        if (RequestedSecureChannelLifetimeMilliseconds == 0)
            return Failure("UA_CHANNEL_LIFETIME", "Requested SecureChannel lifetime must be greater than zero.");
        if (RequestTimeout <= TimeSpan.Zero || PublishRequestTimeout <= TimeSpan.Zero)
            return Failure("UA_REQUEST_TIMEOUT", "RequestTimeout and PublishRequestTimeout must both be positive.");
        if (RequestedSessionTimeoutMilliseconds <= 0 || double.IsNaN(RequestedSessionTimeoutMilliseconds) ||
            double.IsInfinity(RequestedSessionTimeoutMilliseconds))
            return Failure("UA_SESSION_TIMEOUT",
                "Requested session timeout must be a finite positive number of milliseconds.");
        if (MaxBrowseContinuationPages <= 0 || MaxBrowseReferences <= 0)
            return Failure("UA_BROWSE_LIMITS",
                "MaxBrowseContinuationPages and MaxBrowseReferences must both be positive.");
        if (string.IsNullOrWhiteSpace(ApplicationUri) || string.IsNullOrWhiteSpace(ProductUri) ||
            string.IsNullOrWhiteSpace(ApplicationName))
            return Failure("UA_APPLICATION_DESCRIPTION",
                "ApplicationUri, ProductUri and ApplicationName are required.");
        return DriverResult.Success();
    }

    internal X509Certificate2? GetEndpointServerCertificate()
    {
        return EndpointServerCertificate ?? (CertificateTrust.TrustedPeerCertificates.Length == 1
            ? CertificateTrust.TrustedPeerCertificates[0]
            : null);
    }

    private DriverResult Failure(string code, string message, DriverErrorKind kind = DriverErrorKind.Configuration)
    {
        return DriverResult.Failure(new DriverError(
            kind, code, message,
            Operation: DriverOperation.Validate,
            Protocol: OpcUaProtocol.Name,
            Endpoint: EndpointUrl.ToString(),
            Cause: "OPC UA client configuration is invalid or requests a capability not implemented by this milestone.",
            SuggestedAction:
            "Correct the endpoint/buffer settings or select SecurityPolicy#None until secure policies are enabled.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
