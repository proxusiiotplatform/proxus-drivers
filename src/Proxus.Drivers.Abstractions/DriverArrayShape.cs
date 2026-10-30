namespace Proxus.Drivers;

/// <summary>Immutable row-major rank-N array shape used by PLC arrays, record arrays and importers.</summary>
public sealed class DriverArrayShape
{
    private readonly int[] _dimensions;
    private readonly int[] _strides;

    private DriverArrayShape(int[] dimensions, int[] strides, int elementCount)
    {
        _dimensions = dimensions;
        _strides = strides;
        ElementCount = elementCount;
    }

    public int Rank => _dimensions.Length;
    public int ElementCount { get; }
    public ReadOnlyMemory<int> Dimensions => _dimensions;

    public static DriverResult<DriverArrayShape> Create(ReadOnlySpan<int> dimensions)
    {
        return CreateCore(dimensions, false);
    }

    /// <summary>Creates a runtime array shape. OPC UA and other dynamic protocols may legally return empty arrays.</summary>
    public static DriverResult<DriverArrayShape> CreateRuntime(ReadOnlySpan<int> dimensions)
    {
        return CreateCore(dimensions, true);
    }

    private static DriverResult<DriverArrayShape> CreateCore(ReadOnlySpan<int> dimensions, bool allowZeroLength)
    {
        if (dimensions.IsEmpty)
            return Failure("ARRAY_SHAPE_EMPTY", "Array shape must contain at least one dimension.");
        if (dimensions.Length > 32)
            return Failure("ARRAY_SHAPE_RANK", "Array rank cannot exceed 32 dimensions.");

        var copy = dimensions.ToArray();
        var strides = new int[copy.Length];
        var count = 1;
        try
        {
            for (var i = copy.Length - 1; i >= 0; i--)
            {
                if (copy[i] < 0 || (!allowZeroLength && copy[i] == 0))
                    return Failure("ARRAY_SHAPE_DIMENSION", allowZeroLength
                        ? $"Dimension {i} cannot be negative."
                        : $"Dimension {i} must be greater than zero.");
                strides[i] = count;
                count = checked(count * copy[i]);
            }
        }
        catch (OverflowException)
        {
            return Failure("ARRAY_SHAPE_OVERFLOW", "The array element count exceeds Int32 capacity.");
        }

        return DriverResult<DriverArrayShape>.Success(new DriverArrayShape(copy, strides, count));
    }

    public bool TryGetLinearIndex(ReadOnlySpan<int> indices, out int linearIndex)
    {
        linearIndex = 0;
        if (indices.Length != Rank) return false;
        for (var i = 0; i < Rank; i++)
        {
            if ((uint)indices[i] >= (uint)_dimensions[i])
            {
                linearIndex = 0;
                return false;
            }

            linearIndex += indices[i] * _strides[i];
        }

        return true;
    }

    public bool TryGetIndices(int linearIndex, Span<int> destination)
    {
        if ((uint)linearIndex >= (uint)ElementCount || destination.Length < Rank)
            return false;
        var remaining = linearIndex;
        for (var i = 0; i < Rank; i++)
        {
            destination[i] = remaining / _strides[i];
            remaining %= _strides[i];
        }

        return true;
    }

    public string FormatIndices(int linearIndex)
    {
        var indices = Rank <= 16 ? stackalloc int[Rank] : new int[Rank];
        if (!TryGetIndices(linearIndex, indices))
            throw new ArgumentOutOfRangeException(nameof(linearIndex));
        return $"[{string.Join(',', [.. indices])}]";
    }

    private static DriverResult<DriverArrayShape> Failure(string code, string message)
    {
        return DriverResult<DriverArrayShape>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Cause: "The declared array shape cannot be represented safely.",
            SuggestedAction: "Use positive dimensions whose product fits Int32.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
