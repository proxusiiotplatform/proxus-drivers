namespace Proxus.Drivers.OpcUa;

/// <summary>Compiled OPC UA attribute address. NodeId parsing is kept out of the polling hot path.</summary>
public readonly record struct OpcUaNodeDescriptor(
    IndustrialNodeId NodeId,
    OpcUaAttributeId AttributeId = OpcUaAttributeId.Value,
    string? IndexRange = null,
    IndustrialQualifiedName DataEncoding = default)
{
    public IndustrialNodeId NodeId { get; init; } = NodeId ?? throw new ArgumentNullException(nameof(NodeId));

    public override string ToString()
    {
        var suffix = AttributeId == OpcUaAttributeId.Value ? string.Empty : $"@{AttributeId}";
        if (!string.IsNullOrEmpty(IndexRange)) suffix += $"[{IndexRange}]";
        return NodeId + suffix;
    }
}
