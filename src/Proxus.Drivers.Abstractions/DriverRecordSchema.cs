namespace Proxus.Drivers;

/// <summary>
///     One named field inside a schema-driven industrial STRUCT/UDT. ValueOffset addresses
///     the first slot in the caller's flat DriverValue buffer; ValueCount is the number of
///     logical values consumed by the descriptor (1 for a scalar, N for an array).
/// </summary>
public readonly record struct DriverRecordField<TDescriptor>(
    string Name,
    int ValueOffset,
    int ValueCount,
    TDescriptor Descriptor)
    where TDescriptor : struct;

/// <summary>Shape metadata for a scalar array or record array inside a flattened record schema.</summary>
public readonly record struct DriverRecordArrayMetadata(
    string Name,
    int ValueOffset,
    int ElementValueCount,
    DriverArrayShape Shape)
{
    public int ValueCount => checked(ElementValueCount * Shape.ElementCount);

    public bool TryGetElementValueOffset(ReadOnlySpan<int> indices, out int valueOffset)
    {
        valueOffset = 0;
        if (!Shape.TryGetLinearIndex(indices, out var linearIndex)) return false;
        valueOffset = checked(ValueOffset + linearIndex * ElementValueCount);
        return true;
    }
}

/// <summary>
///     AOT-safe protocol-neutral STRUCT/UDT schema. The driver layer deliberately exposes a
///     flat DriverValue buffer rather than reflection-based POCO materialization. This makes
///     nested PLC types deterministic, source-generator friendly and usable from Native AOT.
/// </summary>
public sealed class DriverRecordSchema<TDescriptor>
    where TDescriptor : struct
{
    private readonly DriverRecordArrayMetadata[] _arrays;
    private readonly DriverRecordField<TDescriptor>[] _fields;
    private readonly DriverReadBatchItem<TDescriptor>[] _readItems;
    private readonly DriverWriteBatchItem<TDescriptor>[] _writeItems;

    private DriverRecordSchema(
        DriverRecordField<TDescriptor>[] fields,
        DriverReadBatchItem<TDescriptor>[] readItems,
        DriverWriteBatchItem<TDescriptor>[] writeItems,
        DriverRecordArrayMetadata[] arrays,
        int valueCount)
    {
        _fields = fields;
        _readItems = readItems;
        _writeItems = writeItems;
        _arrays = arrays;
        ValueCount = valueCount;
    }

    public ReadOnlyMemory<DriverRecordField<TDescriptor>> Fields => _fields;
    public ReadOnlyMemory<DriverReadBatchItem<TDescriptor>> ReadItems => _readItems;
    public ReadOnlyMemory<DriverWriteBatchItem<TDescriptor>> WriteItems => _writeItems;
    public ReadOnlyMemory<DriverRecordArrayMetadata> Arrays => _arrays;
    public int ValueCount { get; }

    public static DriverResult<DriverRecordSchema<TDescriptor>> Create(
        ReadOnlySpan<DriverRecordField<TDescriptor>> fields)
    {
        return Create(fields, ReadOnlySpan<DriverRecordArrayMetadata>.Empty);
    }

    internal static DriverResult<DriverRecordSchema<TDescriptor>> Create(
        ReadOnlySpan<DriverRecordField<TDescriptor>> fields,
        ReadOnlySpan<DriverRecordArrayMetadata> arrays)
    {
        if (fields.IsEmpty)
            return Failure("RECORD_EMPTY", "A STRUCT/UDT schema must contain at least one field.");

        var copy = fields.ToArray(); // configuration path only
        var reads = new DriverReadBatchItem<TDescriptor>[copy.Length];
        var writes = new DriverWriteBatchItem<TDescriptor>[copy.Length];
        var max = 0;

        for (var i = 0; i < copy.Length; i++)
        {
            var field = copy[i];
            if (string.IsNullOrWhiteSpace(field.Name))
                return Failure("RECORD_FIELD_NAME", $"Field {i} has an empty name.");
            if (field.ValueOffset < 0 || field.ValueCount <= 0)
                return Failure("RECORD_FIELD_RANGE", $"Field '{field.Name}' has an invalid value range.");

            for (var previous = 0; previous < i; previous++)
            {
                if (string.Equals(copy[previous].Name, field.Name, StringComparison.Ordinal))
                    return Failure("RECORD_FIELD_DUPLICATE", $"Field name '{field.Name}' is duplicated.");

                var leftStart = copy[previous].ValueOffset;
                var leftEnd = checked(leftStart + copy[previous].ValueCount);
                var rightEnd = checked(field.ValueOffset + field.ValueCount);
                if (field.ValueOffset < leftEnd && leftStart < rightEnd)
                    return Failure("RECORD_VALUE_OVERLAP",
                        $"Field '{field.Name}' overlaps the value range of '{copy[previous].Name}'.");
            }

            var end = checked(field.ValueOffset + field.ValueCount);
            if (end > max) max = end;
            reads[i] = new DriverReadBatchItem<TDescriptor>(field.ValueOffset, field.Descriptor);
            writes[i] = new DriverWriteBatchItem<TDescriptor>(field.ValueOffset, field.Descriptor);
        }

        var arrayCopy = arrays.ToArray();
        for (var i = 0; i < arrayCopy.Length; i++)
        {
            var array = arrayCopy[i];
            if (string.IsNullOrWhiteSpace(array.Name) || array.Shape is null || array.ValueOffset < 0 ||
                array.ElementValueCount <= 0)
                return Failure("RECORD_ARRAY_METADATA", $"Array metadata entry {i} is invalid.");
            if (array.ValueOffset + array.ValueCount > max)
                return Failure("RECORD_ARRAY_RANGE", $"Array '{array.Name}' exceeds the record value range.");
            for (var j = 0; j < i; j++)
                if (string.Equals(arrayCopy[j].Name, array.Name, StringComparison.Ordinal))
                    return Failure("RECORD_ARRAY_DUPLICATE", $"Array metadata name '{array.Name}' is duplicated.");
        }

        return DriverResult<DriverRecordSchema<TDescriptor>>.Success(
            new DriverRecordSchema<TDescriptor>(copy, reads, writes, arrayCopy, max));
    }

    public bool TryGetArray(string name, out DriverRecordArrayMetadata array)
    {
        for (var i = 0; i < _arrays.Length; i++)
            if (string.Equals(_arrays[i].Name, name, StringComparison.Ordinal))
            {
                array = _arrays[i];
                return true;
            }

        array = default;
        return false;
    }

    public bool TryGetField(string name, out DriverRecordField<TDescriptor> field)
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

    private static DriverResult<DriverRecordSchema<TDescriptor>> Failure(string code, string message)
    {
        return DriverResult<DriverRecordSchema<TDescriptor>>.Failure(new DriverError(
            DriverErrorKind.Configuration,
            code,
            message,
            Operation: DriverOperation.Validate,
            Cause: "The schema is internally inconsistent and cannot be compiled into a safe driver batch.",
            SuggestedAction:
            "Fix duplicate names, invalid ranges or overlapping value slots before connecting to the device.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}

public static class IndustrialDriverRecordExtensions
{
    public static ValueTask<DriverResult<int>> ReadRecordAsync<TDescriptor>(
        this IIndustrialDriver<TDescriptor> driver,
        DriverRecordSchema<TDescriptor> schema,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
        where TDescriptor : struct
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(schema);
        if (destination.Length < schema.ValueCount)
            return ValueTask.FromResult(DriverResult<int>.Failure(BufferError("RECORD_DESTINATION_SHORT",
                destination.Length, schema.ValueCount)));
        return driver.ReadBatchAsync(schema.ReadItems, destination, cancellationToken);
    }

    public static ValueTask<DriverResult<int>> WriteRecordAsync<TDescriptor>(
        this IIndustrialDriver<TDescriptor> driver,
        DriverRecordSchema<TDescriptor> schema,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default)
        where TDescriptor : struct
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(schema);
        if (source.Length < schema.ValueCount)
            return ValueTask.FromResult(
                DriverResult<int>.Failure(BufferError("RECORD_SOURCE_SHORT", source.Length, schema.ValueCount)));
        return driver.WriteBatchAsync(schema.WriteItems, source, cancellationToken);
    }

    private static DriverError BufferError(string code, int actual, int required)
    {
        return new DriverError(
            DriverErrorKind.Configuration,
            code,
            $"Record buffer contains {actual} values; {required} are required by the schema.",
            Operation: DriverOperation.Validate,
            Cause: "The caller-provided DriverValue buffer does not match the compiled STRUCT/UDT schema.",
            SuggestedAction: "Allocate a value buffer using schema.ValueCount and retry.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}
