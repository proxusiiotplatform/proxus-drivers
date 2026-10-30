namespace Proxus.Drivers.OpcUa;

public enum OpcUaBrowseDirection : uint
{
    Forward = 0,
    Inverse = 1,
    Both = 2
}

[Flags]
public enum OpcUaNodeClass : uint
{
    Unspecified = 0,
    Object = 1,
    Variable = 2,
    Method = 4,
    ObjectType = 8,
    VariableType = 16,
    ReferenceType = 32,
    DataType = 64,
    View = 128,
    All = Object | Variable | Method | ObjectType | VariableType | ReferenceType | DataType | View
}

[Flags]
public enum OpcUaBrowseResultMask : uint
{
    None = 0,
    ReferenceType = 1 << 0,
    IsForward = 1 << 1,
    NodeClass = 1 << 2,
    BrowseName = 1 << 3,
    DisplayName = 1 << 4,
    TypeDefinition = 1 << 5,
    All = ReferenceType | IsForward | NodeClass | BrowseName | DisplayName | TypeDefinition
}

public readonly record struct OpcUaBrowseDescription(
    IndustrialNodeId NodeId,
    OpcUaBrowseDirection Direction = OpcUaBrowseDirection.Forward,
    IndustrialNodeId? ReferenceTypeId = null,
    bool IncludeSubtypes = true,
    OpcUaNodeClass NodeClassMask = OpcUaNodeClass.Unspecified,
    OpcUaBrowseResultMask ResultMask = OpcUaBrowseResultMask.All)
{
    public IndustrialNodeId NodeId { get; init; } = NodeId ?? throw new ArgumentNullException(nameof(NodeId));
}

public readonly record struct OpcUaReferenceDescription(
    IndustrialNodeId ReferenceTypeId,
    bool IsForward,
    IndustrialExpandedNodeId NodeId,
    IndustrialQualifiedName BrowseName,
    IndustrialLocalizedText DisplayName,
    OpcUaNodeClass NodeClass,
    IndustrialExpandedNodeId TypeDefinition);

public sealed class OpcUaBrowseResult
{
    public OpcUaBrowseResult(uint statusCode, byte[]? continuationPoint, OpcUaReferenceDescription[] references)
    {
        StatusCode = statusCode;
        ContinuationPoint = continuationPoint is null ? null : (byte[])continuationPoint.Clone();
        References = references ?? throw new ArgumentNullException(nameof(references));
    }

    public uint StatusCode { get; }
    public DriverQuality Quality => OpcUaBinaryCodec.MapQuality(StatusCode);
    public byte[]? ContinuationPoint { get; }
    public IReadOnlyList<OpcUaReferenceDescription> References { get; }
    public bool HasContinuationPoint => ContinuationPoint is { Length: > 0 };
}

public enum OpcUaStructureType
{
    Structure = 0,
    StructureWithOptionalFields = 1,
    Union = 2,
    StructureWithSubtypedValues = 3,
    UnionWithSubtypedValues = 4
}

public readonly record struct OpcUaStructureFieldDefinition(
    string Name,
    IndustrialLocalizedText Description,
    IndustrialNodeId DataType,
    int ValueRank,
    uint[] ArrayDimensions,
    uint MaxStringLength,
    bool IsOptional);

public readonly record struct OpcUaEnumFieldDefinition(
    long Value,
    IndustrialLocalizedText DisplayName,
    IndustrialLocalizedText Description,
    string Name);

public abstract record OpcUaDataTypeDefinition;

public sealed record OpcUaStructureDataTypeDefinition(
    IndustrialNodeId DefaultEncodingId,
    IndustrialNodeId BaseDataType,
    OpcUaStructureType StructureType,
    OpcUaStructureFieldDefinition[] Fields) : OpcUaDataTypeDefinition;

public sealed record OpcUaEnumDataTypeDefinition(
    OpcUaEnumFieldDefinition[] Fields) : OpcUaDataTypeDefinition;

public readonly record struct OpcUaDiscoveredDataType(
    IndustrialNodeId NodeId,
    IndustrialQualifiedName BrowseName,
    OpcUaDataTypeDefinition Definition);
