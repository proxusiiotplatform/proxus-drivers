namespace Proxus.Drivers.OpcUa;

public enum OpcUaApplicationType : uint
{
    Server = 0,
    Client = 1,
    ClientAndServer = 2,
    DiscoveryServer = 3
}

public enum OpcUaUserTokenType : uint
{
    Anonymous = 0,
    UserName = 1,
    Certificate = 2,
    IssuedToken = 3
}

public sealed record OpcUaApplicationDescription(
    string? ApplicationUri,
    string? ProductUri,
    IndustrialLocalizedText ApplicationName,
    OpcUaApplicationType ApplicationType,
    string? GatewayServerUri,
    string? DiscoveryProfileUri,
    string[] DiscoveryUrls);

public sealed record OpcUaUserTokenPolicy(
    string PolicyId,
    OpcUaUserTokenType TokenType,
    string? IssuedTokenType,
    string? IssuerEndpointUrl,
    string? SecurityPolicyUri);

public sealed record OpcUaEndpointDescription(
    string EndpointUrl,
    OpcUaApplicationDescription Server,
    byte[]? ServerCertificate,
    OpcUaMessageSecurityMode SecurityMode,
    string SecurityPolicyUri,
    OpcUaUserTokenPolicy[] UserIdentityTokens,
    string? TransportProfileUri,
    byte SecurityLevel)
{
    public bool Supports(OpcUaMessageSecurityMode mode, string securityPolicyUri)
    {
        return SecurityMode == mode && string.Equals(SecurityPolicyUri, securityPolicyUri, StringComparison.Ordinal);
    }
}

public static class OpcUaEndpointSelector
{
    public static DriverResult<OpcUaEndpointDescription> Select(
        IReadOnlyList<OpcUaEndpointDescription> endpoints,
        OpcUaMessageSecurityMode mode,
        string securityPolicyUri,
        OpcUaUserTokenType userTokenType = OpcUaUserTokenType.Anonymous)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        var candidates = endpoints
            .Where(endpoint => endpoint.Supports(mode, securityPolicyUri) &&
                               endpoint.UserIdentityTokens.Any(token => token.TokenType == userTokenType))
            .OrderByDescending(static endpoint => endpoint.SecurityLevel)
            .ToArray();
        if (candidates.Length != 0) return DriverResult<OpcUaEndpointDescription>.Success(candidates[0]);
        return DriverResult<OpcUaEndpointDescription>.Failure(new DriverError(
            DriverErrorKind.NotSupported,
            "UA_ENDPOINT_NOT_FOUND",
            $"No endpoint advertises MessageSecurityMode={mode}, SecurityPolicyUri='{securityPolicyUri}' and user token type {userTokenType}.",
            Operation: DriverOperation.Negotiate,
            Protocol: "OPC UA Binary/TCP",
            Cause: "The discovered endpoint set does not contain the requested security and identity combination.",
            SuggestedAction:
            "Select one of the returned endpoint descriptions or enable the required endpoint on the server.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
