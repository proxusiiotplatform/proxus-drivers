namespace Proxus.Drivers.OpcUa;

/// <summary>
///     One method call for the Call service (OPC 10000-4 §5.11.2): the Object or ObjectType that owns the
///     method, the Method node, and the input arguments in declaration order. Each argument must carry the
///     exact DataType the method declares; servers report a mismatch per argument (Bad_TypeMismatch).
/// </summary>
public sealed class OpcUaMethodCall
{
    public OpcUaMethodCall(IndustrialNodeId objectId, IndustrialNodeId methodId, params DriverValue[] inputArguments)
    {
        ObjectId = objectId ?? throw new ArgumentNullException(nameof(objectId));
        MethodId = methodId ?? throw new ArgumentNullException(nameof(methodId));
        ArgumentNullException.ThrowIfNull(inputArguments);
        InputArguments = [.. inputArguments];
    }

    public IndustrialNodeId ObjectId { get; }
    public IndustrialNodeId MethodId { get; }
    public IReadOnlyList<DriverValue> InputArguments { get; }
}

/// <summary>
///     Per-call result. A Bad <see cref="StatusCode" /> (for example Bad_MethodInvalid or
///     Bad_InvalidArgument) fails only this call; <see cref="InputArgumentResults" /> then names the
///     offending arguments when the server reports them.
/// </summary>
public sealed class OpcUaMethodResult
{
    public OpcUaMethodResult(uint statusCode, uint[] inputArgumentResults, DriverValue[] outputArguments)
    {
        StatusCode = statusCode;
        InputArgumentResults = inputArgumentResults ?? throw new ArgumentNullException(nameof(inputArgumentResults));
        OutputArguments = outputArguments ?? throw new ArgumentNullException(nameof(outputArguments));
    }

    public uint StatusCode { get; }
    public DriverQuality Quality => OpcUaBinaryCodec.MapQuality(StatusCode);
    public bool IsGood => (StatusCode & 0xC0000000u) == 0;
    public IReadOnlyList<uint> InputArgumentResults { get; }
    public IReadOnlyList<DriverValue> OutputArguments { get; }
}
