// Shared by every tests/*.SelfTests project through a linked <Compile> item. Loads the external
// golden-vector corpus (tests/corpus/fixtures, plus artifacts/golden-corpus/fixtures when the
// fetch-only captures have been fetched and extracted locally) and provides a loopback replay
// server that answers driver requests with recorded device responses.
//
// Fixture format: see tools/golden_corpus/extract_corpus.py.

using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Proxus.Drivers.GoldenCorpus;

internal sealed record CorpusPdu(
    string Capture,
    string Protocol,
    int Pair,
    char Direction,
    int Frame,
    string Stream,
    byte[] Bytes,
    IReadOnlyDictionary<string, string> Wireshark)
{
    public bool FromClient => Direction is 'C' or 'O';

    /// <summary>First value of a Wireshark field recorded for the frame that ends this PDU.</summary>
    public string? Ws(string key)
    {
        if (!Wireshark.TryGetValue(key, out var value))
            return null;
        var comma = value.IndexOf(',');
        return comma < 0 ? value : value[..comma];
    }

    public string[] WsAll(string key) =>
        Wireshark.TryGetValue(key, out var value) ? value.Split(',') : [];

    public string Location => $"{Capture} frame {Frame.ToString(CultureInfo.InvariantCulture)}";
}

internal sealed record CorpusCapture(
    string Id,
    string License,
    string Origin,
    bool Committed,
    IReadOnlyList<CorpusPdu> Pdus)
{
    /// <summary>Request/response pairs in capture order. Unpaired PDUs are excluded.</summary>
    public IEnumerable<(CorpusPdu Request, CorpusPdu Response)> Pairs()
    {
        var requests = new Dictionary<int, CorpusPdu>();
        foreach (var pdu in Pdus)
        {
            if (pdu.Pair < 0)
                continue;
            if (pdu.FromClient)
                requests[pdu.Pair] = pdu;
            else if (requests.Remove(pdu.Pair, out var request))
                yield return (request, pdu);
        }
    }
}

internal static class GoldenCorpusLoader
{
    public static string? RepositoryRoot { get; } = FindRoot();

    public static IReadOnlyList<CorpusCapture> Load(string protocol)
    {
        var captures = new List<CorpusCapture>();
        if (RepositoryRoot is null)
            return captures;
        foreach (var (directory, committed) in new[]
                 {
                     (Path.Combine(RepositoryRoot, "tests", "corpus", "fixtures"), true),
                     (Path.Combine(RepositoryRoot, "artifacts", "golden-corpus", "fixtures"), false)
                 })
        {
            if (!Directory.Exists(directory))
                continue;
            foreach (var file in Directory.EnumerateFiles(directory, "*.pdus").Order(StringComparer.Ordinal))
            {
                var capture = Parse(file, committed, protocol);
                if (capture.Pdus.Count > 0)
                    captures.Add(capture);
            }
        }
        return captures;
    }

    private static CorpusCapture Parse(string file, bool committed, string protocol)
    {
        string id = Path.GetFileNameWithoutExtension(file), license = "", origin = "";
        var pdus = new List<CorpusPdu>();
        foreach (var line in File.ReadLines(file))
        {
            if (line.StartsWith("# capture: ", StringComparison.Ordinal))
                id = line["# capture: ".Length..];
            else if (line.StartsWith("# license: ", StringComparison.Ordinal))
                license = line["# license: ".Length..];
            else if (line.StartsWith("# origin: ", StringComparison.Ordinal))
                origin = line["# origin: ".Length..];
            if (!line.StartsWith("P\t", StringComparison.Ordinal))
                continue;
            var parts = line.Split('\t');
            if (parts.Length < 8 || parts[1] != protocol)
                continue;
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            if (parts[7] != "-")
                foreach (var pair in parts[7].Split(';'))
                {
                    var equals = pair.IndexOf('=');
                    if (equals > 0)
                        fields[pair[..equals]] = pair[(equals + 1)..];
                }
            pdus.Add(new CorpusPdu(id, parts[1], int.Parse(parts[2], CultureInfo.InvariantCulture), parts[3][0],
                int.Parse(parts[4], CultureInfo.InvariantCulture), parts[5], Convert.FromHexString(parts[6]), fields));
        }
        return new CorpusCapture(id, license, origin, committed, pdus);
    }

