namespace Proxus.Drivers.Core;

/// <summary>
///     Creates a linked timeout token only when a finite timeout is configured. An
///     infinite timeout keeps the connected steady-state path free of timeout-CTS
///     allocations and lets the caller own the read-cycle budget.
/// </summary>
public readonly struct TimeoutTokenScope : IDisposable
{
    public static readonly TimeSpan MaximumSupportedTimeout =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1D);

    private readonly CancellationTokenSource? _source;

    private TimeoutTokenScope(CancellationToken token, CancellationTokenSource? source)
    {
        Token = token;
        _source = source;
    }

    public CancellationToken Token { get; }

    public static TimeoutTokenScope Create(CancellationToken callerToken, TimeSpan timeout)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
            return new TimeoutTokenScope(callerToken, null);

        if (timeout <= TimeSpan.Zero || timeout > MaximumSupportedTimeout)
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                $"Timeout must be positive, infinite, or no greater than {MaximumSupportedTimeout}.");

        var source = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        source.CancelAfter(timeout);
        return new TimeoutTokenScope(source.Token, source);
    }

    public void Dispose()
    {
        _source?.Dispose();
    }
}
