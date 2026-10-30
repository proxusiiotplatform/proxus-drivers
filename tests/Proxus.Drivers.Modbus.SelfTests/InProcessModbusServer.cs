using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Proxus.Drivers.Core;

namespace Proxus.Drivers.Modbus.SelfTests;

public enum ServerFault
{
    None,
    CorruptTransactionId,
    CorruptProtocolId,
    CorruptUnitId,
    InvalidMbapLength,
    WrongFunction,
    InvalidByteCount,
    MalformedException,
    CloseBeforeResponse,
    CloseMidResponse
}

// ReSharper disable once UnusedType.Global -- linked into self-test, AOT, differential, and performance executables.
public sealed class InProcessModbusServer : IAsyncDisposable
{
    private readonly ConcurrentDictionary<int, Task> _clientTasks = new();
    private readonly ConcurrentDictionary<int, Socket> _clients = new();
    private readonly bool[] _coils = new bool[ushort.MaxValue + 1];
    private readonly bool[] _discrete = new bool[ushort.MaxValue + 1];
    private readonly ConcurrentDictionary<uint, ushort> _fileRecords = new();

    private readonly TaskCompletionSource _firstRequestObserved =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly ushort[] _holding = new ushort[ushort.MaxValue + 1];
    private readonly ushort[] _input = new ushort[ushort.MaxValue + 1];
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private Task? _acceptLoop;
    private int _activeRequests;
    private int _clientSequence;
    private int _connectionCount;
    private int _maximumConcurrentRequests;
    private int _requestCount;

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public TimeSpan ResponseDelay { get; set; }
    public bool FragmentResponses { get; set; }
    public bool CloseAfterResponse { get; set; }
    public byte? ForcedExceptionCode { get; set; }
    public ServerFault Fault { get; set; }
    public int RequestCount => Volatile.Read(ref _requestCount);
    public int ConnectionCount => Volatile.Read(ref _connectionCount);
    public Task FirstRequestObserved => _firstRequestObserved.Task;
    public int TransactionIdDelta { get; set; }

    /// <summary>Speak Modbus RTU (Unit ID, PDU, CRC-16) instead of MBAP on accepted connections.</summary>
    public bool RtuFraming { get; set; } = DefaultRtuFraming;

    /// <summary>Initial <see cref="RtuFraming" /> for new servers; lets one test body run over both framings.</summary>
    public static bool DefaultRtuFraming { get; set; }

    /// <summary>Speak Modbus ASCII (":", hex Unit ID, PDU and LRC, CR LF) on accepted connections.</summary>
    public bool AsciiFraming { get; set; } = DefaultAsciiFraming;

    /// <summary>Initial <see cref="AsciiFraming" /> for new servers; lets one test body run over ASCII too.</summary>
    public static bool DefaultAsciiFraming { get; set; }

    /// <summary>Invert the LRC of the next ASCII response.</summary>
    public bool CorruptNextAsciiLrc { get; set; }

    /// <summary>Send ASCII responses in lower-case hexadecimal.</summary>
    public bool AsciiLowerCase { get; set; }

    /// <summary>Characters sent before the colon of the next ASCII response (line noise).</summary>
    public string AsciiNoiseBeforeStart { get; set; } = string.Empty;

    /// <summary>Raw characters of the most recent ASCII request frame, colon through LF.</summary>
    public byte[] LastAsciiRequest { get; private set; } = [];

    /// <summary>Invert the CRC of the next RTU response.</summary>
    public bool CorruptNextRtuCrc { get; set; }

    /// <summary>Raw bytes of the most recent RTU request frame, CRC included.</summary>
    public byte[] LastRtuRequest { get; private set; } = [];

    public bool CorruptByteCount
    {
        get => Fault == ServerFault.InvalidByteCount;
        set
        {
            if (value) Fault = ServerFault.InvalidByteCount;
            else if (Fault == ServerFault.InvalidByteCount) Fault = ServerFault.None;
        }
    }

    public int MaximumConcurrentRequests => Volatile.Read(ref _maximumConcurrentRequests);

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();

        foreach (var socket in _clients.Values)
            socket.Dispose();

