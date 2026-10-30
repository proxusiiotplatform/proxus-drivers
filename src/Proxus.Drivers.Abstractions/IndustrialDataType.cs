namespace Proxus.Drivers;

/// <summary>
///     Protocol-neutral semantic value kinds. Existing ordinal values are append-only so
///     persisted configurations can safely carry the enum across library upgrades.
///     Protocol spellings such as IEC INT, Siemens WORD, Allen-Bradley SINT, or OPC UA
///     UInt16 belong to protocol-specific type compilers and metadata, not this enum.
/// </summary>
public enum IndustrialDataType : byte
{
    Bool = 0,
    Int8 = 1,
    UInt8 = 2,
    Int16 = 3,
    UInt16 = 4,
    Int32 = 5,
    UInt32 = 6,
    Int64 = 7,
    UInt64 = 8,
    Float32 = 9,
    Float64 = 10,
    String = 11,
    Bytes = 12,
    DateTime = 13,
    Duration = 14,

    // Foundation v3 additions. Never insert members above this line.
    Int128 = 15,
    UInt128 = 16,
    Float16 = 17,
    Decimal = 18,
    Char8 = 19,
    Char16 = 20,
    Guid = 21,
    Date = 22,
    TimeOfDay = 23,
    BitString8 = 24,
    BitString16 = 25,
    BitString32 = 26,
    BitString64 = 27,

    // User-defined symbolic enumerations. Wire width/signedness is supplied by each protocol descriptor.
    Enumeration = 28,

    // OPC UA semantic values. These preserve meaning instead of flattening structured UA values to strings.
    StatusCode = 29,
    NodeId = 30,
    ExpandedNodeId = 31,
    QualifiedName = 32,
    LocalizedText = 33,

    // Dynamic industrial values. Append-only for persisted configuration compatibility.
    Null = 34,
    Array = 35,
    Record = 36,
    Variant = 37,
    ExtensionObject = 38,

    // OPC UA built-in XmlElement. Kept distinct from String to preserve wire semantics.
    XmlElement = 39
}
