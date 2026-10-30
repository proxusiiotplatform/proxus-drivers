namespace Proxus.Drivers;

public enum IndustrialNodeIdKind : byte
{
    Numeric = 0,
    String = 1,
    Guid = 2,
    Opaque = 3
}

/// <summary>Protocol-neutral immutable representation of an OPC UA NodeId.</summary>
public sealed class IndustrialNodeId : IEquatable<IndustrialNodeId>
{
    private readonly byte[]? _opaque;

    private IndustrialNodeId(ushort namespaceIndex, IndustrialNodeIdKind kind, ulong numeric, string? text, Guid guid,
        byte[]? opaque)
    {
        NamespaceIndex = namespaceIndex;
        Kind = kind;
        NumericIdentifier = numeric;
        StringIdentifier = text;
        GuidIdentifier = guid;
        _opaque = opaque;
    }

    public ushort NamespaceIndex { get; }
    public IndustrialNodeIdKind Kind { get; }
    public ulong NumericIdentifier { get; }
    public string? StringIdentifier { get; }
    public Guid GuidIdentifier { get; }
    public ReadOnlyMemory<byte> OpaqueIdentifier => _opaque ?? ReadOnlyMemory<byte>.Empty;

    public bool Equals(IndustrialNodeId? other)
    {
        if (other is null || NamespaceIndex != other.NamespaceIndex || Kind != other.Kind) return false;
        return Kind switch
        {
            IndustrialNodeIdKind.Numeric => NumericIdentifier == other.NumericIdentifier,
            IndustrialNodeIdKind.String => string.Equals(StringIdentifier, other.StringIdentifier,
                StringComparison.Ordinal),
            IndustrialNodeIdKind.Guid => GuidIdentifier == other.GuidIdentifier,
            IndustrialNodeIdKind.Opaque => OpaqueIdentifier.Span.SequenceEqual(other.OpaqueIdentifier.Span),
            _ => false
        };
    }

    public static IndustrialNodeId Numeric(ushort namespaceIndex, ulong identifier)
    {
        return new IndustrialNodeId(namespaceIndex, IndustrialNodeIdKind.Numeric, identifier, null, default, null);
    }

    public static IndustrialNodeId String(ushort namespaceIndex, string identifier)
    {
        return new IndustrialNodeId(namespaceIndex, IndustrialNodeIdKind.String, 0,
            identifier ?? throw new ArgumentNullException(nameof(identifier)), default, null);
    }

    public static IndustrialNodeId Guid(ushort namespaceIndex, Guid identifier)
    {
        return new IndustrialNodeId(namespaceIndex, IndustrialNodeIdKind.Guid, 0, null, identifier, null);
    }

    public static IndustrialNodeId Opaque(ushort namespaceIndex, ReadOnlySpan<byte> identifier)
    {
        return new IndustrialNodeId(namespaceIndex, IndustrialNodeIdKind.Opaque, 0, null, default,
            [.. identifier]);
    }

    public override bool Equals(object? obj)
    {
        return obj is IndustrialNodeId other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(NamespaceIndex);
        hash.Add(Kind);
        switch (Kind)
        {
            case IndustrialNodeIdKind.Numeric: hash.Add(NumericIdentifier); break;
            case IndustrialNodeIdKind.String: hash.Add(StringIdentifier, StringComparer.Ordinal); break;
            case IndustrialNodeIdKind.Guid: hash.Add(GuidIdentifier); break;
            case IndustrialNodeIdKind.Opaque:
                foreach (var b in OpaqueIdentifier.Span) hash.Add(b);
                break;
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return Kind switch
        {
            IndustrialNodeIdKind.Numeric => $"ns={NamespaceIndex};i={NumericIdentifier}",
            IndustrialNodeIdKind.String => $"ns={NamespaceIndex};s={StringIdentifier}",
            IndustrialNodeIdKind.Guid => $"ns={NamespaceIndex};g={GuidIdentifier:D}",
            IndustrialNodeIdKind.Opaque => $"ns={NamespaceIndex};b={Convert.ToBase64String(OpaqueIdentifier.Span)}",
            _ => string.Empty
        };
    }
}

public sealed record IndustrialExpandedNodeId(
    IndustrialNodeId NodeId,
    string? NamespaceUri = null,
    uint ServerIndex = 0)
{
    public IndustrialNodeId NodeId { get; init; } = NodeId ?? throw new ArgumentNullException(nameof(NodeId));
}

public readonly record struct IndustrialQualifiedName(ushort NamespaceIndex, string Name)
{
    public string Name { get; init; } = Name ?? throw new ArgumentNullException(nameof(Name));

    public override string ToString()
    {
        return NamespaceIndex == 0 ? Name : $"{NamespaceIndex}:{Name}";
    }
}

public readonly record struct IndustrialLocalizedText(string? Locale, string Text)
{
    public string Text { get; init; } = Text ?? throw new ArgumentNullException(nameof(Text));

    public override string ToString()
    {
        return string.IsNullOrEmpty(Locale) ? Text : $"[{Locale}] {Text}";
    }
}
