using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Proxus.Drivers.Modbus.SelfTests;

/// <summary>
///     Modbus ASCII golden vectors (tests/corpus/modbus-ascii), recorded from PyModbus 3.12.0 and proconX modpoll by
///     tools/modbus_ascii_record_vectors.py. For each vector the driver must send the recorded request characters
///     exactly, and decode the recorded server response to the value the independent master decoded.
/// </summary>
internal static class AsciiGoldenVectorTests
{
    public static async Task RunAsync()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "corpus", "modbus-ascii");
        var total = 0;
        foreach (var file in Directory.GetFiles(directory, "*.json").Order())
        {
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file).ConfigureAwait(false));
            foreach (var vector in document.RootElement.GetProperty("vectors").EnumerateArray())
            {
                var name = $"{Path.GetFileName(file)}: {vector.GetProperty("name").GetString()}";
                var operation = vector.GetProperty("operation");

                await using (var server = new InProcessModbusServer { AsciiFraming = true })
                {
                    server.Start();
                    await using var client = Client(server.Port);
                    var sent = await ExecuteAsync(client, operation).ConfigureAwait(false);
                    MiniAssert.True(sent.Ok, $"{name}: {sent.Error}");
                    MiniAssert.Equal(vector.GetProperty("request").GetString(), Encoding.ASCII.GetString(server.LastAsciiRequest),
                        $"{name}: request characters {Encoding.ASCII.GetString(server.LastAsciiRequest).TrimEnd()}");
                }

                var replayed = await ReplayAsync(operation, vector.GetProperty("response").GetString()!).ConfigureAwait(false);
                MiniAssert.True(replayed.Ok, $"{name}: replay {replayed.Error}");
                if (vector.TryGetProperty("decoded", out var decoded))
                    MiniAssert.Equal(string.Concat(decoded.GetRawText().Where(static c => !char.IsWhiteSpace(c))), replayed.Value, $"{name}: decoded value");
                total++;
            }
        }

        MiniAssert.True(total >= 20, $"only {total} vectors");
    }

    private static ModbusTcpClient Client(int port) => new(new ModbusTcpOptions
    {
        Host = "127.0.0.1", Port = port, UnitId = 1, Framing = ModbusFraming.Ascii,
        RequestTimeout = TimeSpan.FromSeconds(2)
    });

    /// <summary>Answers one request with the recorded response line, as the independent server did.</summary>
    private static async Task<(bool Ok, string? Value, string? Error)> ReplayAsync(JsonElement operation, string response)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync().ConfigureAwait(false);
            var one = new byte[1];
            do
                await socket.ReceiveAsync(one).ConfigureAwait(false);
            while (one[0] != (byte)'\n');
            await socket.SendAsync(Encoding.ASCII.GetBytes(response)).ConfigureAwait(false);
            await Task.Delay(200).ConfigureAwait(false);
        });
        await using var client = Client(((IPEndPoint)listener.LocalEndpoint).Port);
        var result = await ExecuteAsync(client, operation).ConfigureAwait(false);
        await peer.ConfigureAwait(false);
        return result;
    }

    private static async Task<(bool Ok, string? Value, string? Error)> ExecuteAsync(ModbusTcpClient client, JsonElement op)
    {
        var address = (ushort)op.GetProperty("address").GetInt32();
        switch (op.GetProperty("op").GetString())
        {
            case "read_coils" or "read_di":
            {
                var bits = new bool[op.GetProperty("count").GetInt32()];
                var read = op.GetProperty("op").GetString() == "read_coils"
                    ? await client.ReadCoilsAsync(address, bits).ConfigureAwait(false)
                    : await client.ReadDiscreteInputsAsync(address, bits).ConfigureAwait(false);
                return read.IsSuccess ? (true, JsonSerializer.Serialize(bits, AsciiVectorJson.Default.BooleanArray), null)
                    : (false, null, read.Error.Code);
            }
            case "read_hr" or "read_ir":
            {
                var registers = new ushort[op.GetProperty("count").GetInt32()];
                var read = op.GetProperty("op").GetString() == "read_hr"
                    ? await client.ReadHoldingRegistersAsync(address, registers).ConfigureAwait(false)
                    : await client.ReadInputRegistersAsync(address, registers).ConfigureAwait(false);
                return read.IsSuccess ? (true, JsonSerializer.Serialize(registers, AsciiVectorJson.Default.UInt16Array), null)
                    : (false, null, read.Error.Code);
            }
            case "write_coils":
            {
                var values = op.GetProperty("values").EnumerateArray().Select(static v => v.GetBoolean()).ToArray();
                var write = values.Length == 1
                    ? await client.WriteSingleCoilAsync(address, values[0]).ConfigureAwait(false)
                    : await client.WriteMultipleCoilsAsync(address, values).ConfigureAwait(false);
                return write.IsSuccess ? (true, null, null) : (false, null, write.Error.Code);
            }
            case "write_hr":
            {
                var values = op.GetProperty("values").EnumerateArray().Select(static v => (ushort)v.GetInt32()).ToArray();
                var write = values.Length == 1
                    ? await client.WriteSingleRegisterAsync(address, values[0]).ConfigureAwait(false)
                    : await client.WriteMultipleRegistersAsync(address, values).ConfigureAwait(false);
                return write.IsSuccess ? (true, null, null) : (false, null, write.Error.Code);
            }
            default:
                throw new InvalidOperationException($"unknown operation {op}");
        }
    }
}

[System.Text.Json.Serialization.JsonSerializable(typeof(bool[]))]
[System.Text.Json.Serialization.JsonSerializable(typeof(ushort[]))]
internal sealed partial class AsciiVectorJson : System.Text.Json.Serialization.JsonSerializerContext;
