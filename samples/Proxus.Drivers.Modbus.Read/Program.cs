using Proxus.Drivers.Modbus;

// "--rtu" or "--ascii" selects Modbus RTU or Modbus ASCII framing, for a serial-to-Ethernet gateway in
// transparent mode.
var framing = args.Contains("--rtu") ? ModbusFraming.Rtu : args.Contains("--ascii") ? ModbusFraming.Ascii : ModbusFraming.Mbap;
args = args.Where(static argument => argument is not ("--rtu" or "--ascii")).ToArray();

if (args.Length is < 1 or > 3 ||
    (args.Length >= 2 && !int.TryParse(args[1], out _)))
{
    Console.Error.WriteLine("Usage: Proxus.Drivers.Modbus.Read <host> [port] [address] [--rtu | --ascii]");
    return 2;
}

var host = args[0];
var port = args.Length >= 2 ? int.Parse(args[1]) : 502;
var addressText = args.Length == 3 ? args[2] : "hr:0";
var compiled = ModbusAddress.Parse(addressText);
if (!compiled.IsSuccess)
{
    Console.Error.WriteLine(compiled.Error.ToUserMessage());
    return 3;
}

if (compiled.Value.ObjectType != ModbusObjectType.HoldingRegister)
{
    Console.Error.WriteLine("This read-only quickstart expects a holding-register address such as hr:0.");
    return 4;
}

await using var client = new ModbusTcpClient(new ModbusTcpOptions
{
    Host = host,
    Port = port,
    UnitId = compiled.Value.UnitId,
    Framing = framing,
    ConnectTimeout = TimeSpan.FromSeconds(5),
    RequestTimeout = TimeSpan.FromSeconds(5)
});

var connected = await client.ConnectAsync();
if (!connected.IsSuccess)
{
    Console.Error.WriteLine(connected.Error.ToUserMessage());
    return 5;
}

var registers = new ushort[1];
var read = await client.ReadHoldingRegistersAsync(compiled.Value.Offset, registers);
if (!read.IsSuccess)
{
    Console.Error.WriteLine(read.Error.ToUserMessage());
    return 6;
}

Console.WriteLine($"{compiled.Value} = {registers[0]} (0x{registers[0]:X4})");
var disconnected = await client.DisconnectAsync();
if (!disconnected.IsSuccess)
{
    Console.Error.WriteLine(disconnected.Error.ToUserMessage());
    return 7;
}

return 0;