    private static string? FindRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "tests", "corpus", "sources.json")))
                    return directory.FullName;
        }
        return null;
    }

    public static ulong ParseWsInteger(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : ulong.Parse(value, CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Outcome ledger for a corpus run. A case either parses with plausible values, or fails with a
/// structured DriverError. An escaped exception, a hang, or a disagreement with Wireshark's dissector
/// is a finding. Findings listed in <c>known</c> (documented defects) are reported but do not fail.
/// </summary>
internal sealed class CorpusLedger(string suite, IReadOnlySet<string> known)
{
    private readonly List<string> _findings = [];
    private readonly HashSet<string> _seenKnown = [];
    public int Parsed { get; private set; }
    public int StructuredErrors { get; private set; }
    public Dictionary<string, int> ErrorCodes { get; } = new(StringComparer.Ordinal);

    public void Success() => Parsed++;

    private static readonly bool sVerbose = Environment.GetEnvironmentVariable("CORPUS_VERBOSE") == "1";

    public void Structured(DriverError error, CorpusPdu? pdu = null)
    {
        StructuredErrors++;
        if (sVerbose)
            Console.WriteLine($"      [{suite}] {pdu?.Location ?? "-"}: {error.Code} {error.Message}");
        ErrorCodes[error.Code] = ErrorCodes.GetValueOrDefault(error.Code) + 1;
        if (string.IsNullOrWhiteSpace(error.Code) || string.IsNullOrWhiteSpace(error.Message))
            Finding("UNSTRUCTURED_ERROR", "-", $"DriverError without code or message: {error}");
    }

    /// <summary>Records a call outcome: success, or a structured error.</summary>
    public void Note(DriverError? error, CorpusPdu? pdu = null)
    {
        if (error is { } value)
            Structured(value, pdu);
        else
            Success();
    }

    public void Finding(string kind, string location, string detail)
    {
        var key = $"{kind}@{location}";
        if (known.Contains(key))
        {
            _seenKnown.Add(key);
            return;
        }
        _findings.Add($"{kind} at {location}: {detail}");
    }

    public void Complete(int captures)
    {
        var codes = string.Join(", ", ErrorCodes.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}"));
        Console.WriteLine($"      {suite}: {captures} captures, {Parsed} parsed, {StructuredErrors} structured errors" +
                          (codes.Length > 0 ? $" ({codes})" : "") +
                          (_seenKnown.Count > 0 ? $", {_seenKnown.Count} known findings" : ""));
        foreach (var key in _seenKnown.Order(StringComparer.Ordinal))
            Console.WriteLine($"      known finding {key}");
        if (_findings.Count > 0)
            throw new InvalidOperationException(
                $"{_findings.Count} corpus findings:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", _findings));
    }
}

/// <summary>
/// Loopback TCP server that frames each request with <c>frameLength</c> and answers it with the
/// bytes returned by <c>respond</c>. A null answer closes the connection, which the driver sees as
/// a transport fault; replay tests only issue requests whose semantics match a recording.
/// </summary>
internal sealed class ReplayTcpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Func<byte[], int> _frameLength;
    private readonly int _headerLength;
    private readonly Func<byte[], byte[]?> _respond;
    private readonly Task _accept;

    public ReplayTcpServer(int headerLength, Func<byte[], int> frameLength, Func<byte[], byte[]?> respond)
    {
        _headerLength = headerLength;
        _frameLength = frameLength;
        _respond = respond;
        _listener.Start();
        _accept = AcceptAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    public int Requests;
    public int Unmatched;

    private async Task AcceptAsync()
    {
        var sessions = new List<Task>();
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var socket = await _listener.AcceptSocketAsync(_stop.Token).ConfigureAwait(false);
                sessions.Add(ServeAsync(socket));
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
        await Task.WhenAll(sessions).ConfigureAwait(false);
    }

    private async Task ServeAsync(Socket socket)
    {
        using var _ = socket;
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var header = new byte[_headerLength];
                if (!await ReceiveAsync(socket, header).ConfigureAwait(false))
                    return;
                var total = _frameLength(header);
                if (total < _headerLength || total > 1 << 20)
                    return;
                var frame = new byte[total];
                header.CopyTo(frame, 0);
                if (!await ReceiveAsync(socket, frame.AsMemory(_headerLength)).ConfigureAwait(false))
                    return;
                Interlocked.Increment(ref Requests);
                var response = _respond(frame);
                if (response is null)
                {
                    Interlocked.Increment(ref Unmatched);
                    return;
                }
                if (response.Length > 0)
                    await socket.SendAsync(response, SocketFlags.None, _stop.Token).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
        }
    }

    private async Task<bool> ReceiveAsync(Socket socket, Memory<byte> buffer)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await socket.ReceiveAsync(buffer[offset..], SocketFlags.None, _stop.Token).ConfigureAwait(false);
            if (read == 0)
                return false;
            offset += read;
        }
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        try
        {
            await _accept.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
        }
        _stop.Dispose();
    }
}
