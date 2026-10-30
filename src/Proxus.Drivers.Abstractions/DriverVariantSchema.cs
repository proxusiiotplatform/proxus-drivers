namespace Proxus.Drivers;

public readonly record struct DriverVariantCase<TDescriptor>(
    string Name,
    DriverValue Discriminator,
    DriverRecordSchema<TDescriptor> Schema)
    where TDescriptor : struct;

/// <summary>
///     Reflection-free discriminated union schema. Alternatives may overlay the same physical PLC memory;
///     the discriminator determines which record schema is active.
/// </summary>
public sealed class DriverVariantSchema<TDescriptor>
    where TDescriptor : struct
{
    private readonly DriverVariantCase<TDescriptor>[] _cases;

    private DriverVariantSchema(string name, DriverVariantCase<TDescriptor>[] cases, int maximumValueCount)
    {
        Name = name;
        _cases = cases;
        MaximumValueCount = maximumValueCount;
        DiscriminatorType = cases[0].Discriminator.Type;
    }

    public string Name { get; }
    public IndustrialDataType DiscriminatorType { get; }
    public ReadOnlyMemory<DriverVariantCase<TDescriptor>> Cases => _cases;
    public int MaximumValueCount { get; }

    public static DriverResult<DriverVariantSchema<TDescriptor>> Create(
        string name,
        ReadOnlySpan<DriverVariantCase<TDescriptor>> cases)
    {
        if (string.IsNullOrWhiteSpace(name)) return Failure("VARIANT_NAME", "Variant schema name cannot be empty.");
        if (cases.IsEmpty) return Failure("VARIANT_EMPTY", "Variant schema must contain at least one case.");
        var copy = cases.ToArray();
        var type = copy[0].Discriminator.Type;
        var max = 0;
        for (var i = 0; i < copy.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(copy[i].Name))
                return Failure("VARIANT_CASE_NAME", $"Variant case {i} has an empty name.");
            if (copy[i].Schema is null)
                return Failure("VARIANT_CASE_SCHEMA", $"Variant case '{copy[i].Name}' has no schema.");
            if (copy[i].Discriminator.Type != type)
                return Failure("VARIANT_DISCRIMINATOR_TYPE",
                    "All variant discriminators must use the same canonical type.");
            for (var j = 0; j < i; j++)
            {
                if (string.Equals(copy[j].Name, copy[i].Name, StringComparison.Ordinal))
                    return Failure("VARIANT_CASE_DUPLICATE_NAME", $"Variant case name '{copy[i].Name}' is duplicated.");
                if (copy[j].Discriminator == copy[i].Discriminator)
                    return Failure("VARIANT_CASE_DUPLICATE_VALUE",
                        $"Variant discriminator '{copy[i].Discriminator}' is duplicated.");
            }

            max = Math.Max(max, copy[i].Schema.ValueCount);
        }

        return DriverResult<DriverVariantSchema<TDescriptor>>.Success(
            new DriverVariantSchema<TDescriptor>(name, copy, max));
    }

    public bool TryResolve(DriverValue discriminator, out DriverVariantCase<TDescriptor> variantCase)
    {
        if (discriminator.Type != DiscriminatorType)
        {
            variantCase = default;
            return false;
        }

        for (var i = 0; i < _cases.Length; i++)
            if (_cases[i].Discriminator == discriminator)
            {
                variantCase = _cases[i];
                return true;
            }

        variantCase = default;
        return false;
    }

    private static DriverResult<DriverVariantSchema<TDescriptor>> Failure(string code, string message)
    {
        return DriverResult<DriverVariantSchema<TDescriptor>>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Cause: "The discriminated union schema is ambiguous or incomplete.",
            SuggestedAction: "Use unique case names and discriminator values with one common discriminator type.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}

public readonly record struct DriverVariantSelection(string CaseName, DriverValue Discriminator, int ValueCount);

public static class IndustrialDriverVariantExtensions
{
    /// <summary>
    ///     Reads the discriminator first and then only the active union payload. The destination buffer
    ///     is sized for schema.MaximumValueCount, not for every alternative combined.
    /// </summary>
    public static async ValueTask<DriverResult<DriverVariantSelection>> ReadVariantAsync<TDescriptor>(
        this IIndustrialDriver<TDescriptor> driver,
        TDescriptor discriminatorDescriptor,
        DriverVariantSchema<TDescriptor> schema,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
        where TDescriptor : struct
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(schema);
        if (destination.Length < schema.MaximumValueCount)
            return DriverResult<DriverVariantSelection>.Failure(BufferError("VARIANT_DESTINATION_SHORT",
                destination.Length, schema.MaximumValueCount));

        var discriminatorResult =
            await driver.ReadValueAsync(discriminatorDescriptor, cancellationToken).ConfigureAwait(false);
        if (!discriminatorResult.IsSuccess)
            return DriverResult<DriverVariantSelection>.Failure(discriminatorResult.Error);
        if (!schema.TryResolve(discriminatorResult.Value, out var activeCase))
            return DriverResult<DriverVariantSelection>.Failure(new DriverError(
                DriverErrorKind.Data,
                "VARIANT_UNKNOWN_DISCRIMINATOR",
                $"No case in variant '{schema.Name}' matches discriminator {discriminatorResult.Value}.",
                Operation: DriverOperation.Decode,
                DataType: discriminatorResult.Value.Type,
                Cause: "The device returned a discriminator value that is not declared by the compiled union schema.",
                SuggestedAction: "Update the variant schema or verify the discriminator address/type.",
                Retry: DriverRetryDisposition.FixConfiguration,
                Connection: DriverConnectionDisposition.Unchanged));

        var read = await driver.ReadRecordAsync(activeCase.Schema, destination, cancellationToken)
            .ConfigureAwait(false);
        if (!read.IsSuccess) return DriverResult<DriverVariantSelection>.Failure(read.Error);
        return DriverResult<DriverVariantSelection>.Success(new DriverVariantSelection(activeCase.Name,
            discriminatorResult.Value, read.Value));
    }

    /// <summary>
    ///     Writes the active payload first and commits the discriminator last, avoiding a transient state
    ///     where readers observe a new union tag with an old payload.
    /// </summary>
    public static async ValueTask<DriverResult<int>> WriteVariantAsync<TDescriptor>(
        this IIndustrialDriver<TDescriptor> driver,
        TDescriptor discriminatorDescriptor,
        DriverVariantSchema<TDescriptor> schema,
        DriverValue discriminator,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default)
        where TDescriptor : struct
    {
        ArgumentNullException.ThrowIfNull(driver);
        ArgumentNullException.ThrowIfNull(schema);
        if (!schema.TryResolve(discriminator, out var activeCase))
            return DriverResult<int>.Failure(new DriverError(
                DriverErrorKind.Configuration,
                "VARIANT_UNKNOWN_DISCRIMINATOR",
                $"No case in variant '{schema.Name}' matches discriminator {discriminator}.",
                Operation: DriverOperation.Validate,
                DataType: discriminator.Type,
                Cause: "The caller selected a discriminator that is not declared by the compiled union schema.",
                SuggestedAction: "Select one of schema.Cases before writing.",
                Retry: DriverRetryDisposition.FixConfiguration,
                Connection: DriverConnectionDisposition.Unchanged));
        if (source.Length < activeCase.Schema.ValueCount)
            return DriverResult<int>.Failure(BufferError("VARIANT_SOURCE_SHORT", source.Length,
                activeCase.Schema.ValueCount));

        var payloadWrite = await driver.WriteRecordAsync(activeCase.Schema, source, cancellationToken)
            .ConfigureAwait(false);
        if (!payloadWrite.IsSuccess) return DriverResult<int>.Failure(payloadWrite.Error);
        var discriminatorWrite = await driver.WriteValueAsync(discriminatorDescriptor, discriminator, cancellationToken)
            .ConfigureAwait(false);
        if (!discriminatorWrite.IsSuccess) return DriverResult<int>.Failure(discriminatorWrite.Error);
        return DriverResult<int>.Success(payloadWrite.Value);
    }

    private static DriverError BufferError(string code, int actual, int required)
    {
        return new DriverError(
            DriverErrorKind.Configuration,
            code,
            $"Variant value buffer contains {actual} values; {required} are required.",
            Operation: DriverOperation.Validate,
            Cause: "The caller-provided buffer is smaller than the active union payload.",
            SuggestedAction:
            "Allocate destination using schema.MaximumValueCount and source using the selected case schema.ValueCount.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}
