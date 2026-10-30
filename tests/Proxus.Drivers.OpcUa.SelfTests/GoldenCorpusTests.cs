using System.Buffers.Binary;
using Proxus.Drivers.GoldenCorpus;

namespace Proxus.Drivers.OpcUa.SelfTests;

/// <summary>
/// External golden-vector tests (ADR 0005) for OPC UA Binary. The corpus holds open62541,
/// python-opcua and other third-party server responses (tests/corpus). Decoding: every SecurityPolicy
/// None response chunk goes through the driver's UA Binary readers (NodeId, ResponseHeader with
/// DiagnosticInfo, DataValue) and is compared with Wireshark. Replay: the real
/// <see cref="OpcUaTcpClient"/> runs HEL/ACK, OpenSecureChannel, GetEndpoints, CreateSession,
/// ActivateSession and Read against a server that answers with the recorded responses; only the
/// channel, token, sequence number, request ID and request handle are rewritten.
/// </summary>
internal static class GoldenCorpusTests
{
    // Findings documented in docs/VALIDATION_RESULTS.md (golden corpus section), keyed "KIND@capture frame N".
    private static readonly HashSet<string> sKnown = new(StringComparer.Ordinal)
    {
        // Correct rejection: the recorded Variant matrices declare more elements in ArrayDimensions than the
        // array carries; OPC 10000-6 5.2.2.16 requires a decoder to fail. Wireshark does not check this.
        "REJECTS_WIRESHARK_CLEAN@icsnpp-opcua-binary/open62541_read_service_test_data frame 35",
        "REJECTS_WIRESHARK_CLEAN@icsnpp-opcua-binary/open62541_read_service_test_data frame 95",
        "REJECTS_WIRESHARK_CLEAN@icsnpp-opcua-binary/open62541_read_service_test_data_with_history frame 35",
        "REJECTS_WIRESHARK_CLEAN@icsnpp-opcua-binary/open62541_read_service_test_data_with_history frame 95",
        // Decoding limit: the crafted icsnpp vector nests InnerDiagnosticInfo 40 levels deep; the driver's
        // 32-level recursion guard rejects it with a structured error.
        "REJECTS_WIRESHARK_CLEAN@icsnpp-opcua-binary/opcua_with_nested_diaginfo frame 4"
    };

    private readonly record struct Chunk(string Type, char Kind, uint ChannelId, string? Policy, uint TokenId, uint RequestId,
        int BodyOffset);

    public static Task DecodeAsync()
    {
        var captures = GoldenCorpusLoader.Load("opcua");
        if (captures.Count == 0)
            throw new InvalidOperationException("The committed OPC UA golden corpus is missing (tests/corpus/fixtures).");
        var ledger = new CorpusLedger("opcua decode", sKnown);
        int responses = 0, values = 0, secured = 0, partial = 0;
        foreach (var capture in captures)
        {
            var secure = capture.Pdus.Any(pdu => pdu.Bytes.Length > 12 && pdu.Bytes.AsSpan(0, 3).SequenceEqual("OPN"u8) &&
                                                 TryChunk(pdu.Bytes, out var c) && c.Policy is { } policy &&
                                                 policy != OpcUaProtocol.SecurityPolicyNone);
            foreach (var pdu in capture.Pdus.Where(pdu => !pdu.FromClient))
            {
                try
                {
                    var size = BinaryPrimitives.ReadUInt32LittleEndian(pdu.Bytes.AsSpan(4));
                    if (size != pdu.Bytes.Length)
                        ledger.Finding("SIZE_DIFFERS", pdu.Location, $"header {size}, chunk {pdu.Bytes.Length}");
                    if (pdu.Ws("transport.size") is { } wsSize && pdu.WsAll("transport.size").Length == 1 &&
                        GoldenCorpusLoader.ParseWsInteger(wsSize) != size)
                        ledger.Finding("WS_SIZE_DIFFERS", pdu.Location, $"driver {size}, Wireshark {wsSize}");
                    var type = System.Text.Encoding.ASCII.GetString(pdu.Bytes, 0, 3);
                    if (type == "ACK")
                    {
                        // OPC 10000-6 7.1.2.4: Acknowledge is exactly 28 bytes; the client enforces this.
                        if (pdu.Bytes.Length != 28)
                            ledger.Finding("ACK_LENGTH", pdu.Location, $"{pdu.Bytes.Length} bytes");
                        else
                            ledger.Success();
                        continue;
                    }
                    if (type is not ("OPN" or "MSG"))
                        continue;
                    if (!TryChunk(pdu.Bytes, out var chunk))
                    {
                        ledger.Finding("CHUNK_HEADER", pdu.Location, "security header could not be parsed");
                        continue;
                    }
                    if (secure && (type == "MSG" || chunk.Policy != OpcUaProtocol.SecurityPolicyNone))
                    {
                        secured++;
                        continue;
                    }
                    if (chunk.Kind != 'F')
                    {
                        partial++;
                        continue;
                    }
                    responses++;
                    values += DecodeBody(ledger, pdu, chunk);
                }
                catch (Exception ex)
                {
                    ledger.Finding("EXCEPTION", pdu.Location, ex.GetType().Name + ": " + ex.Message);
                }
            }
        }
        Console.WriteLine($"      opcua decode: {responses} None-policy responses, {values} DataValues, " +
                          $"{secured} signed/encrypted chunks skipped, {partial} intermediate chunks skipped");
        ledger.Complete(captures.Count);
        return Task.CompletedTask;
    }

