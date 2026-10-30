namespace Proxus.Drivers;

/// <summary>Runtime-sized homogeneous array with explicit rank/dimensions.</summary>
public sealed class DriverDynamicArray : IEquatable<DriverDynamicArray>
{
    private readonly DriverValue[] _values;

    private DriverDynamicArray(IndustrialDataType elementType, DriverArrayShape shape, DriverValue[] values)
    {
        ElementType = elementType;
        Shape = shape;
        _values = values;
    }

    public IndustrialDataType ElementType { get; }
    public DriverArrayShape Shape { get; }
    public ReadOnlyMemory<DriverValue> Values => _values;
    public int Count => _values.Length;

    public bool Equals(DriverDynamicArray? other)
    {
        if (other is null || ElementType != other.ElementType || Shape.Rank != other.Shape.Rank ||
            Shape.ElementCount != other.Shape.ElementCount) return false;
        if (!Shape.Dimensions.Span.SequenceEqual(other.Shape.Dimensions.Span)) return false;
        return _values.AsSpan().SequenceEqual(other._values);
    }

    public static DriverResult<DriverDynamicArray> Create(
        IndustrialDataType elementType,
        ReadOnlySpan<int> dimensions,
        ReadOnlySpan<DriverValue> values)
    {
        var shapeResult = DriverArrayShape.CreateRuntime(dimensions);
        if (!shapeResult.IsSuccess)
            return DriverResult<DriverDynamicArray>.Failure(shapeResult.Error);
        var shape = shapeResult.Value;
        if (shape.ElementCount != values.Length)
            return Failure("DYNAMIC_ARRAY_COUNT",
                $"Array shape contains {shape.ElementCount} elements but {values.Length} values were supplied.");
        for (var i = 0; i < values.Length; i++)
            if (values[i].Type != elementType)
                return Failure("DYNAMIC_ARRAY_TYPE", $"Element {i} is {values[i].Type}, expected {elementType}.");
        return DriverResult<DriverDynamicArray>.Success(new DriverDynamicArray(elementType, shape, [.. values]));
    }

    public override bool Equals(object? obj)
    {
        return obj is DriverDynamicArray other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ElementType);
        hash.Add(Shape.Rank);
        hash.Add(Shape.ElementCount);
        foreach (var dimension in Shape.Dimensions.Span) hash.Add(dimension);
        for (var i = 0; i < _values.Length; i++) hash.Add(_values[i]);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        return $"{ElementType}[{string.Join(',', Shape.Dimensions.ToArray())}] ({Count})";
    }

    private static DriverResult<DriverDynamicArray> Failure(string code, string message)
    {
        return DriverResult<DriverDynamicArray>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Cause: "Runtime array metadata and supplied values are inconsistent.",
            SuggestedAction:
            "Provide positive runtime dimensions whose product matches the number and canonical type of supplied values.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}

public readonly record struct DriverRecordFieldValue(string Name, bool IsPresent, DriverValue Value)
{
    public string Name { get; init; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("Field name cannot be empty.", nameof(Name))
        : Name;

    public static DriverRecordFieldValue Present(string name, DriverValue value)
    {
        return new DriverRecordFieldValue(name, true, value);
    }

    public static DriverRecordFieldValue Absent(string name)
    {
        return new DriverRecordFieldValue(name, false, DriverValue.Null);
    }
}

/// <summary>Ordered runtime record preserving optional-field presence independently from Null.</summary>
public sealed class DriverRecordValue : IEquatable<DriverRecordValue>
{
    private readonly DriverRecordFieldValue[] _fields;

    private DriverRecordValue(string typeName, DriverRecordFieldValue[] fields)
    {
        TypeName = typeName;
        _fields = fields;
    }

    public string TypeName { get; }
    public ReadOnlyMemory<DriverRecordFieldValue> Fields => _fields;

    public bool Equals(DriverRecordValue? other)
    {
        return other is not null && string.Equals(TypeName, other.TypeName, StringComparison.Ordinal) &&
               _fields.AsSpan().SequenceEqual(other._fields);
    }

    public static DriverResult<DriverRecordValue> Create(string typeName, ReadOnlySpan<DriverRecordFieldValue> fields)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return Failure("RECORD_TYPE_EMPTY", "Record type name cannot be empty.");
        if (fields.IsEmpty) return Failure("RECORD_FIELDS_EMPTY", "Runtime record must contain at least one field.");
        var copy = fields.ToArray();
        for (var i = 0; i < copy.Length; i++)
        for (var j = 0; j < i; j++)
            if (string.Equals(copy[i].Name, copy[j].Name, StringComparison.Ordinal))
                return Failure("RECORD_FIELD_DUPLICATE", $"Runtime record contains duplicate field '{copy[i].Name}'.");
        return DriverResult<DriverRecordValue>.Success(new DriverRecordValue(typeName.Trim(), copy));
    }

    public bool TryGetField(string name, out DriverRecordFieldValue field)
    {
        for (var i = 0; i < _fields.Length; i++)
            if (string.Equals(_fields[i].Name, name, StringComparison.Ordinal))
            {
                field = _fields[i];
                return true;
            }

        field = default;
        return false;
    }

    public override bool Equals(object? obj)
    {
        return obj is DriverRecordValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(TypeName, StringComparer.Ordinal);
        for (var i = 0; i < _fields.Length; i++) h.Add(_fields[i]);
        return h.ToHashCode();
    }

    public override string ToString()
    {
        return $"{TypeName}{{{_fields.Length} fields}}";
    }

    private static DriverResult<DriverRecordValue> Failure(string code, string message)
    {
        return DriverResult<DriverRecordValue>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message, Operation: DriverOperation.Validate,
            Cause: "Runtime structured value is not internally consistent.",
            SuggestedAction: "Provide a stable type name and unique ordered fields.",
            Retry: DriverRetryDisposition.FixConfiguration, Connection: DriverConnectionDisposition.Unchanged));
    }
}

