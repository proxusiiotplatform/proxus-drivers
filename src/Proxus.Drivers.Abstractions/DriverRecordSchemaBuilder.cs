using System.Runtime.InteropServices;

namespace Proxus.Drivers;

/// <summary>
///     Configuration-time, reflection-free builder for nested industrial STRUCT/UDT layouts.
///     Nested records are flattened into leaf descriptors once; runtime execution stays on the
///     existing heterogeneous batch/compiled-plan path.
/// </summary>
public sealed class DriverRecordSchemaBuilder<TDescriptor>
    where TDescriptor : struct
{
    private readonly List<DriverRecordArrayMetadata> _arrays = [];
    private readonly List<DriverRecordField<TDescriptor>> _fields = [];
    private DriverError? _error;

    /// <summary>Number of logical DriverValue slots currently assigned by the builder.</summary>
    public int ValueCount { get; private set; }

    public DriverRecordSchemaBuilder<TDescriptor> AddField(
        string name,
        TDescriptor descriptor,
        int valueCount = 1)
    {
        if (_error is not null) return this;
        if (string.IsNullOrWhiteSpace(name))
            return Fail("RECORD_BUILDER_NAME", "Field name cannot be empty.");
        if (valueCount <= 0)
            return Fail("RECORD_BUILDER_COUNT", $"Field '{name}' must consume at least one value.");

        try
        {
            _fields.Add(new DriverRecordField<TDescriptor>(name, ValueCount, valueCount, descriptor));
            ValueCount = checked(ValueCount + valueCount);
        }
        catch (OverflowException)
        {
            return Fail("RECORD_BUILDER_OVERFLOW", "The record value-slot count exceeds Int32 capacity.");
        }

        return this;
    }


    public DriverRecordSchemaBuilder<TDescriptor> AddArrayField(
        string name,
        TDescriptor descriptor,
        DriverArrayShape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (_error is not null) return this;
        var start = ValueCount;
        AddField(name, descriptor, shape.ElementCount);
        if (_error is null) _arrays.Add(new DriverRecordArrayMetadata(name, start, 1, shape));
        return this;
    }

    /// <summary>
    ///     Appends a nested record. descriptorMapper is configuration-time only and can rebase
    ///     relative child descriptors onto a parent device address without reflection.
    /// </summary>
    public DriverRecordSchemaBuilder<TDescriptor> AddRecord(
        string name,
        DriverRecordSchema<TDescriptor> schema,
        Func<TDescriptor, TDescriptor>? descriptorMapper = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (_error is not null) return this;
        if (string.IsNullOrWhiteSpace(name))
            return Fail("RECORD_BUILDER_NAME", "Nested record name cannot be empty.");

        var baseOffset = ValueCount;
        foreach (var child in schema.Fields.Span)
        {
            var descriptor = descriptorMapper is null ? child.Descriptor : descriptorMapper(child.Descriptor);
            _fields.Add(new DriverRecordField<TDescriptor>(
                $"{name}.{child.Name}",
                checked(baseOffset + child.ValueOffset),
                child.ValueCount,
                descriptor));
        }

        ValueCount = checked(baseOffset + schema.ValueCount);
        return this;
    }

    /// <summary>
    ///     Appends an array of nested records. The mapper is intentionally mandatory: every
    ///     element must explicitly project child descriptors to its own physical PLC region.
    /// </summary>
    public DriverRecordSchemaBuilder<TDescriptor> AddRecordArray(
        string name,
        int count,
        DriverRecordSchema<TDescriptor> elementSchema,
        Func<int, TDescriptor, TDescriptor> descriptorMapper)
    {
        var shapeResult = DriverArrayShape.Create([count]);
        if (!shapeResult.IsSuccess)
        {
            _error = shapeResult.Error;
            return this;
        }

        return AddRecordArray(name, shapeResult.Value, elementSchema, descriptorMapper);
    }

    public DriverRecordSchemaBuilder<TDescriptor> AddRecordArray(
        string name,
        DriverArrayShape shape,
        DriverRecordSchema<TDescriptor> elementSchema,
        Func<int, TDescriptor, TDescriptor> descriptorMapper)
    {
        ArgumentNullException.ThrowIfNull(elementSchema);
        ArgumentNullException.ThrowIfNull(descriptorMapper);
        if (_error is not null) return this;
        if (string.IsNullOrWhiteSpace(name))
            return Fail("RECORD_BUILDER_NAME", "Record-array name cannot be empty.");
        ArgumentNullException.ThrowIfNull(shape);

        var baseValueOffset = ValueCount;
        try
        {
            for (var index = 0; index < shape.ElementCount; index++)
            {
                var elementBase = checked(ValueCount + index * elementSchema.ValueCount);
                foreach (var child in elementSchema.Fields.Span)
                    _fields.Add(new DriverRecordField<TDescriptor>(
                        $"{name}{shape.FormatIndices(index)}.{child.Name}",
                        checked(elementBase + child.ValueOffset),
                        child.ValueCount,
                        descriptorMapper(index, child.Descriptor)));
            }

            ValueCount = checked(ValueCount + shape.ElementCount * elementSchema.ValueCount);
            _arrays.Add(new DriverRecordArrayMetadata(name, baseValueOffset, elementSchema.ValueCount, shape));
        }
        catch (OverflowException)
        {
            return Fail("RECORD_BUILDER_OVERFLOW", "The record-array value-slot count exceeds Int32 capacity.");
        }

        return this;
    }

    public DriverResult<DriverRecordSchema<TDescriptor>> Build()
    {
        if (_error is { } error)
            return DriverResult<DriverRecordSchema<TDescriptor>>.Failure(error);
        return DriverRecordSchema<TDescriptor>.Create(CollectionsMarshal.AsSpan(_fields),
            CollectionsMarshal.AsSpan(_arrays));
    }

    private DriverRecordSchemaBuilder<TDescriptor> Fail(string code, string message)
    {
        _error = new DriverError(
            DriverErrorKind.Configuration,
            code,
            message,
            Operation: DriverOperation.Validate,
            Cause: "A nested STRUCT/UDT layout cannot be compiled safely.",
            SuggestedAction: "Correct the field shape or descriptor projection before device I/O starts.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
        return this;
    }
}