    private static int DecodeBody(CorpusLedger ledger, CorpusPdu pdu, Chunk chunk)
    {
        var reader = new UaBinaryReader(pdu.Bytes.AsSpan(chunk.BodyOffset));
        try
        {
            var typeId = reader.ReadNodeId();
            if (typeId.Kind != IndustrialNodeIdKind.Numeric || typeId.NamespaceIndex != 0)
            {
                ledger.Finding("TYPE_ID", pdu.Location, $"non-standard response TypeId {typeId}");
                return 0;
            }
            if (pdu.WsAll("servicenodeid.numeric") is [var wsType] &&
                GoldenCorpusLoader.ParseWsInteger(wsType) != typeId.NumericIdentifier)
                ledger.Finding("WS_TYPE_DIFFERS", pdu.Location, $"driver {typeId.NumericIdentifier}, Wireshark {wsType}");
            var header = OpcUaTcpClient.ReadResponseHeader(ref reader);
            if (pdu.WsAll("RequestHandle") is [var wsHandle] && GoldenCorpusLoader.ParseWsInteger(wsHandle) != header.RequestHandle)
                ledger.Finding("WS_HANDLE_DIFFERS", pdu.Location, $"driver {header.RequestHandle}, Wireshark {wsHandle}");
            if (pdu.Ws("ServiceResult") is { } wsResult && GoldenCorpusLoader.ParseWsInteger(wsResult) != header.ServiceResult)
                ledger.Finding("WS_RESULT_DIFFERS", pdu.Location, $"driver 0x{header.ServiceResult:X8}, Wireshark {wsResult}");
            var values = 0;
            if (typeId.NumericIdentifier == OpcUaProtocol.ReadResponseBinary && (header.ServiceResult & 0x80000000u) == 0)
            {
                var count = reader.ReadInt32();
                if (count < -1 || count > reader.Remaining)
                    throw new InvalidDataException($"Invalid Read result count {count}.");
                for (var i = 0; i < count; i++)
                {
                    _ = OpcUaBinaryCodec.ReadDataValue(ref reader);
                    values++;
                }
            }
            ledger.Success();
            return values;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or NotSupportedException)
        {
            // The client maps these to structured UA_*_DECODE errors.
            ledger.Structured(new DriverError(DriverErrorKind.Protocol, "UA_DECODE", ex.Message), pdu);
            if (pdu.Ws("ws.malformed") is null)
                ledger.Finding("REJECTS_WIRESHARK_CLEAN", pdu.Location, ex.Message);
            return 0;
        }
    }

