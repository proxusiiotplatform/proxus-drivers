namespace Proxus.Drivers;

public enum DriverByteOrder : byte
{
    BigEndian = 0,
    LittleEndian = 1
}

public readonly record struct DriverPackedField(
    string Name,
    IndustrialDataType DataType,
    int ValueOffset,
    int ValueCount,
    int ByteOffset,
    int ByteLength,
    DriverByteOrder ByteOrder,
    DriverArrayShape? Shape = null);

/// <summary>
///     Explicit byte layout for packed UDTs, telegram payloads and vendor-defined register blocks.
///     Padding/alignment is fixed at configuration time and can be projected to protocol descriptors.
/// </summary>
public sealed class DriverPackedLayout
{
    private readonly DriverPackedField[] _fields;

    internal DriverPackedLayout(DriverPackedField[] fields, int valueCount, int byteLength, int paddingByteCount)
    {
        _fields = fields;
        ValueCount = valueCount;
        ByteLength = byteLength;
        PaddingByteCount = paddingByteCount;
    }

    public ReadOnlyMemory<DriverPackedField> Fields => _fields;
    public int ValueCount { get; }
    public int ByteLength { get; }
    public int PaddingByteCount { get; }

    public DriverResult<DriverRecordSchema<TDescriptor>> Project<TDescriptor>(
        Func<DriverPackedField, TDescriptor> descriptorMapper)
        where TDescriptor : struct
    {
        ArgumentNullException.ThrowIfNull(descriptorMapper);
        var fields = new DriverRecordField<TDescriptor>[_fields.Length];
        for (var i = 0; i < _fields.Length; i++)
            fields[i] = new DriverRecordField<TDescriptor>(_fields[i].Name, _fields[i].ValueOffset,
                _fields[i].ValueCount, descriptorMapper(_fields[i]));
        return DriverRecordSchema<TDescriptor>.Create(fields);
    }
}

public sealed class DriverPackedLayoutBuilder
{
    private readonly List<DriverPackedField> _fields = [];
    private DriverError? _error;
    private int _padding;

    public int CurrentByteOffset { get; private set; }

    public int CurrentValueOffset { get; private set; }

    public DriverPackedLayoutBuilder Pad(int byteCount)
    {
        if (_error is not null) return this;
        if (byteCount < 0) return Fail("LAYOUT_PADDING", "Padding cannot be negative.");
        try
        {
            CurrentByteOffset = checked(CurrentByteOffset + byteCount);
            _padding = checked(_padding + byteCount);
        }
        catch (OverflowException)
        {
            return Fail("LAYOUT_OVERFLOW", "Packed layout byte size exceeds Int32 capacity.");
        }

        return this;
    }

    public DriverPackedLayoutBuilder Align(int alignment)
    {
        if (_error is not null) return this;
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            return Fail("LAYOUT_ALIGNMENT", "Alignment must be a positive power of two.");
        var aligned = (CurrentByteOffset + alignment - 1) & ~(alignment - 1);
        return Pad(aligned - CurrentByteOffset);
    }

    public DriverPackedLayoutBuilder AddField(
        string name,
        IndustrialDataType dataType,
        DriverByteOrder byteOrder = DriverByteOrder.BigEndian,
        int elementCount = 1)
    {
        if (_error is not null) return this;
        if (elementCount <= 0)
            return Fail("LAYOUT_ELEMENT_COUNT", $"Field '{name}' must contain at least one element.");
        var width = IndustrialDataTypeInfo.GetFixedWidthBytes(dataType);
        if (width <= 0)
            return Fail("LAYOUT_VARIABLE_WIDTH",
                $"Field '{name}' uses {dataType}, which requires an explicit protocol/string layout and cannot use the fixed packed-layout codec.");
        return AddCore(name, dataType, elementCount, checked(width * elementCount), byteOrder, null);
    }

    public DriverPackedLayoutBuilder AddArrayField(
        string name,
        IndustrialDataType dataType,
        DriverArrayShape shape,
        DriverByteOrder byteOrder = DriverByteOrder.BigEndian)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (_error is not null) return this;
        var width = IndustrialDataTypeInfo.GetFixedWidthBytes(dataType);
        if (width <= 0)
            return Fail("LAYOUT_VARIABLE_WIDTH",
                $"Array field '{name}' uses {dataType}, which is not fixed-width in the canonical packed-layout codec.");
        return AddCore(name, dataType, shape.ElementCount, checked(width * shape.ElementCount), byteOrder, shape);
    }

    public DriverResult<DriverPackedLayout> Build()
    {
        if (_error is { } error) return DriverResult<DriverPackedLayout>.Failure(error);
        if (_fields.Count == 0)
            return DriverResult<DriverPackedLayout>.Failure(LayoutError("LAYOUT_EMPTY",
                "Packed layout must contain at least one field."));
        return DriverResult<DriverPackedLayout>.Success(new DriverPackedLayout([.. _fields], CurrentValueOffset,
            CurrentByteOffset, _padding));
    }

    private DriverPackedLayoutBuilder AddCore(string name, IndustrialDataType type, int valueCount, int byteLength,
        DriverByteOrder order, DriverArrayShape? shape)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Fail("LAYOUT_FIELD_NAME", "Packed layout field name cannot be empty.");
        for (var i = 0; i < _fields.Count; i++)
            if (string.Equals(_fields[i].Name, name, StringComparison.Ordinal))
                return Fail("LAYOUT_FIELD_DUPLICATE", $"Packed layout field '{name}' is duplicated.");
        try
        {
            _fields.Add(new DriverPackedField(name, type, CurrentValueOffset, valueCount, CurrentByteOffset, byteLength,
                order, shape));
            CurrentValueOffset = checked(CurrentValueOffset + valueCount);
            CurrentByteOffset = checked(CurrentByteOffset + byteLength);
        }
        catch (OverflowException)
        {
            return Fail("LAYOUT_OVERFLOW", "Packed layout exceeds Int32 capacity.");
        }

        return this;
    }

    private DriverPackedLayoutBuilder Fail(string code, string message)
    {
        _error = LayoutError(code, message);
        return this;
    }

    private static DriverError LayoutError(string code, string message)
    {
        return new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Cause: "The packed memory layout cannot be compiled deterministically.",
            SuggestedAction: "Fix field widths, padding or alignment before device I/O starts.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}
