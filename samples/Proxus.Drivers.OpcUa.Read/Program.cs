using Proxus.Drivers;
using Proxus.Drivers.OpcUa;

if (args.Length is < 1 or > 3 || (args.Length >= 2 && !ushort.TryParse(args[1], out _)))
{
    Console.Error.WriteLine("Usage: Proxus.Drivers.OpcUa.Read <endpoint> [namespace-index] [string-node-id]");
    return 2;
}

var endpoint = new Uri(args[0]);
var namespaceIndex = args.Length >= 2 ? ushort.Parse(args[1]) : (ushort)0;
var nodeId = args.Length == 3
    ? IndustrialNodeId.String(namespaceIndex, args[2])
    : IndustrialNodeId.Numeric(0, 2258); // ServerStatus.CurrentTime

await using var client = new OpcUaTcpClient(new OpcUaTcpOptions
{
    EndpointUrl = endpoint,
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(10)
});

var connected = await client.ConnectAsync();
if (!connected.IsSuccess) return Fail("CONNECT", connected.Error);
var session = await client.OpenAnonymousSessionAsync();
if (!session.IsSuccess) return Fail("SESSION", session.Error);
var read = await client.ReadAsync(new OpcUaNodeDescriptor(nodeId));
if (!read.IsSuccess) return Fail("READ", read.Error);
Console.WriteLine($"{nodeId} = {read.Value.Value}; quality={read.Value.Quality}; source={read.Value.SourceTimestampUtc:O}");
return 0;

static int Fail(string stage, DriverError error)
{
    Console.Error.WriteLine($"{stage}: {error.ToUserMessage()}");
    return 3;
}
