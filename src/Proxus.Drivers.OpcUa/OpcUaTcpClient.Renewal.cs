namespace Proxus.Drivers.OpcUa;

public sealed partial class OpcUaTcpClient
{
    private CancellationTokenSource? _renewalCancellation;

    private void StartSecureChannelRenewal()
    {
        StopSecureChannelRenewal();
        var cancellation = new CancellationTokenSource();
        _renewalCancellation = cancellation;
        _ = RenewSecureChannelLoopAsync(cancellation.Token);
    }

    private void StopSecureChannelRenewal()
    {
        var cancellation = Interlocked.Exchange(ref _renewalCancellation, null);
        if (cancellation is null) return;
        cancellation.Cancel();
        cancellation.Dispose();
    }

    private async Task RenewSecureChannelLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && SecureChannel is { } channel)
            {
                var delayMilliseconds = Math.Max(1_000, channel.RevisedLifetimeMilliseconds * 3L / 4L);
                await Task.Delay(TimeSpan.FromMilliseconds(delayMilliseconds), cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) return;
                var result = await RenewSecureChannelAsync(cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    CloseSocket();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
