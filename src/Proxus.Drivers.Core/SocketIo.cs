using System.Net.Sockets;

namespace Proxus.Drivers.Core;

public static class SocketIo
{
    public static async ValueTask SendAllAsync(
        Socket socket,
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var sent = 0;
        while (sent < buffer.Length)
        {
            int written;
            try
            {
                written = await socket.SendAsync(buffer[sent..], SocketFlags.None, CancellationToken.None)
                    .AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }

            if (written <= 0)
                throw new IOException("The socket closed while sending data.");

            sent += written;
        }
    }

    public static async ValueTask ReceiveExactlyAsync(
        Socket socket,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var received = 0;
        while (received < buffer.Length)
        {
            int count;
            try
            {
                count = await socket.ReceiveAsync(buffer[received..], SocketFlags.None, CancellationToken.None)
                    .AsTask().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                socket.Dispose();
                throw;
            }

            if (count == 0)
                throw new EndOfStreamException("The remote endpoint closed the connection.");

            received += count;
        }
    }
}