    public static async Task ReplayAsync()
    {
        var captures = GoldenCorpusLoader.Load("opcua");
        var ledger = new CorpusLedger("opcua replay", sKnown);
        int sessions = 0, reads = 0, endpoints = 0, skipped = 0, unmatched = 0;
        var anyEncodingDiffers = false;
        foreach (var capture in captures)
        {
            var pairs = capture.Pairs().ToArray();
            var ack = pairs.FirstOrDefault(pair => pair.Response.Bytes.AsSpan(0, 3).SequenceEqual("ACK"u8)).Response;
            var open = pairs.FirstOrDefault(pair => pair.Response.Bytes.AsSpan(0, 4).SequenceEqual("OPNF"u8) &&
                                                    TryChunk(pair.Response.Bytes, out var c) &&
                                                    c.Policy == OpcUaProtocol.SecurityPolicyNone &&
                                                    ServiceResult(pair.Response.Bytes, c) == 0).Response;
            if (ack is null || ack.Bytes.Length != 28 || open is null)
            {
                skipped++;
                continue;
            }
            TryChunk(open.Bytes, out var openChunk);
            var channelId = openChunk.ChannelId;
            var tokenId = OpenTokenId(open.Bytes, openChunk);
            var services = new List<(uint Service, byte[] Key, CorpusPdu Response)>();
            foreach (var (request, response) in pairs)
            {
                if (!request.Bytes.AsSpan(0, 4).SequenceEqual("MSGF"u8) || !response.Bytes.AsSpan(0, 4).SequenceEqual("MSGF"u8))
                    continue;
                if (!TryChunk(request.Bytes, out var q) || !TryChunk(response.Bytes, out _))
                    continue;
                var (service, key) = RequestKey(request.Bytes, q.BodyOffset);
                if (service != 0)
                    services.Add((service, key, response));
            }
            var sequence = 1000u;
            var encodingDiffers = 0;
            await using var server = new ReplayTcpServer(8, header => (int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)),
                request =>
                {
                    var kind = System.Text.Encoding.ASCII.GetString(request, 0, 3);
                    if (kind == "HEL")
                        return ack.Bytes;
                    if (kind == "CLO")
                        return [];
                    if (!TryChunk(request, out var q))
                        return null;
                    if (kind == "OPN")
                        return PatchResponse(open.Bytes, openChunk, channelId, tokenId, ++sequence, q.RequestId, RequestHandle(request, q, false));
                    var (service, key) = RequestKey(request, q.BodyOffset);
                    var semantic = service == OpcUaProtocol.ReadRequestBinary ? SemanticKey(key) : null;
                    foreach (var (recordedService, recordedKey, response) in services)
                        if (recordedService == service && (recordedKey.AsSpan().SequenceEqual(key) ||
                                                           (semantic is not null && semantic == SemanticKey(recordedKey))))
                        {
                            // NodeIds may legitimately use a different (compact or full) numeric encoding;
                            // GC-UA-2 is only about how the default DataEncoding name is encoded.
                            if (!DataEncodingNamesMatch(recordedKey, key))
                                Volatile.Write(ref encodingDiffers, 1);
                            TryChunk(response.Bytes, out var r);
                            return PatchResponse(response.Bytes, r, channelId, tokenId, ++sequence, q.RequestId,
                                RequestHandle(request, q, true));
                        }
                    if (Environment.GetEnvironmentVariable("CORPUS_VERBOSE") == "1")
                        Console.WriteLine($"      [opcua replay] unmatched service {service} key {Convert.ToHexString(key)}; recorded " +
                                          string.Join(" | ", services.Where(s => s.Service == service).Select(s => Convert.ToHexString(s.Key))));
                    return null;
                });
            var limits = BinaryPrimitives.ReadUInt32LittleEndian(ack.Bytes.AsSpan(12));
            var serverSend = BinaryPrimitives.ReadUInt32LittleEndian(ack.Bytes.AsSpan(16));
            await using var client = new OpcUaTcpClient(new OpcUaTcpOptions
            {
                EndpointUrl = new Uri($"opc.tcp://127.0.0.1:{server.Port}/"),
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(2),
                SendBufferSize = Math.Max(limits, 8192),
                ReceiveBufferSize = Math.Max(serverSend, 8192)
            });
            var connected = false;
            await GuardAsync(ledger, open, async token =>
            {
                var result = await client.ConnectAsync(token).ConfigureAwait(false);
                connected = result.IsSuccess;
                Classify(ledger, open, result.IsSuccess ? null : result.Error);
            }).ConfigureAwait(false);
            if (!connected)
                continue;

            var getEndpoints = services.FirstOrDefault(s => s.Service == OpcUaProtocol.GetEndpointsRequestBinary).Response;
            if (getEndpoints is not null)
            {
                endpoints++;
                await GuardAsync(ledger, getEndpoints, async token =>
                {
                    var result = await client.GetEndpointsAsync(cancellationToken: token).ConfigureAwait(false);
                    // An empty result is legitimate: open62541 answers with a null Endpoints array here.
                    Classify(ledger, getEndpoints, result.IsSuccess ? null : result.Error);
                }).ConfigureAwait(false);
            }

            var create = services.FirstOrDefault(s => s.Service == OpcUaProtocol.CreateSessionRequestBinary).Response;
            var activate = services.FirstOrDefault(s => s.Service == OpcUaProtocol.ActivateSessionRequestBinary).Response;
            if (create is null || activate is null)
                continue;
            sessions++;
            var active = false;
            await GuardAsync(ledger, create, async token =>
            {
                var result = await client.OpenAnonymousSessionAsync(token).ConfigureAwait(false);
                active = result.IsSuccess;
                Classify(ledger, create, result.IsSuccess ? null : result.Error);
            }).ConfigureAwait(false);
            if (!active)
                continue;
            foreach (var (service, key, response) in services.Where(s => s.Service == OpcUaProtocol.ReadRequestBinary))
            {
                var nodes = ReadNodes(key);
                if (nodes is null)
                    continue;
                reads++;
                var before = server.Unmatched;
                await GuardAsync(ledger, response, async token =>
                {
                    var samples = new DriverSample[nodes.Length];
                    var result = await client.ReadAsync(nodes, samples, token).ConfigureAwait(false);
                    Classify(ledger, response, result.IsSuccess ? null : result.Error);
                    if (result.IsSuccess && result.Value != nodes.Length)
                        ledger.Finding("READ_COUNT", response.Location, $"{result.Value} of {nodes.Length} values");
                }).ConfigureAwait(false);
                if (server.Unmatched != before)
                {
                    unmatched++;
                    ledger.Finding("REQUEST_ENCODING_DIFFERS", response.Location, "the client requested different NodesToRead");
                    break;
                }
            }
            if (Volatile.Read(ref encodingDiffers) != 0)
                anyEncodingDiffers = true;
        }
        if (anyEncodingDiffers)
            ledger.Finding("READVALUEID_DATAENCODING_EMPTY", "all captures",
                "the client encoded a ReadValueId.DataEncoding name differently from the recorded request (null vs empty QualifiedName name)");
        Console.WriteLine($"      opcua replay: {sessions} sessions, {endpoints} GetEndpoints, {reads} Read requests, " +
                          $"{skipped} captures without a None-policy handshake, {unmatched} unmatched");
        ledger.Complete(captures.Count);
    }

    /// <summary>
    /// Regression for the 2026-10-19 fuzz findings (GC-UA-3, GC-UA-4, GC-UA-5): every committed reproducer in
    /// tests/corpus/fuzz-findings is served at the step it targeted, inside a session built from the recorded
    /// open62541 handshake. Each call must complete with success or a structured error, throw nothing, and
    /// allocate a bounded amount.
    /// </summary>
    public static async Task FuzzReproducersAsync()
    {
        var root = GoldenCorpusLoader.RepositoryRoot ?? throw new InvalidOperationException("Repository root not found.");
        var directory = Path.Combine(root, "tests", "corpus", "fuzz-findings");
        var files = Directory.GetFiles(directory, "opcua-*.bin").Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0)
            throw new InvalidOperationException("No OPC UA fuzz reproducers found in tests/corpus/fuzz-findings.");
        var baseline = GoldenCorpusLoader.Load("opcua")
            .First(capture => capture.Id == "icsnpp-opcua-binary/open62541_client-server_minimal");
        byte[] ack = [], open = [];
        var recorded = new Dictionary<uint, byte[]>();
        foreach (var (request, response) in baseline.Pairs())
        {
            if (response.Bytes.AsSpan(0, 3).SequenceEqual("ACK"u8))
                ack = response.Bytes;
            else if (response.Bytes.AsSpan(0, 4).SequenceEqual("OPNF"u8))
                open = response.Bytes;
            else if (TryChunk(request.Bytes, out var q) && RequestKey(request.Bytes, q.BodyOffset).Service is var service and > 0)
                recorded.TryAdd(service, response.Bytes);
        }
        TryChunk(open, out var openChunk);
        var tokenId = OpenTokenId(open, openChunk);
        var ledger = new CorpusLedger("opcua fuzz reproducers", sKnown);
        foreach (var file in files)
        {
            var input = File.ReadAllBytes(file);
            var location = new CorpusPdu(Path.GetFileName(file), "opcua", -1, 'S', 0, "-", input,
                new Dictionary<string, string>());
            var before = GC.GetTotalAllocatedBytes(false);
            if (Path.GetFileName(file).StartsWith("opcua-codecs-", StringComparison.Ordinal))
                DecodeReproducer(ledger, location, input);
            else
                await ReplayReproducerAsync(ledger, location, input, ack, open, openChunk, tokenId, recorded).ConfigureAwait(false);
            var allocated = GC.GetTotalAllocatedBytes(false) - before;
            if (allocated > 64L * 1024 * 1024)
                ledger.Finding("UNBOUNDED_ALLOCATION", location.Location, $"{allocated / (1024 * 1024)} MB");
        }
        Console.WriteLine($"      opcua fuzz reproducers: {files.Length} inputs");
        ledger.Complete(files.Length);
    }

    /// <summary>Byte-level regressions for GC-UA-1, GC-UA-3 and GC-UA-5 (OPC 10000-6 5.2.2.16 and 5.2.5).</summary>
    public static Task CodecHardeningAsync()
    {
        // GC-UA-1: a DiagnosticInfo Variant (built-in 25) fails only its own node.
        byte[] results =
        [
            0x01, 25, 0x21, 0x07, 0, 0, 0, 0x00, 0x00, 0x34, 0x80, // DataValue{Variant DiagnosticInfo{SymbolicId=7, InnerStatus=0x80340000}}
            0x01, 6, 42, 0, 0, 0 // DataValue{Variant Int32 42}
        ];
        var reader = new UaBinaryReader(results);
        var first = OpcUaBinaryCodec.ReadDataValue(ref reader);
        var second = OpcUaBinaryCodec.ReadDataValue(ref reader);
        if (first.Quality.NativeStatusCode != OpcUaBinaryCodec.BadDataEncodingUnsupported || first.Value.Type != IndustrialDataType.Null)
            throw new InvalidOperationException($"DiagnosticInfo Variant: expected Bad_DataEncodingUnsupported, got {first.Quality}.");
        if (second.Value.AsInt32() != 42 || reader.Remaining != 0)
            throw new InvalidOperationException("The DataValue after a DiagnosticInfo Variant was not decoded.");

        // GC-UA-3: a 2^31-1 element array in a 10-byte payload is rejected before allocating.
        var allocatedBefore = GC.GetTotalAllocatedBytes(false);
        ExpectInvalidData([0x86, 0xFF, 0xFF, 0xFF, 0x7F, 1, 0, 0, 0, 0], "huge Int32 array");
        if (GC.GetTotalAllocatedBytes(false) - allocatedBefore > 1024 * 1024)
            throw new InvalidOperationException("A huge Variant array length allocated before validation.");

        // GC-UA-5: a Variant directly containing a Variant is a decoding error, not an ArgumentException.
        ExpectInvalidData([24, 24, 6, 1, 0, 0, 0], "Variant in Variant");

        // Recursion guard: arrays of Variant nested deeper than the limit.
        var nested = new List<byte>();
        for (var i = 0; i <= OpcUaBinaryCodec.MaximumVariantDepth + 1; i++)
            nested.AddRange(new byte[] { 0x80 | 24, 1, 0, 0, 0 });
        nested.Add(0);
        ExpectInvalidData(nested.ToArray(), "deep Variant nesting");
        return Task.CompletedTask;

        static void ExpectInvalidData(byte[] bytes, string what)
        {
            try
            {
                var variantReader = new UaBinaryReader(bytes);
                _ = OpcUaBinaryCodec.ReadVariant(ref variantReader);
            }
            catch (InvalidDataException)
            {
                return;
            }
            throw new InvalidOperationException($"{what}: expected InvalidDataException.");
        }
    }

    private static void DecodeReproducer(CorpusLedger ledger, CorpusPdu location, byte[] input)
    {
        try
        {
            // Same steps as the opcua-codecs fuzz target: headers, ResponseHeader, DataValues, then Variants.
            var reader = new UaBinaryReader(input.AsSpan(8));
            _ = reader.ReadUInt32();
            if (input.AsSpan(0, 3).SequenceEqual("OPN"u8))
            {
                _ = reader.ReadString();
                _ = reader.ReadByteString();
                _ = reader.ReadByteString();
            }
            else
                _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadNodeId();
            _ = OpcUaTcpClient.ReadResponseHeader(ref reader);
            var count = reader.ReadInt32();
            for (var i = 0; i < Math.Min(count, 256) && reader.Remaining > 0; i++)
                _ = OpcUaBinaryCodec.ReadDataValue(ref reader);
            var variants = 0;
            while (reader.Remaining > 0 && variants++ < 64)
                _ = OpcUaBinaryCodec.ReadVariant(ref reader);
            ledger.Success();
        }
        catch (Exception ex) when (OpcUaTcpClient.IsDecodeFailure(ex))
        {
            ledger.Structured(new DriverError(DriverErrorKind.Protocol, "UA_DECODE", ex.Message), location);
        }
        catch (Exception ex)
        {
            ledger.Finding("EXCEPTION", location.Location, ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static async Task ReplayReproducerAsync(CorpusLedger ledger, CorpusPdu location, byte[] input, byte[] ack,
        byte[] open, Chunk openChunk, uint tokenId, Dictionary<uint, byte[]> recorded)
    {
        var kind = input.Length >= 3 ? System.Text.Encoding.ASCII.GetString(input, 0, 3) : "MSG";
        var target = 0u;
        if (kind == "MSG" && input.Length >= 28)
        {
            try
            {
                var typeReader = new UaBinaryReader(input.AsSpan(24));
                var typeId = typeReader.ReadNodeId();
                target = typeId.Kind == IndustrialNodeIdKind.Numeric ? (uint)typeId.NumericIdentifier : 0;
            }
            catch (Exception ex) when (OpcUaTcpClient.IsDecodeFailure(ex))
            {
            }
        }
        var sequence = 5000u;
        var serveInput = false;
        await using var server = new ReplayTcpServer(8, header => (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4)), 1u << 20),
            request =>
            {
                var requestKind = System.Text.Encoding.ASCII.GetString(request, 0, Math.Min(3, request.Length));
                if (requestKind == "HEL")
                    return kind == "ACK" ? input : ack;
                if (requestKind == "CLO")
                    return [];
                if (!TryChunk(request, out var q))
                    return null;
                var useInput = Volatile.Read(ref serveInput) || kind == "OPN";
                if (requestKind == "OPN")
                    return kind == "OPN" ? PatchOrRaw(input, q, openChunk.ChannelId, tokenId, ++sequence, true, request)
                        : PatchResponse(open, openChunk, openChunk.ChannelId, tokenId, ++sequence, q.RequestId, RequestHandle(request, q, false));
                var (service, _) = RequestKey(request, q.BodyOffset);
                if (useInput && (service + 3 == target || target == 397 || target == 0))
                    return PatchOrRaw(input, q, openChunk.ChannelId, tokenId, ++sequence, false, request);
                if (!recorded.TryGetValue(service, out var response) || !TryChunk(response, out var r))
                    return null;
                return PatchResponse(response, r, openChunk.ChannelId, tokenId, ++sequence, q.RequestId, RequestHandle(request, q, true));
            });
        await using var client = new OpcUaTcpClient(new OpcUaTcpOptions
        {
            EndpointUrl = new Uri($"opc.tcp://127.0.0.1:{server.Port}/"),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            RequestTimeout = TimeSpan.FromSeconds(1)
        });
        await GuardAsync(ledger, location, async token =>
        {
            DriverError? error = null;
            var connect = await client.ConnectAsync(token).ConfigureAwait(false);
            if (!connect.IsSuccess || kind is "ACK" or "OPN")
            {
                Classify(ledger, location, connect.IsSuccess ? null : connect.Error);
                return;
            }
            if (target == OpcUaProtocol.GetEndpointsResponseBinary)
            {
                Volatile.Write(ref serveInput, true);
                var endpoints = await client.GetEndpointsAsync(cancellationToken: token).ConfigureAwait(false);
                ledger.Note(endpoints.IsSuccess ? null : endpoints.Error, location);
                return;
            }
            if (target is OpcUaProtocol.CreateSessionResponseBinary or OpcUaProtocol.ActivateSessionResponseBinary)
                Volatile.Write(ref serveInput, true);
            var session = await client.OpenAnonymousSessionAsync(token).ConfigureAwait(false);
            if (!session.IsSuccess || target is OpcUaProtocol.CreateSessionResponseBinary or OpcUaProtocol.ActivateSessionResponseBinary)
            {
                ledger.Note(session.IsSuccess ? null : session.Error, location);
                return;
            }
            Volatile.Write(ref serveInput, true);
            if (target == OpcUaProtocol.BrowseResponseBinary)
            {
                var browse = await client.BrowseAsync(new OpcUaBrowseDescription(IndustrialNodeId.Numeric(0, 85)),
                    cancellationToken: token).ConfigureAwait(false);
                error = browse.IsSuccess ? null : browse.Error;
            }
            else
            {
                var samples = new DriverSample[2];
                var read = await client.ReadAsync(
                    new[] { new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(0, 2258)), new OpcUaNodeDescriptor(IndustrialNodeId.Numeric(0, 2259)) },
                    samples, token).ConfigureAwait(false);
                error = read.IsSuccess ? null : read.Error;
            }
            ledger.Note(error, location);
        }).ConfigureAwait(false);
    }

    private static byte[] PatchOrRaw(byte[] input, Chunk request, uint channelId, uint tokenId, uint sequence, bool open,
        byte[] requestBytes)
    {
        if (!TryChunk(input, out var chunk) || chunk.Type != (open ? "OPN" : "MSG"))
            return input;
        try
        {
            return PatchResponse(input, chunk, channelId, tokenId, sequence, request.RequestId, RequestHandle(requestBytes, request, !open));
        }
        catch (Exception ex) when (OpcUaTcpClient.IsDecodeFailure(ex) || ex is ArgumentOutOfRangeException)
        {
            return input;
        }
    }

    private static void Classify(CorpusLedger ledger, CorpusPdu response, DriverError? error)
    {
        if (error is null)
        {
            ledger.Success();
            return;
        }
        var value = error.Value;
        ledger.Structured(value, response);
        if (value.Kind == DriverErrorKind.Protocol && response.Ws("ws.malformed") is null)
            ledger.Finding("REJECTS_WIRESHARK_CLEAN", response.Location, $"{value.Code}: {value.Message}");
    }

    private static bool TryChunk(byte[] bytes, out Chunk chunk)
    {
        chunk = default;
        try
        {
            if (bytes.Length < 12)
                return false;
            var type = System.Text.Encoding.ASCII.GetString(bytes, 0, 3);
            var reader = new UaBinaryReader(bytes.AsSpan(8));
            var channel = reader.ReadUInt32();
            if (type == "OPN")
            {
                var policy = reader.ReadString();
                _ = reader.ReadByteString();
                _ = reader.ReadByteString();
                _ = reader.ReadUInt32();
                var requestId = reader.ReadUInt32();
                chunk = new Chunk(type, (char)bytes[3], channel, policy, 0, requestId, 8 + reader.Position);
                return true;
            }
            if (type is "MSG" or "CLO")
            {
                var token = reader.ReadUInt32();
                _ = reader.ReadUInt32();
                var requestId = reader.ReadUInt32();
                chunk = new Chunk(type, (char)bytes[3], channel, null, token, requestId, 8 + reader.Position);
                return true;
            }
            return false;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            return false;
        }
    }

    private static uint ServiceResult(byte[] bytes, Chunk chunk)
    {
        try
        {
            var reader = new UaBinaryReader(bytes.AsSpan(chunk.BodyOffset));
            _ = reader.ReadNodeId();
            return OpcUaTcpClient.ReadResponseHeader(ref reader).ServiceResult;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException)
        {
            return uint.MaxValue;
        }
    }

    private static uint OpenTokenId(byte[] bytes, Chunk chunk)
    {
        var reader = new UaBinaryReader(bytes.AsSpan(chunk.BodyOffset));
        _ = reader.ReadNodeId();
        _ = OpcUaTcpClient.ReadResponseHeader(ref reader);
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        return reader.ReadUInt32();
    }

    private static uint RequestHandle(byte[] request, Chunk chunk, bool hasAuthenticationToken)
    {
        var reader = new UaBinaryReader(request.AsSpan(chunk.BodyOffset));
        _ = reader.ReadNodeId(); // request TypeId
        _ = reader.ReadNodeId(); // authentication token (null NodeId on OPN)
        _ = reader.ReadDateTime();
        _ = hasAuthenticationToken;
        return reader.ReadUInt32();
    }

    /// <summary>Request service TypeId and the semantic key: NodesToRead for Read, empty for session services.</summary>
    private static (uint Service, byte[] Key) RequestKey(byte[] request, int bodyOffset)
    {
        try
        {
            var reader = new UaBinaryReader(request.AsSpan(bodyOffset));
            var typeId = reader.ReadNodeId();
            if (typeId.Kind != IndustrialNodeIdKind.Numeric || typeId.NamespaceIndex != 0)
                return (0, []);
            var service = (uint)typeId.NumericIdentifier;
            // RequestHeader: authenticationToken, timestamp, requestHandle, returnDiagnostics, auditEntryId,
            // timeoutHint, additionalHeader.
            _ = reader.ReadNodeId();
            _ = reader.ReadDateTime();
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
            _ = reader.ReadString();
            _ = reader.ReadUInt32();
            _ = reader.ReadExtensionObject();
            if (service != OpcUaProtocol.ReadRequestBinary)
                return (service, []);
            _ = reader.ReadDouble(); // maxAge
            _ = reader.ReadUInt32(); // timestampsToReturn
            var start = bodyOffset + reader.Position;
            return (service, request.AsSpan(start).ToArray());
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or NotSupportedException)
        {
            return (0, []);
        }
    }

    /// <summary>NodesToRead with null and empty strings folded together (OPC UA treats both as default).</summary>
    private static string? SemanticKey(byte[] key) =>
        ReadNodes(key) is { } nodes
            ? string.Join(";", nodes.Select(node =>
                $"{node.NodeId}|{(uint)node.AttributeId}|{node.IndexRange ?? ""}|{node.DataEncoding.NamespaceIndex}:{node.DataEncoding.Name ?? ""}"))
            : null;

    /// <summary>
    /// GC-UA-2 regression: each ReadValueId.DataEncoding name must use the same null, empty or named form
    /// as the recorded request. Reference stacks send the default DataEncoding with a null name (-1).
    /// </summary>
    private static bool DataEncodingNamesMatch(byte[] recorded, byte[] actual)
    {
        static string[]? Forms(byte[] key)
        {
            try
            {
                var reader = new UaBinaryReader(key);
                var count = reader.ReadInt32();
                if (count is <= 0 or > 1000)
                    return null;
                var forms = new string[count];
                for (var i = 0; i < count; i++)
                {
                    _ = reader.ReadNodeId();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadString();
                    _ = reader.ReadUInt16();
                    var name = reader.ReadString();
                    forms[i] = name is null ? "null" : name.Length == 0 ? "empty" : "named";
                }
                return forms;
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or NotSupportedException)
            {
                return null;
            }
        }

        var expected = Forms(recorded);
        var observed = Forms(actual);
        // Keys of other services (for example GetEndpoints) carry no ReadValueIds; compare them byte for byte.
        if (expected is null || observed is null)
            return recorded.AsSpan().SequenceEqual(actual);
        return expected.AsSpan().SequenceEqual(observed);
    }

    private static OpcUaNodeDescriptor[]? ReadNodes(byte[] key)
    {
        try
        {
            var reader = new UaBinaryReader(key);
            var count = reader.ReadInt32();
            if (count <= 0 || count > 1000)
                return null;
            var nodes = new OpcUaNodeDescriptor[count];
            for (var i = 0; i < count; i++)
                nodes[i] = new OpcUaNodeDescriptor(reader.ReadNodeId(), (OpcUaAttributeId)reader.ReadUInt32(), reader.ReadString(),
                    reader.ReadQualifiedName());
            return reader.Remaining == 0 ? nodes : null;
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or NotSupportedException)
        {
            return null;
        }
    }

    private static byte[] PatchResponse(byte[] recorded, Chunk chunk, uint channelId, uint tokenId, uint sequence, uint requestId,
        uint requestHandle)
    {
        var answer = (byte[])recorded.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(answer.AsSpan(8), channelId);
        if (chunk.Type == "MSG")
            BinaryPrimitives.WriteUInt32LittleEndian(answer.AsSpan(12), tokenId);
        BinaryPrimitives.WriteUInt32LittleEndian(answer.AsSpan(chunk.BodyOffset - 8), sequence);
        BinaryPrimitives.WriteUInt32LittleEndian(answer.AsSpan(chunk.BodyOffset - 4), requestId);
        var reader = new UaBinaryReader(answer.AsSpan(chunk.BodyOffset));
        _ = reader.ReadNodeId();
        _ = reader.ReadDateTime();
        BinaryPrimitives.WriteUInt32LittleEndian(answer.AsSpan(chunk.BodyOffset + reader.Position), requestHandle);
        return answer;
    }

    private static async Task GuardAsync(CorpusLedger ledger, CorpusPdu pdu, Func<CancellationToken, Task> body)
    {
        using var hang = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var work = body(hang.Token);
            var finished = await Task.WhenAny(work, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
            if (finished != work)
                ledger.Finding("HANG", pdu.Location, "the client did not complete within 15 s");
            else
                await work.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ledger.Finding("EXCEPTION", pdu.Location, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