        if (_acceptLoop is not null)
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }

        var tasks = _clientTasks.Values.ToArray();
        if (tasks.Length > 0)
            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)
            {
            }

        _stop.Dispose();
    }

    public void SetHolding(ushort address, ushort value)
    {
        _holding[address] = value;
    }

    public ushort GetHolding(ushort address)
    {
        return _holding[address];
    }

    public void SetInput(ushort address, ushort value)
    {
        _input[address] = value;
    }

    public ushort GetInput(ushort address)
    {
        return _input[address];
    }

    public void SetCoil(ushort address, bool value)
    {
        _coils[address] = value;
    }

    public bool GetCoil(ushort address)
    {
        return _coils[address];
    }

    public void SetDiscrete(ushort address, bool value)
    {
        _discrete[address] = value;
    }

    public bool GetDiscrete(ushort address)
    {
        return _discrete[address];
    }

    public void SetFileRecord(ushort fileNumber, ushort recordNumber, ushort value)
    {
        _fileRecords[FileRecordKey(fileNumber, recordNumber)] = value;
    }

    public ushort GetFileRecord(ushort fileNumber, ushort recordNumber)
    {
        return _fileRecords.TryGetValue(FileRecordKey(fileNumber, recordNumber), out var value) ? value : (ushort)0;
    }

    public void Start()
    {
        if (_acceptLoop is not null)
            throw new InvalidOperationException("Server is already started.");

        _listener.Start();
        _acceptLoop = AcceptLoopAsync(_stop.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket socket;
            try
            {
                socket = await _listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var id = Interlocked.Increment(ref _clientSequence);
            Interlocked.Increment(ref _connectionCount);
            _clients[id] = socket;
            var task = HandleClientTrackedAsync(id, socket, cancellationToken);
            _clientTasks[id] = task;
        }
    }

    private async Task HandleClientTrackedAsync(
        int id,
        Socket socket,
        CancellationToken cancellationToken)
    {
        try
        {
            await HandleClientAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _clients.TryRemove(id, out _);
            _clientTasks.TryRemove(id, out _);
            socket.Dispose();
        }
    }

    private async Task HandleClientAsync(Socket socket, CancellationToken cancellationToken)
    {
        if (RtuFraming)
        {
            await HandleRtuClientAsync(socket, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (AsciiFraming)
        {
            await HandleAsciiClientAsync(socket, cancellationToken).ConfigureAwait(false);
            return;
        }

        var header = new byte[7];
        var pdu = new byte[253];
        var response = new byte[260];

        while (!cancellationToken.IsCancellationRequested)
            try
            {
                await SocketIo.ReceiveExactlyAsync(socket, header, cancellationToken).ConfigureAwait(false);
                var transaction = BinaryPrimitives.ReadUInt16BigEndian(header);
                var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2));
                var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
                var unit = header[6];

                if (protocolId != 0 || length is < 2 or > 254)
                    return;

                var pduLength = length - 1;
                await SocketIo.ReceiveExactlyAsync(
                        socket,
                        pdu.AsMemory(0, pduLength),
                        cancellationToken)
                    .ConfigureAwait(false);

                Interlocked.Increment(ref _requestCount);
                _firstRequestObserved.TrySetResult();
                var active = Interlocked.Increment(ref _activeRequests);
                UpdateMaximum(active);

                try
                {
                    if (ResponseDelay > TimeSpan.Zero)
                        await Task.Delay(ResponseDelay, cancellationToken).ConfigureAwait(false);

                    if (Fault == ServerFault.CloseBeforeResponse)
                        return;

                    var responsePduLength = BuildResponse(
                        pdu.AsSpan(0, pduLength),
                        response.AsSpan(7));

                    var responseTransaction = Fault == ServerFault.CorruptTransactionId
                        ? unchecked((ushort)(transaction + 1))
                        : TransactionIdDelta == 0
                            ? transaction
                            : unchecked((ushort)(transaction + TransactionIdDelta));
                    var responseProtocol = Fault == ServerFault.CorruptProtocolId ? (ushort)1 : (ushort)0;
                    var responseUnit = Fault == ServerFault.CorruptUnitId
                        ? unchecked((byte)(unit + 1))
                        : unit;
                    var responseLength = Fault == ServerFault.InvalidMbapLength
                        ? (ushort)1
                        : checked((ushort)(responsePduLength + 1));

                    if (Fault == ServerFault.WrongFunction && responsePduLength > 0)
                        response[7] = unchecked((byte)(response[7] + 1));
                    if (Fault == ServerFault.InvalidByteCount && responsePduLength > 1)
                        response[8] = unchecked((byte)(response[8] + 1));
                    if (Fault == ServerFault.MalformedException && responsePduLength == 2 && (response[7] & 0x80) != 0)
                        responsePduLength = 1;

                    BinaryPrimitives.WriteUInt16BigEndian(response, responseTransaction);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2), responseProtocol);
                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4), responseLength);
                    response[6] = responseUnit;
                    var total = 7 + responsePduLength;

                    if (Fault == ServerFault.CloseMidResponse)
                    {
                        await SocketIo.SendAllAsync(socket, response.AsMemory(0, 8), cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    if (FragmentResponses)
                    {
                        var offset = 0;
                        while (offset < total)
                        {
                            var chunk = Math.Min(3, total - offset);
                            await SocketIo.SendAllAsync(
                                    socket,
                                    response.AsMemory(offset, chunk),
                                    cancellationToken)
                                .ConfigureAwait(false);
                            offset += chunk;
                            if (offset < total)
                                await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        await SocketIo.SendAllAsync(
                                socket,
                                response.AsMemory(0, total),
                                cancellationToken)
                            .ConfigureAwait(false);
                    }

                    if (CloseAfterResponse)
                        return;
                }
                finally
                {
                    Interlocked.Decrement(ref _activeRequests);
                }
            }
            catch (Exception ex) when (ex is EndOfStreamException or SocketException or
                                           ObjectDisposedException or OperationCanceledException or
                                           ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                return;
            }
    }

    /// <summary>
    ///     Independent Modbus RTU server loop. Request lengths follow the Modbus Application Protocol request
    ///     layouts; a frame with a bad CRC is ignored, as a serial slave would.
    /// </summary>
    private async Task HandleRtuClientAsync(Socket socket, CancellationToken cancellationToken)
    {
        var frame = new byte[256];
        var response = new byte[260];
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                await SocketIo.ReceiveExactlyAsync(socket, frame.AsMemory(0, 2), cancellationToken).ConfigureAwait(false);
                var length = await RtuRequestLengthAsync(socket, frame, cancellationToken).ConfigureAwait(false);
                if (length < 4)
                    return;
                LastRtuRequest = frame.AsSpan(0, length).ToArray();
                if (RtuCrc(frame.AsSpan(0, length - 2)) != BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(length - 2)))
                    continue;

                Interlocked.Increment(ref _requestCount);
                _firstRequestObserved.TrySetResult();
                if (ResponseDelay > TimeSpan.Zero)
                    await Task.Delay(ResponseDelay, cancellationToken).ConfigureAwait(false);
                if (Fault == ServerFault.CloseBeforeResponse)
                    return;

                var unit = frame[0];
                var pduLength = frame[1] == 0x41
                    ? EchoVendorPdu(frame.AsSpan(1, length - 3), response.AsSpan(1))
                    : BuildResponse(frame.AsSpan(1, length - 3), response.AsSpan(1));
                response[0] = Fault == ServerFault.CorruptUnitId ? unchecked((byte)(unit + 1)) : unit;
                var crc = RtuCrc(response.AsSpan(0, 1 + pduLength));
                if (CorruptNextRtuCrc)
                {
                    crc = (ushort)~crc;
                    CorruptNextRtuCrc = false;
                }

                BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(1 + pduLength), crc);
                var total = 1 + pduLength + 2;
                if (FragmentResponses)
                    for (var offset = 0; offset < total; offset += 3)
                    {
                        await SocketIo.SendAllAsync(socket, response.AsMemory(offset, Math.Min(3, total - offset)),
                            cancellationToken).ConfigureAwait(false);
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    }
                else
                    await SocketIo.SendAllAsync(socket, response.AsMemory(0, total), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is EndOfStreamException or SocketException or ObjectDisposedException or
                                           OperationCanceledException or ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                return;
            }
    }

    /// <summary>
    ///     Independent Modbus ASCII server loop: a request is a line from ':' to LF; a line with a bad LRC or a
    ///     character that is not hexadecimal is ignored, as a serial slave would.
    /// </summary>
    private async Task HandleAsciiClientAsync(Socket socket, CancellationToken cancellationToken)
    {
        var line = new List<byte>(513);
        var one = new byte[1];
        var response = new byte[260];
        while (!cancellationToken.IsCancellationRequested)
            try
            {
                line.Clear();
                do
                {
                    await SocketIo.ReceiveExactlyAsync(socket, one, cancellationToken).ConfigureAwait(false);
                    if (one[0] == (byte)':') line.Clear();
                    line.Add(one[0]);
                } while (one[0] != (byte)'\n' && line.Count < 600);

                LastAsciiRequest = [.. line];
                var text = Encoding.ASCII.GetString(LastAsciiRequest);
                if (!text.StartsWith(':') || !text.EndsWith("\r\n") || text.Length % 2 != 1)
                    continue;
                byte[] binary;
                try
                {
                    binary = Convert.FromHexString(text[1..^2]);
                }
                catch (FormatException)
                {
                    continue;
                }

                var sum = 0;
                foreach (var value in binary) sum += value;
                if (binary.Length < 3 || (sum & 0xFF) != 0)
                    continue;

                Interlocked.Increment(ref _requestCount);
                _firstRequestObserved.TrySetResult();
                if (ResponseDelay > TimeSpan.Zero)
                    await Task.Delay(ResponseDelay, cancellationToken).ConfigureAwait(false);
                if (Fault == ServerFault.CloseBeforeResponse)
                    return;

                var unit = binary[0];
                var request = binary.AsSpan(1, binary.Length - 2);
                var pduLength = request[0] == 0x41
                    ? EchoVendorPdu(request, response.AsSpan(1))
                    : BuildResponse(request, response.AsSpan(1));
                response[0] = Fault == ServerFault.CorruptUnitId ? unchecked((byte)(unit + 1)) : unit;
                var total = 0;
                for (var i = 0; i < 1 + pduLength; i++) total += response[i];
                var lrc = (byte)(-total & 0xFF);
                if (CorruptNextAsciiLrc)
                {
                    lrc = (byte)~lrc;
                    CorruptNextAsciiLrc = false;
                }

                response[1 + pduLength] = lrc;
                var hex = Convert.ToHexString(response, 0, 2 + pduLength);
                if (AsciiLowerCase) hex = hex.ToLowerInvariant();
                var frame = Encoding.ASCII.GetBytes(AsciiNoiseBeforeStart + ":" + hex + "\r\n");
                AsciiNoiseBeforeStart = string.Empty;
                if (FragmentResponses)
                    for (var offset = 0; offset < frame.Length; offset += 3)
                    {
                        await SocketIo.SendAllAsync(socket, frame.AsMemory(offset, Math.Min(3, frame.Length - offset)),
                            cancellationToken).ConfigureAwait(false);
                        await Task.Delay(1, cancellationToken).ConfigureAwait(false);
                    }
                else
                    await SocketIo.SendAllAsync(socket, frame, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is EndOfStreamException or SocketException or ObjectDisposedException or
                                           OperationCanceledException or ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                return;
            }
    }

    private static int EchoVendorPdu(ReadOnlySpan<byte> request, Span<byte> response)
    {
        request.CopyTo(response);
        return request.Length;
    }

    private static async Task<int> RtuRequestLengthAsync(Socket socket, byte[] frame, CancellationToken cancellationToken)
    {
        async Task Need(int count, int have) =>
            await SocketIo.ReceiveExactlyAsync(socket, frame.AsMemory(have, count - have), cancellationToken).ConfigureAwait(false);

        int total;
        switch (frame[1])
        {
            case 0x01: case 0x02: case 0x03: case 0x04: case 0x05: case 0x06: case 0x08:
                total = 8;
                break;
            case 0x07: case 0x0B: case 0x0C: case 0x11:
                total = 4;
                break;
            case 0x0F: case 0x10:
                await Need(7, 2).ConfigureAwait(false);
                total = 7 + frame[6] + 2;
                await Need(total, 7).ConfigureAwait(false);
                return total;
            case 0x14: case 0x15:
                await Need(3, 2).ConfigureAwait(false);
                total = 3 + frame[2] + 2;
                await Need(total, 3).ConfigureAwait(false);
                return total;
            case 0x16:
                total = 10;
                break;
            case 0x17:
                await Need(11, 2).ConfigureAwait(false);
                total = 11 + frame[10] + 2;
                await Need(total, 11).ConfigureAwait(false);
                return total;
            case 0x18:
                total = 6;
                break;
            case 0x2B:
                total = 7;
                break;
            case 0x41: // vendor-specific test function: unit, 0x41, one data byte, CRC
                total = 5;
                break;
            default:
                return 0;
        }

        await Need(total, 2).ConfigureAwait(false);
        return total;
    }

    /// <summary>CRC-16/MODBUS computed bytewise with a lookup-free shift register (fixture-side copy).</summary>
    public static ushort RtuCrc(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x0001) == 1 ? (crc >> 1) ^ 0xA001 : crc >> 1;
        }

        return (ushort)crc;
    }

    private int BuildResponse(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.IsEmpty)
            return BuildException(0, 3, response);

        var function = request[0];
        if (ForcedExceptionCode is { } exception)
            return BuildException(function, exception, response);

        return function switch
        {
            0x01 => BuildBitReadResponse(function, _coils, request, response),
            0x02 => BuildBitReadResponse(function, _discrete, request, response),
            0x03 => BuildRegisterReadResponse(function, _holding, request, response),
            0x04 => BuildRegisterReadResponse(function, _input, request, response),
            0x05 => WriteSingleCoil(request, response),
            0x06 => WriteSingleRegister(request, response),
            0x07 => ReadExceptionStatus(request, response),
            0x08 => Diagnostics(request, response),
            0x0B => GetCommEventCounter(request, response),
            0x0C => GetCommEventLog(request, response),
            0x0F => WriteMultipleCoils(request, response),
            0x10 => WriteMultipleRegisters(request, response),
            0x11 => ReportServerId(request, response),
            0x14 => ReadFileRecord(request, response),
            0x15 => WriteFileRecord(request, response),
            0x16 => MaskWriteRegister(request, response),
            0x17 => ReadWriteMultipleRegisters(request, response),
            0x18 => ReadFifoQueue(request, response),
            0x2B => EncapsulatedInterfaceTransport(request, response),
            0x41 => UserDefinedEcho(request, response),
            _ => BuildException(function, 1, response)
        };
    }

    private static int BuildException(byte function, byte code, Span<byte> response)
    {
        response[0] = (byte)(function | 0x80);
        response[1] = code;
        return 2;
    }

    private static int BuildRegisterReadResponse(
        byte function,
        IReadOnlyList<ushort> source,
        ReadOnlySpan<byte> request,
        Span<byte> response)
    {
        if (request.Length != 5)
            return BuildException(function, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        if (quantity is < 1 or > 125 || (uint)address + quantity > source.Count)
            return BuildException(function, 2, response);

        response[0] = function;
        response[1] = checked((byte)(quantity * 2));
        for (var i = 0; i < quantity; i++)
            BinaryPrimitives.WriteUInt16BigEndian(response[(2 + i * 2)..], source[address + i]);
        return 2 + quantity * 2;
    }

    private static int BuildBitReadResponse(
        byte function,
        IReadOnlyList<bool> source,
        ReadOnlySpan<byte> request,
        Span<byte> response)
    {
        if (request.Length != 5)
            return BuildException(function, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        if (quantity is < 1 or > 2000 || (uint)address + quantity > source.Count)
            return BuildException(function, 2, response);

        var byteCount = (quantity + 7) / 8;
        response[..(2 + byteCount)].Clear();
        response[0] = function;
        response[1] = checked((byte)byteCount);
        for (var i = 0; i < quantity; i++)
            if (source[address + i])
                response[2 + (i >> 3)] |= (byte)(1 << (i & 7));
        return 2 + byteCount;
    }

    private int WriteSingleCoil(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 5)
            return BuildException(0x05, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var encoded = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        if (encoded is not (0x0000 or 0xFF00))
            return BuildException(0x05, 3, response);

        _coils[address] = encoded == 0xFF00;
        request.CopyTo(response);
        return request.Length;
    }

    private int WriteSingleRegister(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 5)
            return BuildException(0x06, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        _holding[address] = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        request.CopyTo(response);
        return request.Length;
    }

    private int WriteMultipleCoils(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 7)
            return BuildException(0x0F, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        var byteCount = request[5];
        if (quantity is < 1 or > 1968 || byteCount != (quantity + 7) / 8 ||
            request.Length != 6 + byteCount || (uint)address + quantity > _coils.Length)
            return BuildException(0x0F, 3, response);

        for (var i = 0; i < quantity; i++)
            _coils[address + i] = (request[6 + (i >> 3)] & (1 << (i & 7))) != 0;

        response[0] = 0x0F;
        request.Slice(1, 4).CopyTo(response[1..]);
        return 5;
    }

    private int WriteMultipleRegisters(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 7)
            return BuildException(0x10, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var quantity = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        var byteCount = request[5];
        if (quantity is < 1 or > 123 || byteCount != quantity * 2 ||
            request.Length != 6 + byteCount || (uint)address + quantity > _holding.Length)
            return BuildException(0x10, 3, response);

        for (var i = 0; i < quantity; i++)
            _holding[address + i] = BinaryPrimitives.ReadUInt16BigEndian(request[(6 + i * 2)..]);

        response[0] = 0x10;
        request.Slice(1, 4).CopyTo(response[1..]);
        return 5;
    }

    private int MaskWriteRegister(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 7)
            return BuildException(0x16, 3, response);

        var address = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var andMask = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        var orMask = BinaryPrimitives.ReadUInt16BigEndian(request[5..]);
        var current = _holding[address];
        _holding[address] = (ushort)((current & andMask) | (orMask & ~andMask));
        request.CopyTo(response);
        return request.Length;
    }

    private int ReadWriteMultipleRegisters(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 11)
            return BuildException(0x17, 3, response);

        var readAddress = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var readQuantity = BinaryPrimitives.ReadUInt16BigEndian(request[3..]);
        var writeAddress = BinaryPrimitives.ReadUInt16BigEndian(request[5..]);
        var writeQuantity = BinaryPrimitives.ReadUInt16BigEndian(request[7..]);
        var writeByteCount = request[9];

        if (readQuantity is < 1 or > 125 || writeQuantity is < 1 or > 121 ||
            writeByteCount != writeQuantity * 2 || request.Length != 10 + writeByteCount ||
            (uint)readAddress + readQuantity > _holding.Length ||
            (uint)writeAddress + writeQuantity > _holding.Length)
            return BuildException(0x17, 3, response);

        for (var i = 0; i < writeQuantity; i++)
            _holding[writeAddress + i] = BinaryPrimitives.ReadUInt16BigEndian(request[(10 + i * 2)..]);

        response[0] = 0x17;
        response[1] = checked((byte)(readQuantity * 2));
        for (var i = 0; i < readQuantity; i++)
            BinaryPrimitives.WriteUInt16BigEndian(response[(2 + i * 2)..], _holding[readAddress + i]);
        return 2 + readQuantity * 2;
    }


    private static uint FileRecordKey(ushort fileNumber, ushort recordNumber)
    {
        return ((uint)fileNumber << 16) | recordNumber;
    }

    private static int ReadExceptionStatus(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 1) return BuildException(0x07, 3, response);
        response[0] = 0x07;
        response[1] = 0xA5;
        return 2;
    }

    private static int Diagnostics(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 5) return BuildException(0x08, 3, response);
        request.CopyTo(response);
        return 5;
    }

    private static int GetCommEventCounter(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 1) return BuildException(0x0B, 3, response);
        response[0] = 0x0B;
        BinaryPrimitives.WriteUInt16BigEndian(response[1..], 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(response[3..], 42);
        return 5;
    }

    private static int GetCommEventLog(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 1) return BuildException(0x0C, 3, response);
        response[0] = 0x0C;
        response[1] = 8; // status + event count + message count + two event bytes
        BinaryPrimitives.WriteUInt16BigEndian(response[2..], 0x0000);
        BinaryPrimitives.WriteUInt16BigEndian(response[4..], 42);
        BinaryPrimitives.WriteUInt16BigEndian(response[6..], 77);
        response[8] = 0x21;
        response[9] = 0x40;
        return 10;
    }

    private static int ReportServerId(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 1) return BuildException(0x11, 3, response);
        var additional = "ProxusTest"u8;
        response[0] = 0x11;
        response[1] = checked((byte)(2 + additional.Length));
        response[2] = 0x42;
        response[3] = 0xFF;
        additional.CopyTo(response[4..]);
        return 4 + additional.Length;
    }

    private int ReadFileRecord(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 9 || request[1] < 7 || request[1] != request.Length - 2 || request[1] % 7 != 0)
            return BuildException(0x14, 3, response);

        response[0] = 0x14;
        var requestOffset = 2;
        var responseOffset = 2;
        while (requestOffset < request.Length)
        {
            if (request[requestOffset] != 6) return BuildException(0x14, 3, response);
            var file = BinaryPrimitives.ReadUInt16BigEndian(request[(requestOffset + 1)..]);
            var record = BinaryPrimitives.ReadUInt16BigEndian(request[(requestOffset + 3)..]);
            var count = BinaryPrimitives.ReadUInt16BigEndian(request[(requestOffset + 5)..]);
            if (count == 0 || record > 9999 || (uint)record + count > 10000)
                return BuildException(0x14, 2, response);
            var subLength = checked(1 + count * 2);
            if (responseOffset + 1 + subLength > 253) return BuildException(0x14, 3, response);
            response[responseOffset++] = checked((byte)subLength);
            response[responseOffset++] = 6;
            for (var i = 0; i < count; i++)
                BinaryPrimitives.WriteUInt16BigEndian(response[(responseOffset + i * 2)..],
                    GetFileRecord(file, checked((ushort)(record + i))));
            responseOffset += count * 2;
            requestOffset += 7;
        }

        response[1] = checked((byte)(responseOffset - 2));
        return responseOffset;
    }

    private int WriteFileRecord(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 11 || request[1] < 9 || request[1] != request.Length - 2)
            return BuildException(0x15, 3, response);
        var offset = 2;
        while (offset < request.Length)
        {
            if (offset + 7 > request.Length || request[offset] != 6) return BuildException(0x15, 3, response);
            var file = BinaryPrimitives.ReadUInt16BigEndian(request[(offset + 1)..]);
            var record = BinaryPrimitives.ReadUInt16BigEndian(request[(offset + 3)..]);
            var count = BinaryPrimitives.ReadUInt16BigEndian(request[(offset + 5)..]);
            var bytes = count * 2;
            if (count == 0 || record > 9999 || (uint)record + count > 10000 || offset + 7 + bytes > request.Length)
                return BuildException(0x15, 2, response);
            for (var i = 0; i < count; i++)
                SetFileRecord(file, checked((ushort)(record + i)),
                    BinaryPrimitives.ReadUInt16BigEndian(request[(offset + 7 + i * 2)..]));
            offset += 7 + bytes;
        }

        if (offset != request.Length) return BuildException(0x15, 3, response);
        request.CopyTo(response);
        return request.Length;
    }

    private int ReadFifoQueue(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 3) return BuildException(0x18, 3, response);
        var pointer = BinaryPrimitives.ReadUInt16BigEndian(request[1..]);
        var count = _holding[pointer];
        if (count > 31 || (uint)pointer + count >= _holding.Length) return BuildException(0x18, 3, response);
        response[0] = 0x18;
        BinaryPrimitives.WriteUInt16BigEndian(response[1..], checked((ushort)(2 + count * 2)));
        BinaryPrimitives.WriteUInt16BigEndian(response[3..], count);
        for (var i = 0; i < count; i++)
            BinaryPrimitives.WriteUInt16BigEndian(response[(5 + i * 2)..], _holding[pointer + 1 + i]);
        return 5 + count * 2;
    }

    private static int UserDefinedEcho(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length < 1) return BuildException(0x41, 3, response);
        request.CopyTo(response);
        return request.Length;
    }

    private static int EncapsulatedInterfaceTransport(ReadOnlySpan<byte> request, Span<byte> response)
    {
        if (request.Length != 4 || request[1] != 0x0E) return BuildException(0x2B, 3, response);
        var readCode = request[2];
        var startObject = request[3];
        if (readCode is < 1 or > 4) return BuildException(0x2B, 3, response);

        var objects = new (byte Id, byte[] Value)[]
        {
            (0x00, [.. "Proxus Labs"u8]),
            (0x01, [.. "VirtualModbus"u8]),
            (0x02, [.. "3.0"u8]),
            (0x03, [.. "https://proxus.io"u8])
        };
        var selected = objects.Where(x => x.Id >= startObject && (readCode != 4 || x.Id == startObject)).ToArray();
        response[0] = 0x2B;
        response[1] = 0x0E;
        response[2] = readCode;
        response[3] = 0x83; // extended stream conformity, individual access supported
        response[4] = 0x00;
        response[5] = 0x00;
        response[6] = checked((byte)selected.Length);
        var offset = 7;
        foreach (var item in selected)
        {
            response[offset++] = item.Id;
            response[offset++] = checked((byte)item.Value.Length);
            item.Value.CopyTo(response[offset..]);
            offset += item.Value.Length;
        }

        return offset;
    }

    private void UpdateMaximum(int active)
    {
        while (true)
        {
            var current = Volatile.Read(ref _maximumConcurrentRequests);
            if (active <= current)
                return;
            if (Interlocked.CompareExchange(ref _maximumConcurrentRequests, active, current) == current)
                return;
        }
    }
}