/// <summary>Explicit Variant wrapper preserving that a value came from a dynamic variant container.</summary>
// ReSharper disable once UnusedType.Global -- public canonical variant model intended for driver consumers.
public sealed class DriverVariantValue : IEquatable<DriverVariantValue>
{
    public DriverVariantValue(DriverValue value)
    {
        if (value.Type == IndustrialDataType.Variant)
            throw new ArgumentException("Nested Variant wrappers are not allowed.", nameof(value));
        Value = value;
    }

    public DriverValue Value { get; }

    public bool Equals(DriverVariantValue? other)
    {
        return other is not null && Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        return obj is DriverVariantValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override string ToString()
    {
        return $"Variant<{Value.Type}>({Value})";
    }
}

public enum DriverExtensionObjectEncoding : byte
{
    None = 0,
    Binary = 1,
    Xml = 2,
    Json = 3
}

/// <summary>Lossless ExtensionObject envelope: original encoded body plus optional canonical decoded value.</summary>
public sealed class DriverExtensionObject : IEquatable<DriverExtensionObject>
{
    private readonly byte[] _body;

    public DriverExtensionObject(
        IndustrialExpandedNodeId typeId,
        DriverExtensionObjectEncoding encoding,
        ReadOnlySpan<byte> encodedBody,
        DriverValue? decodedValue = null)
    {
        TypeId = typeId ?? throw new ArgumentNullException(nameof(typeId));
        Encoding = encoding;
        if (encoding == DriverExtensionObjectEncoding.None && !encodedBody.IsEmpty)
            throw new ArgumentException("Encoding None requires an empty encoded body.", nameof(encodedBody));
        _body = [.. encodedBody];
        DecodedValue = decodedValue;
    }

    public IndustrialExpandedNodeId TypeId { get; }
    public DriverExtensionObjectEncoding Encoding { get; }
    public ReadOnlyMemory<byte> EncodedBody => _body;
    public DriverValue? DecodedValue { get; }
    public bool HasDecodedValue => DecodedValue.HasValue;

    public bool Equals(DriverExtensionObject? other)
    {
        return other is not null && Equals(TypeId, other.TypeId) && Encoding == other.Encoding &&
               _body.AsSpan().SequenceEqual(other._body) && Nullable.Equals(DecodedValue, other.DecodedValue);
    }

    public override bool Equals(object? obj)
    {
        return obj is DriverExtensionObject other && Equals(other);
    }

    public override int GetHashCode()
    {
        var h = new HashCode();
        h.Add(TypeId);
        h.Add(Encoding);
        for (var i = 0; i < _body.Length; i++) h.Add(_body[i]);
        h.Add(DecodedValue);
        return h.ToHashCode();
    }

    public override string ToString()
    {
        return $"ExtensionObject<{TypeId}> {Encoding} {_body.Length} bytes";
    }
}
