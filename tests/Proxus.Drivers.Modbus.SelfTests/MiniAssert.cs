namespace Proxus.Drivers.Modbus.SelfTests;

internal static class MiniAssert
{
    public static void True(bool condition, string? message = null)
    {
        if (!condition)
            throw new InvalidOperationException(message ?? "Expected true.");
    }

    public static void False(bool condition, string? message = null)
    {
        True(!condition, message ?? "Expected false.");
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException(message ?? $"Expected '{expected}', actual '{actual}'.");
    }

    public static void SequenceEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual)
        where T : IEquatable<T>
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException("Sequences are not equal.");
    }

    public static void NearlyEqual(double expected, double actual, double tolerance)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"Expected {expected:R} ± {tolerance:R}, actual {actual:R}.");
    }
}
