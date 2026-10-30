using System.Buffers.Binary;
using Proxus.Drivers.GoldenCorpus;

namespace Proxus.Drivers.Modbus.SelfTests;

/// <summary>
/// External golden-vector tests (ADR 0005): every recorded Modbus/TCP response from the capture
/// corpus in tests/corpus is replayed to the real <see cref="ModbusTcpClient"/>. The replay server
/// answers a request only when its unit ID and PDU are byte-identical to a recorded request, so the
/// client runs its full request/response state machine against bytes produced by real devices.
/// </summary>
internal static class GoldenCorpusTests
{
    // Findings documented in docs/VALIDATION_RESULTS.md (golden corpus section), keyed "KIND@capture frame N".
    private static readonly HashSet<string> sKnown = new(StringComparer.Ordinal)
    {
        // Recording anomaly: the master pipelined two FC06 requests with the same transaction ID and
        // the reply echoes neither request; the driver's MODBUS_WRITE_ECHO rejection is correct.
        "REJECTS_WIRESHARK_CLEAN@iti/modbustcp/modbus/modbus_test frame 70",
        // Recording anomaly: Zeek's synthetic MEI reply carries conformity level 0x00 and More Follows
        // 0x4D, both outside the specification; the driver's MODBUS_MEI_HEADER rejection is correct.
        "REJECTS_WIRESHARK_CLEAN@zeek/modbus/modbus-eit frame 2"
    };

