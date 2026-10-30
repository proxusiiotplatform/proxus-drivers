namespace Proxus.Drivers.OpcUa;

public readonly record struct OpcUaSessionInfo(
    IndustrialNodeId SessionId,
    IndustrialNodeId AuthenticationToken,
    double RevisedSessionTimeoutMilliseconds,
    uint MaxRequestMessageSize,
    string AnonymousPolicyId,
    bool IsActivated,
    OpcUaUserTokenPolicy[]? UserIdentityTokens = null);

// ReSharper disable once UnusedType.Global -- public service-model API consumed by OpcUaTcpClient and external callers.
public readonly record struct OpcUaWriteItem(OpcUaNodeDescriptor Descriptor, DriverSample Sample)
{
    public static OpcUaWriteItem Value(OpcUaNodeDescriptor descriptor, DriverValue value)
    {
        return new OpcUaWriteItem(descriptor,
            new DriverSample(value, DriverQuality.Good, DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc),
                DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc)));
    }
}

// ReSharper disable once UnusedType.Global -- response model used by the endpoint-selection decoder.
internal readonly record struct OpcUaEndpointSelection(
    string? AnonymousPolicyId,
    OpcUaUserTokenPolicy[] UserIdentityTokens);

// ReSharper disable once UnusedType.Global -- response model used by the session-signature decoder.
internal readonly record struct OpcUaSignatureData(string? Algorithm, byte[]? Signature);