    public static async Task ReplayAsync()
    {
        var captures = GoldenCorpusLoader.Load("modbus");
        MiniAssert.True(captures.Count > 0, "The committed Modbus golden corpus is missing (tests/corpus/fixtures).");
        var ledger = new CorpusLedger("modbus replay", sKnown);
        var unmatched = 0;
        var notReplayable = 0;
        foreach (var capture in captures)
        {
            var pairs = capture.Pairs().Where(pair => pair.Request.Bytes.Length >= 8 && pair.Response.Bytes.Length >= 8)
                .ToArray();
            if (pairs.Length == 0)
                continue;
            await using var server = new ReplayTcpServer(7, header => 6 + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4)),
                request => Answer(pairs, request));
            await using var client = new ModbusTcpClient(new ModbusTcpOptions
            {
                Host = "127.0.0.1",
                Port = server.Port,
                ConnectTimeout = TimeSpan.FromSeconds(2),
                RequestTimeout = TimeSpan.FromSeconds(1)
            });
            foreach (var (request, response) in pairs)
            {
                var before = server.Unmatched;
                var outcome = await RunAsync(client, request, response, ledger).ConfigureAwait(false);
                if (outcome == Outcome.NotReplayable)
                    notReplayable++;
                else if (server.Unmatched != before)
                {
                    unmatched++;
                    ledger.Finding("REQUEST_ENCODING_DIFFERS", request.Location,
                        $"the client encoded function 0x{request.Bytes[7]:X2} differently from the recorded master");
                }
            }
        }
        Console.WriteLine($"      modbus replay: {notReplayable} recorded requests have no equivalent client call, {unmatched} unmatched");
        ledger.Complete(captures.Count);
    }

    /// <summary>
    /// Feeds every recorded server PDU (paired or not, any function) as the answer to a holding-register
    /// read. The driver must reject mismatches with a structured error and never throw or hang.
    /// </summary>
    public static async Task CrossFeedAsync()
    {
        var captures = GoldenCorpusLoader.Load("modbus");
        var responses = captures.SelectMany(capture => capture.Pdus).Where(pdu => !pdu.FromClient)
            .DistinctBy(pdu => Convert.ToHexString(pdu.Bytes.AsSpan(6))).ToArray();
        var ledger = new CorpusLedger("modbus cross-feed", sKnown);
        var current = 0;
        await using var server = new ReplayTcpServer(7, header => 6 + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4)),
            request =>
            {
                var recorded = responses[Volatile.Read(ref current)].Bytes;
                var answer = (byte[])recorded.Clone();
                answer[0] = request[0];
                answer[1] = request[1];
                answer[6] = request[6];
                return answer;
            });
        await using var client = new ModbusTcpClient(new ModbusTcpOptions
        {
            Host = "127.0.0.1",
            Port = server.Port,
            ConnectTimeout = TimeSpan.FromSeconds(2),
            RequestTimeout = TimeSpan.FromSeconds(1)
        });
        var destination = new ushort[125];
        for (var i = 0; i < responses.Length; i++)
        {
            Volatile.Write(ref current, i);
            var pdu = responses[i];
            var quantity = pdu.Bytes.Length > 8 && pdu.Bytes[7] is 3 or 4 ? Math.Max(1, pdu.Bytes[8] / 2) : 1;
            await GuardAsync(ledger, pdu, async token =>
            {
                var result = await client.ReadHoldingRegistersAsync(0, destination.AsMemory(0, Math.Min(quantity, 125)),
                    cancellationToken: token).ConfigureAwait(false);
                if (result.IsSuccess)
                {
                    if (pdu.Bytes[7] != 3)
                        ledger.Finding("ACCEPTED_WRONG_FUNCTION", pdu.Location,
                            $"function 0x{pdu.Bytes[7]:X2} accepted as a 0x03 response");
                    else
                        ledger.Success();
                }
                else
                    ledger.Structured(result.Error, pdu);
            }).ConfigureAwait(false);
        }
        ledger.Complete(captures.Count);
    }

    private enum Outcome
    {
        Done,
        NotReplayable
    }

    private static byte[]? Answer((CorpusPdu Request, CorpusPdu Response)[] pairs, byte[] request)
    {
        var key = request.AsSpan(6);
        foreach (var (recordedRequest, recordedResponse) in pairs)
        {
            if (!recordedRequest.Bytes.AsSpan(6).SequenceEqual(key))
                continue;
            var answer = (byte[])recordedResponse.Bytes.Clone();
            answer[0] = request[0];
            answer[1] = request[1];
            return answer;
        }
        return null;
    }

    private static async Task<Outcome> RunAsync(ModbusTcpClient client, CorpusPdu request, CorpusPdu response,
        CorpusLedger ledger)
    {
        var pdu = request.Bytes.AsMemory(7);
        var unit = request.Bytes[6];
        var function = pdu.Span[0];
        var body = pdu.Span[1..];
        var exception = response.Bytes[7] == (function | 0x80) ? response.Bytes[8] : (byte?)null;
        var bytes = request.Bytes;
        ushort U16(int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8 + offset));

        // Typed client calls for functions the driver models; everything else goes through the raw-PDU API.
        Func<CancellationToken, Task<DriverError?>> call;
        switch (function)
        {
            case 3 or 4 when body.Length == 4 && U16(2) is >= 1 and <= 125:
            {
                var start = U16(0);
                var values = new ushort[U16(2)];
                call = async token =>
                {
                    var result = function == 3
                        ? await client.ReadHoldingRegistersAsync(start, values, unit, token).ConfigureAwait(false)
                        : await client.ReadInputRegistersAsync(start, values, unit, token).ConfigureAwait(false);
                    if (!result.IsSuccess)
                        return result.Error;
                    CompareRegisters(ledger, response, values);
                    return null;
                };
                break;
            }
            case 1 or 2 when body.Length == 4 && U16(2) is >= 1 and <= 2000:
            {
                var start = U16(0);
                var values = new bool[U16(2)];
                call = async token =>
                {
                    var result = function == 1
                        ? await client.ReadCoilsAsync(start, values, unit, token).ConfigureAwait(false)
                        : await client.ReadDiscreteInputsAsync(start, values, unit, token).ConfigureAwait(false);
                    return result.IsSuccess ? null : result.Error;
                };
                break;
            }
            case 5 when body.Length == 4 && U16(2) is 0xFF00 or 0x0000:
            {
                var (address, value) = (U16(0), U16(2) == 0xFF00);
                call = async token => (await client.WriteSingleCoilAsync(address, value, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            case 6 when body.Length == 4:
            {
                var (address, value) = (U16(0), U16(2));
                call = async token => (await client.WriteSingleRegisterAsync(address, value, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            case 0x10 when body.Length >= 5 && body.Length == 5 + body[4] && body[4] == 2 * U16(2):
            {
                var start = U16(0);
                var values = new ushort[U16(2)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = BinaryPrimitives.ReadUInt16BigEndian(body[(5 + 2 * i)..]);
                call = async token => (await client.WriteMultipleRegistersAsync(start, values, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            case 0x0F when body.Length >= 5 && body.Length == 5 + body[4] && body[4] == (U16(2) + 7) / 8:
            {
                var start = U16(0);
                var values = new bool[U16(2)];
                for (var i = 0; i < values.Length; i++)
                    values[i] = (body[5 + i / 8] & (1 << (i % 8))) != 0;
                call = async token => (await client.WriteMultipleCoilsAsync(start, values, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            case 0x16 when body.Length == 6:
            {
                var (address, and, or) = (U16(0), U16(2), U16(4));
                call = async token => (await client.MaskWriteRegisterAsync(address, and, or, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            case 0x2B when body.Length == 3 && body[0] == 0x0E && body[1] is >= 1 and <= 4:
            {
                var (code, objectId) = ((ModbusDeviceIdReadCode)body[1], body[2]);
                call = async token =>
                {
                    var result = await client.ReadDeviceIdentificationAsync(code, objectId, unit, token).ConfigureAwait(false);
                    return result.IsSuccess ? null : result.Error;
                };
                break;
            }
            case 0x11 when body.Length == 0:
                call = async token =>
                {
                    var result = await client.ReportServerIdAsync(unit, token).ConfigureAwait(false);
                    if (!result.IsSuccess)
                        return result.Error;
                    // GC-MB-1 regression: the whole device-specific payload is returned, and the one-byte
                    // Server ID layout is claimed only when the second byte is a Run Indicator.
                    var payload = response.Bytes.AsSpan(9);
                    var id = result.Value;
                    if (!id.Data.AsSpan().SequenceEqual(payload) ||
                        id.HasOneByteServerIdLayout != (payload.Length >= 2 && payload[1] is 0x00 or 0xFF))
                        ledger.Finding("SERVER_ID_DIFFERS", response.Location, "FC17 payload or layout misreported");
                    return null;
                };
                break;
            case 0x07 when body.Length == 0:
                call = async token => (await client.ReadExceptionStatusAsync(unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            case 0x08 when body.Length == 4:
            {
                var (sub, data) = (U16(0), U16(2));
                call = async token => (await client.DiagnosticsRawAsync(sub, data, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
            default:
            {
                if (pdu.Length is 0 or > 253 || function is 0 or >= 0x80)
                    return Outcome.NotReplayable;
                var raw = pdu.ToArray();
                var destination = new byte[253];
                call = async token => (await client.ExecuteRawPduAsync(raw, destination, unit, token).ConfigureAwait(false)) is
                    { IsSuccess: false } r ? r.Error : null;
                break;
            }
        }

        var replayable = true;
        await GuardAsync(ledger, response, async token =>
        {
            var error = await call(token).ConfigureAwait(false);
            if (error is null)
            {
                if (exception is { } code)
                    ledger.Finding("EXCEPTION_ACCEPTED", response.Location, $"exception 0x{code:X2} reported as success");
                else
                    ledger.Success();
                return;
            }
            var value = error.Value;
            if (value.Kind == DriverErrorKind.Configuration && value.Operation == DriverOperation.Validate)
            {
                replayable = false;
                return;
            }
            ledger.Structured(value, response);
            if (exception is null && value.Kind == DriverErrorKind.Protocol && response.Ws("ws.malformed") is null)
                ledger.Finding("REJECTS_WIRESHARK_CLEAN", response.Location,
                    $"{value.Code}: {value.Message} (Wireshark decodes the frame without a malformed flag)");
            if (exception is { } expected)
            {
                var wireshark = response.Ws("exception_code");
                if (value.Code != "MODBUS_EXCEPTION" || value.NativeCode != expected ||
                    (wireshark is not null && (int)GoldenCorpusLoader.ParseWsInteger(wireshark) != expected))
                    ledger.Finding("EXCEPTION_MISMATCH", response.Location,
                        $"driver {value.Code}/{value.NativeCode}, wire 0x{expected:X2}, Wireshark {wireshark}");
            }
        }).ConfigureAwait(false);
        return replayable ? Outcome.Done : Outcome.NotReplayable;
    }

    private static void CompareRegisters(CorpusLedger ledger, CorpusPdu response, ushort[] values)
    {
        // Wireshark aggregates fields per frame; compare only frames that carry exactly this ADU.
        if (response.WsAll("trans_id").Length != 1)
            return;
        var wireshark = response.WsAll("regval_uint16");
        if (wireshark.Length == 0)
            return;
        if (wireshark.Length != values.Length)
        {
            ledger.Finding("REGISTER_COUNT_DIFFERS", response.Location,
                $"driver decoded {values.Length} registers, Wireshark {wireshark.Length}");
            return;
        }
        for (var i = 0; i < values.Length; i++)
            if ((ulong)values[i] != GoldenCorpusLoader.ParseWsInteger(wireshark[i]))
            {
                ledger.Finding("REGISTER_VALUE_DIFFERS", response.Location,
                    $"register {i}: driver {values[i]}, Wireshark {wireshark[i]}");
                return;
            }
    }

    internal static async Task GuardAsync(CorpusLedger ledger, CorpusPdu pdu, Func<CancellationToken, Task> body)
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
