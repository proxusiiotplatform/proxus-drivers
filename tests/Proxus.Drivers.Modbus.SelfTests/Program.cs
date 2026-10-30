using System.Buffers.Binary;
using System.Text;
using Proxus.Drivers;
using Proxus.Drivers.Modbus;
using Proxus.Drivers.Modbus.SelfTests;

var tests = new (string Name, Func<Task> Run)[]
{
    ("Address parser", AddressParserAsync),
    ("Descriptor validation", DescriptorValidationAsync),
    ("Scalar codec", ScalarCodecAsync),
    ("DriverValue equality/hash contract", DriverValueEqualityAsync),
    ("Array codec", ArrayCodecAsync),
    ("Compiled read planner", CompiledReadPlannerAsync),
    ("Compiled write planner", CompiledWritePlannerAsync),
    ("Compiled nested record read/write", CompiledRecordAsync),
    ("Projected multidimensional packed layout", ProjectedPackedLayoutAsync),
    ("Large typed array chunking", LargeTypedArrayChunkingAsync),
    ("Array type matrix", ArrayTypeMatrixAsync),
    ("String and bytes codec", StringAndBytesCodecAsync),
    ("Temporal codec", TemporalCodecAsync),
    ("Enumeration storage matrix", EnumerationCodecAsync),
    ("Packed BCD decimal codec", PackedBcdDecimalAsync),
    ("Deterministic randomized codec", RandomizedCodecAsync),
    ("Register and bit I/O", RegisterAndBitIoAsync),
    ("Advanced Modbus functions", AdvancedFunctionsAsync),
    ("Application service matrix", ApplicationServicesAsync),
    ("Protocol enum contracts", ProtocolEnumContractsAsync),
    ("Lifecycle and capability contract", LifecycleAndCapabilitiesAsync),
    ("Typed scalar I/O", TypedScalarIoAsync),
    ("Typed array I/O", TypedArrayIoAsync),
    ("Byte lane preservation", ByteLanePreservationAsync),
    ("Fragmented TCP response", FragmentedResponseAsync),
    ("Concurrent callers serialize", ConcurrentCallersAsync),
    ("Queued cancellation is structured", QueuedCancellationAsync),
    ("Cancellation closes and reconnects", CancellationAndReconnectAsync),
    ("Timeout closes and reconnects", TimeoutAndReconnectAsync),
    ("Modbus exception keeps connection usable", ExceptionResponseAsync),
    ("Diagnostics native-code matrix", DiagnosticsMatrixAsync),
    ("Protocol mismatch closes and reconnects", ProtocolMismatchAsync),
    ("Protocol fault matrix", ProtocolFaultMatrixAsync),
    ("Corrupt byte count closes connection", CorruptByteCountAsync),
    ("Address overflow rejected before I/O", AddressOverflowAsync),
    ("Dispose interrupts infinite request", DisposeInterruptsRequestAsync),
    ("Raw register and coil failures carry operation context", RawFailureContextAsync),
    ("Refused connection carries reconnect disposition", RefusedConnectionDispositionAsync),
    ("Invalid options are structured", InvalidOptionsAsync),
    ("RTU: frame bytes match the specification example", RtuFrameKnownAnswerAsync),
    ("RTU: register and bit I/O", () => OverRtuAsync(RegisterAndBitIoAsync)),
    ("RTU: advanced functions", () => OverRtuAsync(AdvancedFunctionsAsync)),
    ("RTU: application services", () => OverRtuAsync(ApplicationServicesAsync)),
    ("RTU: typed scalar I/O", () => OverRtuAsync(TypedScalarIoAsync)),
    ("RTU: typed array I/O", () => OverRtuAsync(TypedArrayIoAsync)),
    ("RTU: fragmented response", () => OverRtuAsync(FragmentedResponseAsync)),
    ("RTU: exception keeps connection usable", () => OverRtuAsync(ExceptionResponseAsync)),
    ("RTU: timeout closes and reconnects", () => OverRtuAsync(TimeoutAndReconnectAsync)),
    ("RTU: CRC, Unit ID and length faults are structured", RtuFaultsAsync),
    ("RTU: caller-supplied stream transport", RtuStreamTransportAsync),
    ("RTU: option validation", RtuOptionValidationAsync),
    ("ASCII: frame characters and LRC match hand-computed examples", AsciiFrameKnownAnswerAsync),
    ("ASCII: register and bit I/O", () => OverAsciiAsync(RegisterAndBitIoAsync)),
    ("ASCII: advanced functions and a custom function", () => OverAsciiAsync(AdvancedFunctionsAsync)),
    ("ASCII: application services", () => OverAsciiAsync(ApplicationServicesAsync)),
    ("ASCII: typed scalar and array I/O", () => OverAsciiAsync(async () => { await TypedScalarIoAsync(); await TypedArrayIoAsync(); })),
    ("ASCII: fragmented response", () => OverAsciiAsync(FragmentedResponseAsync)),
    ("ASCII: exception keeps connection usable", () => OverAsciiAsync(ExceptionResponseAsync)),
    ("ASCII: timeout closes and reconnects", () => OverAsciiAsync(TimeoutAndReconnectAsync)),
    ("ASCII: LRC, Unit ID, character and delimiter faults are structured", AsciiFaultsAsync),
    ("ASCII: caller-supplied stream transport and option validation", AsciiStreamAndOptionsAsync),
    ("ASCII: frames and decoding match PyModbus and modpoll golden vectors", AsciiGoldenVectorTests.RunAsync),
    ("Golden corpus: real-device response replay", GoldenCorpusTests.ReplayAsync),
    ("Golden corpus: cross-fed responses stay structured", GoldenCorpusTests.CrossFeedAsync)
};

var failures = 0;
foreach (var test in tests)
    try
    {
        await test.Run().ConfigureAwait(false);
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {ex}");
    }

Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static Task AddressParserAsync()
{
    var explicitAddress = ModbusAddress.Parse("unit=7;hr:42");
    MiniAssert.True(explicitAddress.IsSuccess);
    MiniAssert.Equal((byte)7, explicitAddress.Value.UnitId);
    MiniAssert.Equal((ushort)42, explicitAddress.Value.Offset);
    MiniAssert.Equal(ModbusObjectType.HoldingRegister, explicitAddress.Value.ObjectType);

    var reference = ModbusAddress.Parse("40001");
    MiniAssert.True(reference.IsSuccess);
    MiniAssert.Equal((ushort)0, reference.Value.Offset);
    MiniAssert.Equal(ModbusObjectType.HoldingRegister, reference.Value.ObjectType);

    var maximum = ModbusAddress.Parse("465536");
    MiniAssert.True(maximum.IsSuccess);
    MiniAssert.Equal(ushort.MaxValue, maximum.Value.Offset);

    MiniAssert.False(ModbusAddress.Parse("465537").IsSuccess);
    MiniAssert.False(ModbusAddress.Parse("bad").IsSuccess);
    return Task.CompletedTask;
}

static Task DescriptorValidationAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);

    var oddUtf16 = new ModbusRegisterDescriptor(
        address,
        IndustrialDataType.String,
        ByteLength: 3,
        StringEncoding: ModbusStringEncoding.Utf16LittleEndian);
    MiniAssert.False(oddUtf16.ValidateScalar().IsSuccess);

    var stringArray = new ModbusRegisterDescriptor(
        address,
        IndustrialDataType.String,
        2,
        8);
    MiniAssert.True(stringArray.ValidateArray().IsSuccess);

    var badBit = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.Coil, 0),
        IndustrialDataType.Int16);
    MiniAssert.False(badBit.ValidateScalar().IsSuccess);

    var dateWithoutFormat = new ModbusRegisterDescriptor(address, IndustrialDataType.DateTime);
    MiniAssert.False(dateWithoutFormat.ValidateScalar().IsSuccess);

    var zeroBoolTrue = new ModbusRegisterDescriptor(
        address,
        IndustrialDataType.Bool,
        RegisterBoolTrueValue: 0);
    MiniAssert.False(zeroBoolTrue.ValidateScalar().IsSuccess);

    var pollutedCoil = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.Coil, 0),
        IndustrialDataType.Bool,
        Layout: ModbusRegisterLayout.ByteSwapped);
    MiniAssert.False(pollutedCoil.ValidateScalar().IsSuccess);

    var maxValue = DriverValue.FromUInt64(ulong.MaxValue);
    var sameMaxValue = DriverValue.FromUInt64(ulong.MaxValue);
    MiniAssert.True(maxValue == sameMaxValue);
    var bytes = DriverValue.FromBytes([1, 2, 3]);
    var sameBytes = DriverValue.FromBytes([1, 2, 3]);
    MiniAssert.True(bytes == sameBytes);
    MiniAssert.False(DriverValue.FromBytes([1, 2, 3]) == DriverValue.FromBytes([1, 2, 4]));

    return Task.CompletedTask;
}

static Task ScalarCodecAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    var layouts = new[]
    {
        ModbusRegisterLayout.Standard,
        ModbusRegisterLayout.ByteSwapped,
        ModbusRegisterLayout.WordSwapped,
        ModbusRegisterLayout.LittleEndian
    };

    foreach (var layout in layouts)
    {
        RoundTrip(
            new ModbusRegisterDescriptor(address, IndustrialDataType.Bool, Layout: layout),
            DriverValue.FromBoolean(true));

        RoundTrip(
            new ModbusRegisterDescriptor(
                address,
                IndustrialDataType.Int8,
                Layout: layout,
                ByteLane: ModbusByteLane.Low,
                ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            DriverValue.FromInt8(-12));

        RoundTrip(
            new ModbusRegisterDescriptor(
                address,
                IndustrialDataType.UInt8,
                Layout: layout,
                ByteLane: ModbusByteLane.High,
                ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            DriverValue.FromUInt8(245));

        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.Int16, Layout: layout),
            DriverValue.FromInt16(-12345));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.UInt16, Layout: layout),
            DriverValue.FromUInt16(54321));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.Int32, Layout: layout),
            DriverValue.FromInt32(-123456789));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.UInt32, Layout: layout),
            DriverValue.FromUInt32(3_456_789_012));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.Int64, Layout: layout),
            DriverValue.FromInt64(-8_123_456_789_012_345));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.UInt64, Layout: layout),
            DriverValue.FromUInt64(17_123_456_789_012_345_678));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.Float32, Layout: layout),
            DriverValue.FromFloat32(-1234.25f));
        RoundTrip(new ModbusRegisterDescriptor(address, IndustrialDataType.Float64, Layout: layout),
            DriverValue.FromFloat64(-123456.75));
    }

    return Task.CompletedTask;
}


static Task DriverValueEqualityAsync()
{
    var a = DriverValue.FromUInt64(12_345_678_901_234UL);
    var b = DriverValue.FromUInt64(12_345_678_901_234UL);
    var c = DriverValue.FromUInt64(12_345_678_901_235UL);
    MiniAssert.True(a == b);
    MiniAssert.True(a != c);
    MiniAssert.Equal(a.GetHashCode(), b.GetHashCode());

    var set = new HashSet<DriverValue> { a };
    MiniAssert.True(set.Contains(b));
    MiniAssert.False(set.Contains(c));
    return Task.CompletedTask;
}

static Task ArrayCodecAsync()
{
    var descriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 0),
        IndustrialDataType.Float32,
        3,
        Layout: ModbusRegisterLayout.WordSwapped);

    var values = new[]
    {
        DriverValue.FromFloat32(1.25f),
        DriverValue.FromFloat32(-2.5f),
        DriverValue.FromFloat32(1000.125f)
    };

    Span<ushort> registers = stackalloc ushort[6];
    var encoded = ModbusDataCodec.EncodeMany(descriptor, values, registers);
    MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
    MiniAssert.Equal(6, encoded.Value);

    var decoded = new DriverValue[3];
    var decode = ModbusDataCodec.DecodeMany(descriptor, registers, decoded);
    MiniAssert.True(decode.IsSuccess, decode.Error.ToString());
    MiniAssert.NearlyEqual(1.25, decoded[0].AsFloat32(), 0);
    MiniAssert.NearlyEqual(-2.5, decoded[1].AsFloat32(), 0);
    MiniAssert.NearlyEqual(1000.125, decoded[2].AsFloat32(), 0);
    return Task.CompletedTask;
}

static Task ArrayTypeMatrixAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    var g1 = new Guid("00112233-4455-6677-8899-aabbccddeeff");
    var g2 = new Guid("ffeeddcc-bbaa-9988-7766-554433221100");
    var cases = new (ModbusRegisterDescriptor Descriptor, DriverValue[] Values)[]
    {
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Bool, 3),
            [DriverValue.FromBoolean(true), DriverValue.FromBoolean(false), DriverValue.FromBoolean(true)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Int8, 3, ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            [DriverValue.FromInt8(-1), DriverValue.FromInt8(0), DriverValue.FromInt8(127)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.UInt8, 3, ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            [DriverValue.FromUInt8(0), DriverValue.FromUInt8(128), DriverValue.FromUInt8(255)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Int16, 3),
            [DriverValue.FromInt16(short.MinValue), DriverValue.FromInt16(-1), DriverValue.FromInt16(short.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.UInt16, 3),
            [DriverValue.FromUInt16(0), DriverValue.FromUInt16(32768), DriverValue.FromUInt16(ushort.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Int32, 3),
            [DriverValue.FromInt32(int.MinValue), DriverValue.FromInt32(-1), DriverValue.FromInt32(int.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.UInt32, 3),
            [DriverValue.FromUInt32(0), DriverValue.FromUInt32(2_147_483_648), DriverValue.FromUInt32(uint.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Int64, 3),
            [DriverValue.FromInt64(long.MinValue), DriverValue.FromInt64(-1), DriverValue.FromInt64(long.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.UInt64, 3),
            [DriverValue.FromUInt64(0), DriverValue.FromUInt64(1UL << 63), DriverValue.FromUInt64(ulong.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Int128, 2),
            [DriverValue.FromInt128(Int128.MinValue + 7), DriverValue.FromInt128(Int128.MaxValue - 9)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.UInt128, 2),
            [DriverValue.FromUInt128(1), DriverValue.FromUInt128(UInt128.MaxValue - 9)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Float16, 3),
        [
            DriverValue.FromFloat16((Half)(-1.5f)), DriverValue.FromFloat16((Half)0),
            DriverValue.FromFloat16((Half)123.25f)
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Float32, 3),
            [DriverValue.FromFloat32(-1.5f), DriverValue.FromFloat32(0), DriverValue.FromFloat32(12345.25f)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Float64, 3),
        [
            DriverValue.FromFloat64(-Math.PI), DriverValue.FromFloat64(0), DriverValue.FromFloat64(double.MaxValue / 2)
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Decimal, 3, DecimalFormat: ModbusDecimalFormat.ScaledInt64, DecimalScale: 3),
            [DriverValue.FromDecimal(-12345.678m), DriverValue.FromDecimal(0m), DriverValue.FromDecimal(98765.432m)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Char8, 3, ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            [DriverValue.FromChar8('A'), DriverValue.FromChar8('z'), DriverValue.FromChar8('0')]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Char16, 3),
            [DriverValue.FromChar16('A'), DriverValue.FromChar16('Ω'), DriverValue.FromChar16('Ж')]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Guid, 2, GuidFormat: ModbusGuidFormat.Rfc4122NetworkOrder),
            [DriverValue.FromGuid(g1), DriverValue.FromGuid(g2)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Date, 3, TemporalFormat: ModbusTemporalFormat.DateDaysSince1990UInt16),
        [
            DriverValue.FromDate(new DateOnly(1990, 1, 1)), DriverValue.FromDate(new DateOnly(2026, 8, 10)),
            DriverValue.FromDate(new DateOnly(2099, 12, 31))
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.TimeOfDay, 3, TemporalFormat: ModbusTemporalFormat.TimeOfDayMillisecondsUInt32),
        [
            DriverValue.FromTimeOfDay(new TimeOnly(0, 0)), DriverValue.FromTimeOfDay(new TimeOnly(12, 34, 56, 789)),
            DriverValue.FromTimeOfDay(new TimeOnly(23, 59, 59, 999))
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.DateTime, 3, TemporalFormat: ModbusTemporalFormat.UnixMillisecondsInt64),
        [
            DriverValue.FromDateTime(DateTime.UnixEpoch), DriverValue.FromDateTime(DateTime.UnixEpoch.AddDays(1)),
            DriverValue.FromDateTime(DateTime.UnixEpoch.AddYears(10))
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Duration, 3, TemporalFormat: ModbusTemporalFormat.DurationMillisecondsInt64),
        [
            DriverValue.FromDuration(TimeSpan.Zero), DriverValue.FromDuration(TimeSpan.FromMilliseconds(-1)),
            DriverValue.FromDuration(TimeSpan.FromDays(42))
        ]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.BitString8, 3, ByteWriteBehavior: ModbusByteWriteBehavior.ZeroOtherLane),
            [DriverValue.FromBitString8(0), DriverValue.FromBitString8(0xA5), DriverValue.FromBitString8(0xFF)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.BitString16, 3),
            [DriverValue.FromBitString16(0), DriverValue.FromBitString16(0xA55A), DriverValue.FromBitString16(0xFFFF)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.BitString32, 2),
            [DriverValue.FromBitString32(0xA55AF00D), DriverValue.FromBitString32(0xFFFFFFFF)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.BitString64, 2),
            [DriverValue.FromBitString64(0xA55AF00DDEADBEEF), DriverValue.FromBitString64(ulong.MaxValue)]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.String, 2, 8),
            [DriverValue.FromString("alpha"), DriverValue.FromString("beta")]),
        (new ModbusRegisterDescriptor(address, IndustrialDataType.Bytes, 2, 4),
            [DriverValue.FromBytes([1, 2, 3, 4]), DriverValue.FromBytes([5, 6, 7, 8])])
    };
    Span<ushort> registers = stackalloc ushort[128];
    foreach (var c in cases)
    {
        var count = c.Descriptor.GetRequiredRegisterCount();
        MiniAssert.True(count.IsSuccess, count.Error.ToUserMessage());
        registers[..count.Value].Clear();
        var enc = ModbusDataCodec.EncodeMany(c.Descriptor, c.Values, registers[..count.Value]);
        MiniAssert.True(enc.IsSuccess, $"{c.Descriptor.DataType}: {enc.Error.ToUserMessage()}");
        var decoded = new DriverValue[c.Values.Length];
        var dec = ModbusDataCodec.DecodeMany(c.Descriptor, registers[..enc.Value], decoded);
        MiniAssert.True(dec.IsSuccess, $"{c.Descriptor.DataType}: {dec.Error.ToUserMessage()}");
        for (var i = 0; i < c.Values.Length; i++) MiniAssert.Equal(c.Values[i], decoded[i]);
    }

    return Task.CompletedTask;
}

static async Task CompiledReadPlannerAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(0, unchecked((ushort)-1234));
    var i32 = unchecked((uint)-1234567);
    server.SetHolding(1, (ushort)(i32 >> 16));
    server.SetHolding(2, (ushort)(i32 & ushort.MaxValue));
    var f = unchecked((uint)BitConverter.SingleToInt32Bits(42.25f));
    server.SetHolding(3, (ushort)(f >> 16));
    server.SetHolding(4, (ushort)f);
    for (ushort i = 0; i < 10; i++) server.SetCoil(i, (i & 1) == 0);
    server.Start();
    await using var client = CreateClient(server.Port);
    MiniAssert.True((await client.ConnectAsync()).IsSuccess);
    var items = new[]
    {
        new ModbusReadPlanItem(0,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 0),
                IndustrialDataType.Int16)),
        new ModbusReadPlanItem(1,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 1),
                IndustrialDataType.Int32)),
        new ModbusReadPlanItem(2,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 3),
                IndustrialDataType.Float32)),
        new ModbusReadPlanItem(3,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.Coil, 0), IndustrialDataType.Bool, 10))
    };
    var plan = ModbusReadPlanner.Compile(items,
        new ModbusReadPlannerOptions { MaximumGapRegisters = 0, MaximumGapBits = 0 });
    MiniAssert.True(plan.IsSuccess, plan.Error.ToUserMessage());
    MiniAssert.Equal(2, plan.Value.Blocks.Length);
    var before = server.RequestCount;
    var values = new DriverValue[13];
    var read = await client.ReadPlanAsync(plan.Value, values);
    MiniAssert.True(read.IsSuccess, read.Error.ToUserMessage());
    MiniAssert.Equal(13, read.Value);
    MiniAssert.Equal(2, server.RequestCount - before);
    MiniAssert.Equal(-1234, values[0].AsInt16());
    MiniAssert.Equal(-1234567, values[1].AsInt32());
    MiniAssert.NearlyEqual(42.25, values[2].AsFloat32(), 0);
    for (var i = 0; i < 10; i++) MiniAssert.Equal((i & 1) == 0, values[3 + i].AsBoolean());
    var sparse = ModbusReadPlanner.Compile(
        new[]
        {
            new ModbusReadPlanItem(0,
                new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 0),
                    IndustrialDataType.UInt16)),
            new ModbusReadPlanItem(1,
                new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 100),
                    IndustrialDataType.UInt16))
        }, new ModbusReadPlannerOptions { MaximumGapRegisters = 2 });
    MiniAssert.True(sparse.IsSuccess);
    MiniAssert.Equal(2, sparse.Value.Blocks.Length);
    var overlap = ModbusReadPlanner.Compile(new[]
    {
        new ModbusReadPlanItem(0,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 0),
                IndustrialDataType.UInt16)),
        new ModbusReadPlanItem(0,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 1),
                IndustrialDataType.UInt16))
    });
    MiniAssert.False(overlap.IsSuccess);
    MiniAssert.Equal("MODBUS_PLAN_DESTINATION_OVERLAP", overlap.Error.Code);
}

static async Task CompiledWritePlannerAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);
    MiniAssert.True((await client.ConnectAsync()).IsSuccess);
    var items = new[]
    {
        new ModbusWritePlanItem(0,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 0),
                IndustrialDataType.UInt16)),
        new ModbusWritePlanItem(1,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 1),
                IndustrialDataType.Int32)),
        new ModbusWritePlanItem(2,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.Coil, 0), IndustrialDataType.Bool, 4))
    };
    var plan = ModbusWritePlanner.Compile(items);
    MiniAssert.True(plan.IsSuccess, plan.Error.ToUserMessage());
    MiniAssert.Equal(2, plan.Value.Blocks.Length);
    var values = new[]
    {
        DriverValue.FromUInt16(0x1234), DriverValue.FromInt32(-1234567), DriverValue.FromBoolean(true),
        DriverValue.FromBoolean(false), DriverValue.FromBoolean(true), DriverValue.FromBoolean(true)
    };
    var before = server.RequestCount;
    var write = await client.WritePlanAsync(plan.Value, values);
    MiniAssert.True(write.IsSuccess, write.Error.ToUserMessage());
    MiniAssert.Equal(6, write.Value);
    MiniAssert.Equal(2, server.RequestCount - before);
    var regs = new ushort[3];
    MiniAssert.True((await client.ReadHoldingRegistersAsync(0, regs)).IsSuccess);
    MiniAssert.Equal((ushort)0x1234, regs[0]);
    var bits = new bool[4];
    MiniAssert.True((await client.ReadCoilsAsync(0, bits)).IsSuccess);
    MiniAssert.True(bits[0]);
    MiniAssert.False(bits[1]);
    MiniAssert.True(bits[2]);
    MiniAssert.True(bits[3]);
    var viaBatch = await client.WriteBatchAsync(
        new[] { new DriverWriteBatchItem<ModbusRegisterDescriptor>(0, items[0].Descriptor) },
        new[] { DriverValue.FromUInt16(0x4321) });
    MiniAssert.True(viaBatch.IsSuccess, viaBatch.Error.ToUserMessage());
}


static async Task CompiledRecordAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(100, 7);
    var tempBits = unchecked((uint)BitConverter.SingleToInt32Bits(12.5f));
    server.SetHolding(101, (ushort)(tempBits >> 16));
    server.SetHolding(102, (ushort)tempBits);
    server.SetHolding(103, 11);
    server.SetHolding(104, 22);
    server.SetHolding(105, 33);
    server.Start();
    await using var client = CreateClient(server.Port);
    MiniAssert.True((await client.ConnectAsync()).IsSuccess);

    var schemaResult = DriverRecordSchema<ModbusRegisterDescriptor>.Create([
        new DriverRecordField<ModbusRegisterDescriptor>("Status", 0, 1,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 100),
                IndustrialDataType.UInt16)),
        new DriverRecordField<ModbusRegisterDescriptor>("Temperature", 1, 1,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 101),
                IndustrialDataType.Float32)),
        new DriverRecordField<ModbusRegisterDescriptor>("Counts", 2, 3,
            new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 103),
                IndustrialDataType.UInt16, 3))
    ]);
    MiniAssert.True(schemaResult.IsSuccess, schemaResult.Error.ToUserMessage());
    var readRecord =
        ModbusRecordCompiler.CompileRead(schemaResult.Value, new ModbusReadPlannerOptions { MaximumGapRegisters = 0 });
    var writeRecord = ModbusRecordCompiler.CompileWrite(schemaResult.Value);
    MiniAssert.True(readRecord.IsSuccess, readRecord.Error.ToUserMessage());
    MiniAssert.True(writeRecord.IsSuccess, writeRecord.Error.ToUserMessage());
    MiniAssert.Equal(1, readRecord.Value.Statistics.BlockCount);
    MiniAssert.Equal(6, readRecord.Value.Statistics.PhysicalUnitCount);
    MiniAssert.Equal(5, readRecord.Value.Statistics.ValueCount);
    MiniAssert.Equal(1, writeRecord.Value.Statistics.BlockCount);

    var values = new DriverValue[5];
    var before = server.RequestCount;
    var read = await client.ReadRecordAsync(readRecord.Value, values);
    MiniAssert.True(read.IsSuccess, read.Error.ToUserMessage());
    MiniAssert.Equal(5, read.Value);
    MiniAssert.Equal(1, server.RequestCount - before);
    MiniAssert.Equal((ushort)7, values[0].AsUInt16());
    MiniAssert.NearlyEqual(12.5, values[1].AsFloat32(), 0);
    MiniAssert.Equal((ushort)33, values[4].AsUInt16());

    var next = new[]
    {
        DriverValue.FromUInt16(9), DriverValue.FromFloat32(33.25f),
        DriverValue.FromUInt16(44), DriverValue.FromUInt16(55), DriverValue.FromUInt16(66)
    };
    before = server.RequestCount;
    var write = await client.WriteRecordAsync(writeRecord.Value, next);
    MiniAssert.True(write.IsSuccess, write.Error.ToUserMessage());
    MiniAssert.Equal(5, write.Value);
    MiniAssert.Equal(1, server.RequestCount - before);
    MiniAssert.Equal((ushort)9, server.GetHolding(100));
    MiniAssert.Equal((ushort)44, server.GetHolding(103));
    MiniAssert.Equal((ushort)66, server.GetHolding(105));
}


static async Task ProjectedPackedLayoutAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(201, 0xBEEF); // explicit padding register must survive writes
    server.Start();
    await using var client = CreateClient(server.Port);
    MiniAssert.True((await client.ConnectAsync()).IsSuccess);

    var shape = DriverArrayShape.Create([2, 3]).Value;
    var layout = new DriverPackedLayoutBuilder()
        .AddField("State", IndustrialDataType.UInt16)
        .Align(4)
        .AddField("Counter", IndustrialDataType.UInt32)
        .AddArrayField("Matrix", IndustrialDataType.Int16, shape)
        .Build();
    MiniAssert.True(layout.IsSuccess, layout.Error.ToUserMessage());
    MiniAssert.Equal(20, layout.Value.ByteLength);
    MiniAssert.Equal(2, layout.Value.PaddingByteCount);

    var schema = layout.Value.Project(static field => new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, checked((ushort)(200 + field.ByteOffset / 2))),
        field.DataType,
        checked((ushort)field.ValueCount)));
    MiniAssert.True(schema.IsSuccess, schema.Error.ToUserMessage());
    var read = ModbusRecordCompiler.CompileRead(schema.Value, new ModbusReadPlannerOptions { MaximumGapRegisters = 1 });
    var write = ModbusRecordCompiler.CompileWrite(schema.Value);
    MiniAssert.True(read.IsSuccess, read.Error.ToUserMessage());
    MiniAssert.True(write.IsSuccess, write.Error.ToUserMessage());

    var source = new[]
    {
        DriverValue.FromUInt16(9), DriverValue.FromUInt32(0x11223344),
        DriverValue.FromInt16(-1), DriverValue.FromInt16(2), DriverValue.FromInt16(-3),
        DriverValue.FromInt16(4), DriverValue.FromInt16(-5), DriverValue.FromInt16(6)
    };
    var wr = await client.WriteRecordAsync(write.Value, source);
    MiniAssert.True(wr.IsSuccess, wr.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0xBEEF, server.GetHolding(201));

    var destination = new DriverValue[source.Length];
    var rr = await client.ReadRecordAsync(read.Value, destination);
    MiniAssert.True(rr.IsSuccess, rr.Error.ToUserMessage());
    for (var i = 0; i < source.Length; i++) MiniAssert.Equal(source[i], destination[i]);
}

static async Task LargeTypedArrayChunkingAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);
    var descriptor = new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 100),
        IndustrialDataType.Int32, 300);
    var values = Enumerable.Range(0, 300).Select(i => DriverValue.FromInt32(unchecked(i * 7919 - 1234567))).ToArray();
    var before = server.RequestCount;
    var write = await client.WriteValuesAsync(descriptor, values);
    MiniAssert.True(write.IsSuccess, write.Error.ToUserMessage());
    var writeRequests = server.RequestCount - before;
    MiniAssert.True(writeRequests >= 5);
    var dest = new DriverValue[300];
    before = server.RequestCount;
    var read = await client.ReadValuesAsync(descriptor, dest);
    MiniAssert.True(read.IsSuccess, read.Error.ToUserMessage());
    var readRequests = server.RequestCount - before;
    MiniAssert.True(readRequests >= 5);
    for (var i = 0; i < values.Length; i++) MiniAssert.Equal(values[i], dest[i]);
    var plan = ModbusReadPlanner.Compile(new[] { new ModbusReadPlanItem(0, descriptor) });
    MiniAssert.True(plan.IsSuccess, plan.Error.ToUserMessage());
    MiniAssert.True(plan.Value.Blocks.Length >= 5);
    var planned = new DriverValue[300];
    var pr = await client.ReadPlanAsync(plan.Value, planned);
    MiniAssert.True(pr.IsSuccess, pr.Error.ToUserMessage());
    for (var i = 0; i < values.Length; i++) MiniAssert.Equal(values[i], planned[i]);

    var coilDescriptor =
        new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.Coil, 1000), IndustrialDataType.Bool, 5000);
    var coilValues = Enumerable.Range(0, 5000).Select(i => DriverValue.FromBoolean(i % 7 is 0 or 3)).ToArray();
    MiniAssert.True((await client.WriteValuesAsync(coilDescriptor, coilValues)).IsSuccess);
    var coilDest = new DriverValue[5000];
    var cr = await client.ReadValuesAsync(coilDescriptor, coilDest);
    MiniAssert.True(cr.IsSuccess, cr.Error.ToUserMessage());
    for (var i = 0; i < coilValues.Length; i++) MiniAssert.Equal(coilValues[i], coilDest[i]);
}

static Task StringAndBytesCodecAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    Span<ushort> registers = stackalloc ushort[16];

    foreach (var descriptor in new[]
             {
                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 10,
                     StringEncoding: ModbusStringEncoding.Ascii,
                     StringTermination: ModbusStringTermination.NullTerminated),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 16,
                     StringEncoding: ModbusStringEncoding.Utf8,
                     StringTermination: ModbusStringTermination.NullTerminated),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 20,
                     StringEncoding: ModbusStringEncoding.Utf16LittleEndian,
                     StringTermination: ModbusStringTermination.NullTerminated),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 20,
                     StringEncoding: ModbusStringEncoding.Utf16BigEndian,
                     StringTermination: ModbusStringTermination.NullTerminated),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 10,
                     StringEncoding: ModbusStringEncoding.Ascii,
                     StringTermination: ModbusStringTermination.SpacePadded),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 20,
                     StringEncoding: ModbusStringEncoding.Utf16BigEndian,
                     StringTermination: ModbusStringTermination.SpacePadded),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 17,
                     StringEncoding: ModbusStringEncoding.Utf8,
                     StringTermination: ModbusStringTermination.ByteLengthPrefixed8),

                 new ModbusRegisterDescriptor(
                     address,
                     IndustrialDataType.String,
                     ByteLength: 22,
                     StringEncoding: ModbusStringEncoding.Utf16LittleEndian,
                     StringTermination: ModbusStringTermination.ByteLengthPrefixed16BigEndian)
             })
    {
        var text = descriptor.StringEncoding == ModbusStringEncoding.Ascii ? "Proxus" : "Proxus Ş";
        var encoded = ModbusDataCodec.Encode(descriptor, DriverValue.FromString(text), registers);
        MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
        var decoded = ModbusDataCodec.Decode(descriptor, registers[..encoded.Value]);
        MiniAssert.True(decoded.IsSuccess, decoded.Error.ToString());
        MiniAssert.Equal(text, decoded.Value.AsString());
    }

    var ascii = new ModbusRegisterDescriptor(
        address,
        IndustrialDataType.String,
        ByteLength: 8,
        StringEncoding: ModbusStringEncoding.Ascii);
    MiniAssert.False(ModbusDataCodec.Encode(ascii, DriverValue.FromString("Ş"), registers).IsSuccess);

    var badPrefix = new ModbusRegisterDescriptor(address, IndustrialDataType.String, ByteLength: 6,
        StringEncoding: ModbusStringEncoding.Ascii, StringTermination: ModbusStringTermination.ByteLengthPrefixed8);
    registers[0] = 0x0641; // length=6, but only five payload bytes are available
    registers[1] = 0x4243;
    registers[2] = 0x4445;
    var badPrefixDecode = ModbusDataCodec.Decode(badPrefix, registers[..3]);
    MiniAssert.False(badPrefixDecode.IsSuccess);

    var invalidUtf16Framing = new ModbusRegisterDescriptor(address, IndustrialDataType.String, ByteLength: 10,
        StringEncoding: ModbusStringEncoding.Utf16BigEndian,
        StringTermination: ModbusStringTermination.ByteLengthPrefixed8);
    MiniAssert.False(invalidUtf16Framing.ValidateScalar()
        .IsSuccess); // 9 payload bytes cannot hold whole UTF-16 code units

    var bytesDescriptor = new ModbusRegisterDescriptor(address, IndustrialDataType.Bytes, ByteLength: 5);
    var source = new byte[] { 0, 1, 2, 0xFE, 0xFF };
    var bytesEncoded = ModbusDataCodec.Encode(bytesDescriptor, DriverValue.FromBytes(source), registers);
    MiniAssert.True(bytesEncoded.IsSuccess, bytesEncoded.Error.ToString());
    var bytesDecoded = ModbusDataCodec.Decode(bytesDescriptor, registers[..bytesEncoded.Value]);
    MiniAssert.True(bytesDecoded.IsSuccess, bytesDecoded.Error.ToString());
    MiniAssert.SequenceEqual(source, bytesDecoded.Value.AsBytes().Span);
    return Task.CompletedTask;
}

static Task TemporalCodecAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    Span<ushort> registers = stackalloc ushort[4];
    var instant = new DateTime(2024, 12, 31, 23, 59, 58, DateTimeKind.Utc);

    var dateTimeFormats = new[]
    {
        ModbusTemporalFormat.UnixSecondsInt32,
        ModbusTemporalFormat.UnixSecondsUInt32,
        ModbusTemporalFormat.UnixSecondsInt64,
        ModbusTemporalFormat.UnixMillisecondsInt64,
        ModbusTemporalFormat.DotNetTicksInt64
    };
    for (var i = 0; i < dateTimeFormats.Length; i++)
    {
        var format = dateTimeFormats[i];
        var descriptor = new ModbusRegisterDescriptor(
            address,
            IndustrialDataType.DateTime,
            TemporalFormat: format);
        var encoded = ModbusDataCodec.Encode(descriptor, DriverValue.FromDateTime(instant), registers);
        MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
        var decoded = ModbusDataCodec.Decode(descriptor, registers[..encoded.Value]);
        MiniAssert.True(decoded.IsSuccess, decoded.Error.ToString());
        MiniAssert.Equal(instant, decoded.Value.AsDateTime());
    }

    var duration = TimeSpan.FromMilliseconds(123_456);
    var durationFormats = new[]
    {
        ModbusTemporalFormat.DurationMillisecondsInt32,
        ModbusTemporalFormat.DurationMillisecondsInt64,
        ModbusTemporalFormat.DurationTicksInt64
    };
    for (var i = 0; i < durationFormats.Length; i++)
    {
        var format = durationFormats[i];
        var descriptor = new ModbusRegisterDescriptor(
            address,
            IndustrialDataType.Duration,
            TemporalFormat: format);
        var encoded = ModbusDataCodec.Encode(descriptor, DriverValue.FromDuration(duration), registers);
        MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
        var decoded = ModbusDataCodec.Decode(descriptor, registers[..encoded.Value]);
        MiniAssert.True(decoded.IsSuccess, decoded.Error.ToString());
        MiniAssert.Equal(duration, decoded.Value.AsDuration());
    }

    return Task.CompletedTask;
}


static Task EnumerationCodecAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    var cases = new (IndustrialDataType Storage, DriverValue Value)[]
    {
        (IndustrialDataType.Int8, DriverValue.FromEnumeration(-100)),
        (IndustrialDataType.UInt8, DriverValue.FromUnsignedEnumeration(250)),
        (IndustrialDataType.Int16, DriverValue.FromEnumeration(short.MinValue + 7)),
        (IndustrialDataType.UInt16, DriverValue.FromUnsignedEnumeration(ushort.MaxValue - 7)),
        (IndustrialDataType.Int32, DriverValue.FromEnumeration(-1_234_567_890)),
        (IndustrialDataType.UInt32, DriverValue.FromUnsignedEnumeration(4_000_000_000)),
        (IndustrialDataType.Int64, DriverValue.FromEnumeration(long.MinValue + 123)),
        (IndustrialDataType.UInt64, DriverValue.FromUnsignedEnumeration(ulong.MaxValue - 123))
    };

    foreach (var item in cases)
    {
        var descriptor = new ModbusRegisterDescriptor(address, IndustrialDataType.Enumeration,
            ByteLane: item.Storage is IndustrialDataType.Int8 or IndustrialDataType.UInt8
                ? ModbusByteLane.High
                : ModbusByteLane.Low,
            ByteWriteBehavior: item.Storage is IndustrialDataType.Int8 or IndustrialDataType.UInt8
                ? ModbusByteWriteBehavior.ZeroOtherLane
                : ModbusByteWriteBehavior.Reject,
            EnumerationStorageType: item.Storage);
        RoundTrip(descriptor, item.Value);
    }

    var arrayDescriptor = new ModbusRegisterDescriptor(address, IndustrialDataType.Enumeration, 4,
        EnumerationStorageType: IndustrialDataType.UInt16);
    var source = new[]
    {
        DriverValue.FromUnsignedEnumeration(0), DriverValue.FromUnsignedEnumeration(1),
        DriverValue.FromUnsignedEnumeration(32768), DriverValue.FromUnsignedEnumeration(65535)
    };
    Span<ushort> registers = stackalloc ushort[8];
    var encoded = ModbusDataCodec.EncodeMany(arrayDescriptor, source, ReadOnlySpan<ushort>.Empty, registers);
    MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
    var decoded = new DriverValue[4];
    var read = ModbusDataCodec.DecodeMany(arrayDescriptor, registers[..encoded.Value], decoded);
    MiniAssert.True(read.IsSuccess, read.Error.ToString());
    for (var i = 0; i < source.Length; i++) MiniAssert.Equal(source[i], decoded[i]);

    var overflow = new ModbusRegisterDescriptor(address, IndustrialDataType.Enumeration,
        EnumerationStorageType: IndustrialDataType.UInt8);
    Span<ushort> one = stackalloc ushort[1];
    var tooLarge = ModbusDataCodec.Encode(overflow, DriverValue.FromUnsignedEnumeration(256), one);
    MiniAssert.False(tooLarge.IsSuccess);
    return Task.CompletedTask;
}

static Task PackedBcdDecimalAsync()
{
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    Span<ushort> registers = stackalloc ushort[4];

    var bcd16 = new ModbusRegisterDescriptor(address, IndustrialDataType.Decimal,
        DecimalFormat: ModbusDecimalFormat.PackedBcd16, DecimalScale: 2);
    var e16 = ModbusDataCodec.Encode(bcd16, DriverValue.FromDecimal(12.34m), registers);
    MiniAssert.True(e16.IsSuccess, e16.Error.ToString());
    MiniAssert.Equal((ushort)0x1234, registers[0]);
    var d16 = ModbusDataCodec.Decode(bcd16, registers[..1]);
    MiniAssert.True(d16.IsSuccess, d16.Error.ToString());
    MiniAssert.Equal(12.34m, d16.Value.AsDecimal());

    var bcd32 = new ModbusRegisterDescriptor(address, IndustrialDataType.Decimal,
        DecimalFormat: ModbusDecimalFormat.PackedBcd32, DecimalScale: 2);
    var e32 = ModbusDataCodec.Encode(bcd32, DriverValue.FromDecimal(123456.78m), registers);
    MiniAssert.True(e32.IsSuccess, e32.Error.ToString());
    MiniAssert.Equal((ushort)0x1234, registers[0]);
    MiniAssert.Equal((ushort)0x5678, registers[1]);
    var d32 = ModbusDataCodec.Decode(bcd32, registers[..2]);
    MiniAssert.True(d32.IsSuccess, d32.Error.ToString());
    MiniAssert.Equal(123456.78m, d32.Value.AsDecimal());

    registers[0] = 0x1A23;
    var invalid = ModbusDataCodec.Decode(bcd16, registers[..1]);
    MiniAssert.False(invalid.IsSuccess);
    var negative = ModbusDataCodec.Encode(bcd16, DriverValue.FromDecimal(-1.23m), registers);
    MiniAssert.False(negative.IsSuccess);
    return Task.CompletedTask;
}

static Task RandomizedCodecAsync()
{
    var random = new Random(0x5EED_2026);
    var address = new ModbusAddress(ModbusObjectType.HoldingRegister, 0);
    var layouts = new[]
    {
        ModbusRegisterLayout.Standard,
        ModbusRegisterLayout.ByteSwapped,
        ModbusRegisterLayout.WordSwapped,
        ModbusRegisterLayout.LittleEndian
    };

    Span<byte> randomBytes = stackalloc byte[8];
    foreach (var layout in layouts)
        for (var i = 0; i < 500; i++)
        {
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.Int16, Layout: layout),
                DriverValue.FromInt16(unchecked((short)random.Next(short.MinValue, short.MaxValue + 1))));

            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.UInt16, Layout: layout),
                DriverValue.FromUInt16(unchecked((ushort)random.Next(0, ushort.MaxValue + 1))));

            var i32 = (int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1);
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.Int32, Layout: layout),
                DriverValue.FromInt32(i32));

            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.UInt32, Layout: layout),
                DriverValue.FromUInt32(unchecked((uint)random.NextInt64(0, (long)uint.MaxValue + 1))));

            random.NextBytes(randomBytes);
            var i64 = BinaryPrimitives.ReadInt64LittleEndian(randomBytes);
            var u64 = BinaryPrimitives.ReadUInt64LittleEndian(randomBytes);
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.Int64, Layout: layout),
                DriverValue.FromInt64(i64));
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.UInt64, Layout: layout),
                DriverValue.FromUInt64(u64));

            var single = (float)(random.NextDouble() * 2_000_000d - 1_000_000d);
            var dbl = random.NextDouble() * 2_000_000_000d - 1_000_000_000d;
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.Float32, Layout: layout),
                DriverValue.FromFloat32(single));
            RoundTrip(
                new ModbusRegisterDescriptor(address, IndustrialDataType.Float64, Layout: layout),
                DriverValue.FromFloat64(dbl));
        }

    return Task.CompletedTask;
}

static async Task RegisterAndBitIoAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(10, 0x1234);
    server.SetHolding(11, 0x5678);
    server.SetInput(3, 77);
    server.SetCoil(5, true);
    server.SetDiscrete(9, true);
    server.Start();

    await using var client = CreateClient(server.Port);
    var registers = new ushort[2];
    var holding = await client.ReadHoldingRegistersAsync(10, registers).ConfigureAwait(false);
    MiniAssert.True(holding.IsSuccess, holding.Error.ToString());
    MiniAssert.Equal((ushort)0x1234, registers[0]);
    MiniAssert.Equal((ushort)0x5678, registers[1]);

    var input = new ushort[1];
    MiniAssert.True((await client.ReadInputRegistersAsync(3, input).ConfigureAwait(false)).IsSuccess);
    MiniAssert.Equal((ushort)77, input[0]);

    var coils = new bool[1];
    MiniAssert.True((await client.ReadCoilsAsync(5, coils).ConfigureAwait(false)).IsSuccess);
    MiniAssert.True(coils[0]);

    var discrete = new bool[1];
    MiniAssert.True((await client.ReadDiscreteInputsAsync(9, discrete).ConfigureAwait(false)).IsSuccess);
    MiniAssert.True(discrete[0]);

    MiniAssert.True((await client.WriteSingleRegisterAsync(10, 99).ConfigureAwait(false)).IsSuccess);
    MiniAssert.Equal((ushort)99, server.GetHolding(10));

    MiniAssert.True((await client.WriteMultipleRegistersAsync(20, new ushort[] { 1, 2, 3 }).ConfigureAwait(false))
        .IsSuccess);
    MiniAssert.Equal((ushort)3, server.GetHolding(22));

    MiniAssert.True((await client.WriteSingleCoilAsync(5, false).ConfigureAwait(false)).IsSuccess);
    MiniAssert.False(server.GetCoil(5));

    MiniAssert.True((await client.WriteMultipleCoilsAsync(30, new[] { true, false, true }).ConfigureAwait(false))
        .IsSuccess);
    MiniAssert.True(server.GetCoil(30));
    MiniAssert.False(server.GetCoil(31));
    MiniAssert.True(server.GetCoil(32));
}

static async Task AdvancedFunctionsAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(30, 0xA55A);
    server.SetHolding(40, 100);
    server.SetHolding(41, 200);
    server.Start();

    await using var client = CreateClient(server.Port);
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.MaskWriteRegister));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.ReadWriteMultipleRegisters));

    var mask = await client.MaskWriteRegisterAsync(30, 0xFF00, 0x0033).ConfigureAwait(false);
    MiniAssert.True(mask.IsSuccess, mask.Error.ToString());
    MiniAssert.Equal((ushort)0xA533, server.GetHolding(30));

    var read = new ushort[2];
    var write = new ushort[] { 300, 400, 500 };
    var readWrite = await client.ReadWriteMultipleRegistersAsync(
            40,
            read,
            50,
            write)
        .ConfigureAwait(false);
    MiniAssert.True(readWrite.IsSuccess, readWrite.Error.ToString());
    MiniAssert.Equal(2, readWrite.Value);
    MiniAssert.SequenceEqual(new ushort[] { 100, 200 }, read);
    MiniAssert.Equal((ushort)300, server.GetHolding(50));
    MiniAssert.Equal((ushort)400, server.GetHolding(51));
    MiniAssert.Equal((ushort)500, server.GetHolding(52));

    var tooManyWrites = new ushort[122];
    var invalid = await client.ReadWriteMultipleRegistersAsync(0, read, 0, tooManyWrites).ConfigureAwait(false);
    MiniAssert.False(invalid.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Configuration, invalid.Error.Kind);
}


static async Task ApplicationServicesAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetFileRecord(3, 10, 0x1111);
    server.SetFileRecord(3, 11, 0x2222);
    server.SetFileRecord(3, 12, 0x3333);
    server.SetHolding(100, 3);
    server.SetHolding(101, 0xAAAA);
    server.SetHolding(102, 0xBBBB);
    server.SetHolding(103, 0xCCCC);
    server.Start();

    await using var client = CreateClient(server.Port);
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.DiagnosticsServices));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.FileRecordServices));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.FifoQueue));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.DeviceIdentification));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.RawProtocolPdu));
    MiniAssert.True(client.Capabilities.Supports(DriverFeature.EncapsulatedInterfaceTransport));

    var exceptionStatus = await client.ReadExceptionStatusAsync().ConfigureAwait(false);
    MiniAssert.True(exceptionStatus.IsSuccess, exceptionStatus.Error.ToUserMessage());
    MiniAssert.Equal((byte)0xA5, exceptionStatus.Value);

    var diagnostic = await client.DiagnosticsAsync(ModbusDiagnosticSubFunction.ReturnQueryData, 0x1234)
        .ConfigureAwait(false);
    MiniAssert.True(diagnostic.IsSuccess, diagnostic.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0x1234, diagnostic.Value);

    var vendorDiagnostic = await client.DiagnosticsRawAsync(0x7F00, 0xBEEF).ConfigureAwait(false);
    MiniAssert.True(vendorDiagnostic.IsSuccess, vendorDiagnostic.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0xBEEF, vendorDiagnostic.Value);

    var eventCounter = await client.GetCommEventCounterAsync().ConfigureAwait(false);
    MiniAssert.True(eventCounter.IsSuccess, eventCounter.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0, eventCounter.Value.Status);
    MiniAssert.Equal((ushort)42, eventCounter.Value.EventCount);

    var eventLog = await client.GetCommEventLogAsync().ConfigureAwait(false);
    MiniAssert.True(eventLog.IsSuccess, eventLog.Error.ToUserMessage());
    MiniAssert.Equal((ushort)42, eventLog.Value.EventCount);
    MiniAssert.Equal((ushort)77, eventLog.Value.MessageCount);
    MiniAssert.SequenceEqual(new byte[] { 0x21, 0x40 }, eventLog.Value.Events);

    var serverId = await client.ReportServerIdAsync().ConfigureAwait(false);
    MiniAssert.True(serverId.IsSuccess, serverId.Error.ToUserMessage());
    MiniAssert.Equal((byte)0x42, serverId.Value.ServerId);
    MiniAssert.True(serverId.Value.RunIndicator);
    MiniAssert.Equal("ProxusTest", Encoding.ASCII.GetString(serverId.Value.AdditionalData));

    var file = new ushort[3];
    var fileRead = await client.ReadFileRecordAsync(3, 10, file).ConfigureAwait(false);
    MiniAssert.True(fileRead.IsSuccess, fileRead.Error.ToUserMessage());
    MiniAssert.Equal(3, fileRead.Value);
    MiniAssert.SequenceEqual(new ushort[] { 0x1111, 0x2222, 0x3333 }, file);

    var fileWrite = await client.WriteFileRecordAsync(4, 20, new ushort[] { 0x4444, 0x5555, 0x6666 })
        .ConfigureAwait(false);
    MiniAssert.True(fileWrite.IsSuccess, fileWrite.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0x4444, server.GetFileRecord(4, 20));
    MiniAssert.Equal((ushort)0x5555, server.GetFileRecord(4, 21));
    MiniAssert.Equal((ushort)0x6666, server.GetFileRecord(4, 22));

    server.SetFileRecord(5, 1, 0x0101);
    server.SetFileRecord(5, 2, 0x0202);
    server.SetFileRecord(6, 8, 0x0808);
    server.SetFileRecord(6, 9, 0x0909);
    var multiRead = await client.ReadFileRecordsAsync(new ModbusFileRecordReadRequest[]
    {
        new(5, 1, 2),
        new(6, 8, 2)
    }).ConfigureAwait(false);
    MiniAssert.True(multiRead.IsSuccess, multiRead.Error.ToUserMessage());
    MiniAssert.Equal(2, multiRead.Value.Length);
    MiniAssert.SequenceEqual(new ushort[] { 0x0101, 0x0202 }, multiRead.Value[0].Values);
    MiniAssert.SequenceEqual(new ushort[] { 0x0808, 0x0909 }, multiRead.Value[1].Values);

    var multiWrite = await client.WriteFileRecordsAsync(new ModbusFileRecordWriteRequest[]
    {
        new(7, 100, new ushort[] { 0xA001, 0xA002 }),
        new(8, 200, new ushort[] { 0xB001, 0xB002, 0xB003 })
    }).ConfigureAwait(false);
    MiniAssert.True(multiWrite.IsSuccess, multiWrite.Error.ToUserMessage());
    MiniAssert.Equal((ushort)0xA001, server.GetFileRecord(7, 100));
    MiniAssert.Equal((ushort)0xA002, server.GetFileRecord(7, 101));
    MiniAssert.Equal((ushort)0xB003, server.GetFileRecord(8, 202));

    var fifo = new ushort[31];
    var fifoRead = await client.ReadFifoQueueAsync(100, fifo).ConfigureAwait(false);
    MiniAssert.True(fifoRead.IsSuccess, fifoRead.Error.ToUserMessage());
    MiniAssert.Equal(3, fifoRead.Value);
    MiniAssert.SequenceEqual(new ushort[] { 0xAAAA, 0xBBBB, 0xCCCC }, [.. fifo.AsSpan(0, 3)]);

    var identification = await client.ReadDeviceIdentificationAsync().ConfigureAwait(false);
    MiniAssert.True(identification.IsSuccess, identification.Error.ToUserMessage());
    MiniAssert.Equal(ModbusDeviceIdReadCode.Basic, identification.Value.ReadDeviceIdCode);
    MiniAssert.True(identification.Value.Objects.Length >= 3);
    MiniAssert.Equal("Proxus Labs", identification.Value.Objects[0].AsAscii());
    MiniAssert.Equal("VirtualModbus", identification.Value.Objects[1].AsAscii());
    MiniAssert.Equal("3.0", identification.Value.Objects[2].AsAscii());

    var individual = await client.ReadDeviceIdentificationAsync(ModbusDeviceIdReadCode.Specific, 1)
        .ConfigureAwait(false);
    MiniAssert.True(individual.IsSuccess, individual.Error.ToUserMessage());
    MiniAssert.Equal(1, individual.Value.Objects.Length);
    MiniAssert.Equal((byte)1, individual.Value.Objects[0].ObjectId);


    var allIdentification = await client.ReadAllDeviceIdentificationAsync().ConfigureAwait(false);
    MiniAssert.True(allIdentification.IsSuccess, allIdentification.Error.ToUserMessage());
    MiniAssert.True(allIdentification.Value.Objects.Length >= 4);

    var meiBuffer = new byte[251];
    var mei = await client
        .ExecuteEncapsulatedInterfaceAsync(ModbusMeiType.ReadDeviceIdentification, new byte[] { 1, 0 }, meiBuffer)
        .ConfigureAwait(false);
    MiniAssert.True(mei.IsSuccess, mei.Error.ToUserMessage());
    MiniAssert.True(mei.Value >= 5);
    MiniAssert.Equal((byte)1, meiBuffer[0]); // echoed Read Device ID code in type-specific response

    // A custom function has no RTU response length rule; RtuFaultsAsync covers its structured error.
    if (InProcessModbusServer.DefaultRtuFraming)
        return;

    var rawResponse = new byte[32];
    var raw = await client.ExecuteRawPduAsync(new byte[] { 0x41, 0xDE, 0xAD, 0xBE, 0xEF }, rawResponse)
        .ConfigureAwait(false);
    MiniAssert.True(raw.IsSuccess, raw.Error.ToUserMessage());
    MiniAssert.Equal(5, raw.Value);
    MiniAssert.SequenceEqual(new byte[] { 0x41, 0xDE, 0xAD, 0xBE, 0xEF }, [.. rawResponse.AsSpan(0, raw.Value)]);
}


static Task ProtocolEnumContractsAsync()
{
    var diagnostics = new[]
    {
        ModbusDiagnosticSubFunction.ReturnQueryData,
        ModbusDiagnosticSubFunction.RestartCommunicationsOption,
        ModbusDiagnosticSubFunction.ReturnDiagnosticRegister,
        ModbusDiagnosticSubFunction.ChangeAsciiInputDelimiter,
        ModbusDiagnosticSubFunction.ForceListenOnlyMode,
        ModbusDiagnosticSubFunction.ClearCountersAndDiagnosticRegister,
        ModbusDiagnosticSubFunction.ReturnBusMessageCount,
        ModbusDiagnosticSubFunction.ReturnBusCommunicationErrorCount,
        ModbusDiagnosticSubFunction.ReturnBusExceptionErrorCount,
        ModbusDiagnosticSubFunction.ReturnServerMessageCount,
        ModbusDiagnosticSubFunction.ReturnServerNoResponseCount,
        ModbusDiagnosticSubFunction.ReturnServerNakCount,
        ModbusDiagnosticSubFunction.ReturnServerBusyCount,
        ModbusDiagnosticSubFunction.ReturnBusCharacterOverrunCount,
        ModbusDiagnosticSubFunction.ClearOverrunCounterAndFlag
    };
    MiniAssert.Equal(diagnostics.Length, diagnostics.Distinct().Count());
    MiniAssert.Equal((ushort)0x0000, (ushort)ModbusDiagnosticSubFunction.ReturnQueryData);
    MiniAssert.Equal((ushort)0x0014, (ushort)ModbusDiagnosticSubFunction.ClearOverrunCounterAndFlag);

    var meiTypes = new[]
    {
        ModbusMeiType.CanOpenGeneralReference,
        ModbusMeiType.ReadDeviceIdentification
    };
    MiniAssert.Equal(2, meiTypes.Distinct().Count());
    MiniAssert.Equal((byte)0x0D, (byte)ModbusMeiType.CanOpenGeneralReference);
    MiniAssert.Equal((byte)0x0E, (byte)ModbusMeiType.ReadDeviceIdentification);

    var readCodes = new[]
    {
        ModbusDeviceIdReadCode.Basic,
        ModbusDeviceIdReadCode.Regular,
        ModbusDeviceIdReadCode.Extended,
        ModbusDeviceIdReadCode.Specific
    };
    MiniAssert.Equal(4, readCodes.Distinct().Count());

    var conformity = new[]
    {
        ModbusDeviceIdConformity.Basic,
        ModbusDeviceIdConformity.Regular,
        ModbusDeviceIdConformity.Extended,
        ModbusDeviceIdConformity.BasicWithIndividualAccess,
        ModbusDeviceIdConformity.RegularWithIndividualAccess,
        ModbusDeviceIdConformity.ExtendedWithIndividualAccess
    };
    MiniAssert.Equal(6, conformity.Distinct().Count());
    return Task.CompletedTask;
}

static async Task LifecycleAndCapabilitiesAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();

    await using IIndustrialDriver<ModbusRegisterDescriptor> driver = CreateClient(server.Port);
    MiniAssert.True(driver.Capabilities.Supports(DriverFeature.None));
    MiniAssert.True(driver.Capabilities.Supports(DriverFeature.NativeAsyncIo));
    MiniAssert.True(driver.Capabilities.Supports(DriverFeature.CompiledReadPlans));

    var connect = await driver.ConnectAsync();
    MiniAssert.True(connect.IsSuccess, connect.Error.ToUserMessage());
    MiniAssert.True(driver.IsConnected);

    var disconnect = await driver.DisconnectAsync();
    MiniAssert.True(disconnect.IsSuccess, disconnect.Error.ToUserMessage());
    MiniAssert.False(driver.IsConnected);

    var reconnect = await driver.ConnectAsync();
    MiniAssert.True(reconnect.IsSuccess, reconnect.Error.ToUserMessage());
    MiniAssert.True(driver.IsConnected);
}

static async Task TypedScalarIoAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);

    var floatDescriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 100),
        IndustrialDataType.Float32,
        Layout: ModbusRegisterLayout.WordSwapped);

    var write = await client.WriteValueAsync(floatDescriptor, DriverValue.FromFloat32(42.25f)).ConfigureAwait(false);
    MiniAssert.True(write.IsSuccess, write.Error.ToString());
    var read = await client.ReadValueAsync(floatDescriptor).ConfigureAwait(false);
    MiniAssert.True(read.IsSuccess, read.Error.ToString());
    MiniAssert.NearlyEqual(42.25, read.Value.AsFloat32(), 0);

    var stringDescriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 110),
        IndustrialDataType.String,
        ByteLength: 12,
        StringEncoding: ModbusStringEncoding.Utf8,
        StringTermination: ModbusStringTermination.NullTerminated);

    MiniAssert.True((await client.WriteValueAsync(stringDescriptor, DriverValue.FromString("Proxus Ş"))
        .ConfigureAwait(false)).IsSuccess);
    var stringRead = await client.ReadValueAsync(stringDescriptor).ConfigureAwait(false);
    MiniAssert.True(stringRead.IsSuccess, stringRead.Error.ToString());
    MiniAssert.Equal("Proxus Ş", stringRead.Value.AsString());

    var bytesDescriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 120),
        IndustrialDataType.Bytes,
        ByteLength: 5);
    var bytes = new byte[] { 1, 2, 3, 4, 5 };
    MiniAssert.True((await client.WriteValueAsync(bytesDescriptor, DriverValue.FromBytes(bytes)).ConfigureAwait(false))
        .IsSuccess);
    var bytesRead = await client.ReadValueAsync(bytesDescriptor).ConfigureAwait(false);
    MiniAssert.True(bytesRead.IsSuccess, bytesRead.Error.ToString());
    MiniAssert.SequenceEqual(bytes, bytesRead.Value.AsBytes().Span);
}

static async Task TypedArrayIoAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);

    var descriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 200),
        IndustrialDataType.Int32,
        3,
        Layout: ModbusRegisterLayout.LittleEndian);

    var values = new[]
    {
        DriverValue.FromInt32(1),
        DriverValue.FromInt32(-2),
        DriverValue.FromInt32(3_000_000)
    };

    var write = await client.WriteValuesAsync(descriptor, values).ConfigureAwait(false);
    MiniAssert.True(write.IsSuccess, write.Error.ToString());
    var target = new DriverValue[3];
    var read = await client.ReadValuesAsync(descriptor, target).ConfigureAwait(false);
    MiniAssert.True(read.IsSuccess, read.Error.ToString());
    MiniAssert.Equal(1, target[0].AsInt32());
    MiniAssert.Equal(-2, target[1].AsInt32());
    MiniAssert.Equal(3_000_000, target[2].AsInt32());

    var coilDescriptor = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.Coil, 300),
        IndustrialDataType.Bool,
        5);
    var coilValues = new[]
    {
        DriverValue.FromBoolean(true),
        DriverValue.FromBoolean(false),
        DriverValue.FromBoolean(true),
        DriverValue.FromBoolean(true),
        DriverValue.FromBoolean(false)
    };

    MiniAssert.True((await client.WriteValuesAsync(coilDescriptor, coilValues).ConfigureAwait(false)).IsSuccess);
    var coilTarget = new DriverValue[5];
    MiniAssert.True((await client.ReadValuesAsync(coilDescriptor, coilTarget).ConfigureAwait(false)).IsSuccess);
    MiniAssert.True(coilTarget[0].AsBoolean());
    MiniAssert.False(coilTarget[1].AsBoolean());
    MiniAssert.True(coilTarget[2].AsBoolean());
    MiniAssert.True(coilTarget[3].AsBoolean());
    MiniAssert.False(coilTarget[4].AsBoolean());
}

static async Task ByteLanePreservationAsync()
{
    await using var server = new InProcessModbusServer();
    server.SetHolding(50, 0xABCD);
    server.Start();
    await using var client = CreateClient(server.Port);

    var low = new ModbusRegisterDescriptor(
        new ModbusAddress(ModbusObjectType.HoldingRegister, 50),
        IndustrialDataType.UInt8,
        ByteLane: ModbusByteLane.Low,
        ByteWriteBehavior: ModbusByteWriteBehavior.PreserveOtherLane);

    var lowWrite = await client.WriteValueAsync(low, DriverValue.FromUInt8(0x12)).ConfigureAwait(false);
    MiniAssert.True(lowWrite.IsSuccess, lowWrite.Error.ToString());
    MiniAssert.Equal((ushort)0xAB12, server.GetHolding(50));

    var high = low with { ByteLane = ModbusByteLane.High };
    var highWrite = await client.WriteValueAsync(high, DriverValue.FromUInt8(0x34)).ConfigureAwait(false);
    MiniAssert.True(highWrite.IsSuccess, highWrite.Error.ToString());
    MiniAssert.Equal((ushort)0x3412, server.GetHolding(50));

    var reject = low with { ByteWriteBehavior = ModbusByteWriteBehavior.Reject };
    MiniAssert.False((await client.WriteValueAsync(reject, DriverValue.FromUInt8(1)).ConfigureAwait(false)).IsSuccess);
}

static async Task FragmentedResponseAsync()
{
    await using var server = new InProcessModbusServer { FragmentResponses = true };
    server.SetHolding(1, 55);
    server.Start();
    await using var client = CreateClient(server.Port);
    var values = new ushort[1];
    var result = await client.ReadHoldingRegistersAsync(1, values).ConfigureAwait(false);
    MiniAssert.True(result.IsSuccess, result.Error.ToString());
    MiniAssert.Equal((ushort)55, values[0]);
}

static async Task ConcurrentCallersAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromMilliseconds(3) };
    server.SetHolding(1, 123);
    server.Start();
    await using var client = CreateClient(server.Port);

    var tasks = Enumerable.Range(0, 32).Select(async _ =>
    {
        var value = new ushort[1];
        var result = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
        MiniAssert.True(result.IsSuccess, result.Error.ToString());
        MiniAssert.Equal((ushort)123, value[0]);
    }).ToArray();

    await Task.WhenAll(tasks).ConfigureAwait(false);
    MiniAssert.Equal(32, server.RequestCount);
    MiniAssert.Equal(1, server.ConnectionCount);
}

static async Task QueuedCancellationAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromMilliseconds(250) };
    server.SetHolding(1, 123);
    server.Start();
    await using var client = CreateClient(server.Port, TimeSpan.FromSeconds(2));

    var firstBuffer = new ushort[1];
    var first = client.ReadHoldingRegistersAsync(1, firstBuffer).AsTask();
    await server.FirstRequestObserved.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
    var secondBuffer = new ushort[1];
    var second = await client.ReadHoldingRegistersAsync(1, secondBuffer, cancellationToken: cts.Token)
        .ConfigureAwait(false);
    MiniAssert.False(second.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Cancelled, second.Error.Kind);

    var firstResult = await first.ConfigureAwait(false);
    MiniAssert.True(firstResult.IsSuccess, firstResult.Error.ToString());
    MiniAssert.Equal(1, server.RequestCount);
}

static async Task CancellationAndReconnectAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromSeconds(2) };
    server.SetHolding(1, 456);
    server.Start();
    await using var client = CreateClient(server.Port, Timeout.InfiniteTimeSpan);

    using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
    {
        var value = new ushort[1];
        var cancelled = await client.ReadHoldingRegistersAsync(1, value, cancellationToken: cts.Token)
            .ConfigureAwait(false);
        MiniAssert.False(cancelled.IsSuccess);
        MiniAssert.Equal(DriverErrorKind.Cancelled, cancelled.Error.Kind);
    }

    server.ResponseDelay = TimeSpan.Zero;
    var after = new ushort[1];
    var recovered = await client.ReadHoldingRegistersAsync(1, after).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess, recovered.Error.ToString());
    MiniAssert.Equal((ushort)456, after[0]);
    MiniAssert.True(server.ConnectionCount >= 2);
}

static async Task TimeoutAndReconnectAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromMilliseconds(300) };
    server.SetHolding(1, 789);
    server.Start();
    await using var client = CreateClient(server.Port, TimeSpan.FromMilliseconds(40));

    var value = new ushort[1];
    var timedOut = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.False(timedOut.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Timeout, timedOut.Error.Kind);

    server.ResponseDelay = TimeSpan.Zero;
    var recovered = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess, recovered.Error.ToString());
    MiniAssert.Equal((ushort)789, value[0]);
}

static async Task ExceptionResponseAsync()
{
    await using var server = new InProcessModbusServer { ForcedExceptionCode = 2 };
    server.SetHolding(1, 321);
    server.Start();
    await using var client = CreateClient(server.Port);

    var value = new ushort[1];
    var descriptor = new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 1),
        IndustrialDataType.UInt16);
    var result = await client.ReadValueAsync(descriptor).ConfigureAwait(false);
    MiniAssert.False(result.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Device, result.Error.Kind);
    MiniAssert.Equal(2, result.Error.NativeCode!.Value);
    MiniAssert.Equal(ExpectedProtocol(), result.Error.Protocol!);
    MiniAssert.Equal(DriverOperation.Read, result.Error.Operation);
    MiniAssert.Equal(descriptor.Address.ToString(), result.Error.Address!);
    MiniAssert.Equal(IndustrialDataType.UInt16, result.Error.DataType!.Value);
    MiniAssert.Equal(DriverRetryDisposition.FixConfiguration, result.Error.Retry);
    MiniAssert.Equal(DriverConnectionDisposition.Healthy, result.Error.Connection);
    MiniAssert.True(result.Error.ToUserMessage().Contains("Action:", StringComparison.Ordinal));
    MiniAssert.True(result.Error.ToUserMessage().Contains("Illegal Data Address", StringComparison.Ordinal));

    server.ForcedExceptionCode = null;
    var recovered = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess, recovered.Error.ToString());
    MiniAssert.Equal((ushort)321, value[0]);
    MiniAssert.Equal(1, server.ConnectionCount);
}

static async Task DiagnosticsMatrixAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);
    var descriptor = new ModbusRegisterDescriptor(new ModbusAddress(ModbusObjectType.HoldingRegister, 123),
        IndustrialDataType.UInt16);
    foreach (var code in new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x08, 0x0A, 0x0B, 0x7E })
    {
        server.ForcedExceptionCode = code;
        var r = await client.ReadValueAsync(descriptor);
        MiniAssert.False(r.IsSuccess);
        MiniAssert.Equal(code, r.Error.NativeCode!.Value);
        MiniAssert.True(!string.IsNullOrWhiteSpace(r.Error.NativeMeaning));
        MiniAssert.True(!string.IsNullOrWhiteSpace(r.Error.Cause));
        MiniAssert.True(!string.IsNullOrWhiteSpace(r.Error.SuggestedAction));
        MiniAssert.True(r.Error.Retry != DriverRetryDisposition.Unknown);
        MiniAssert.Equal(DriverConnectionDisposition.Healthy, r.Error.Connection);
        MiniAssert.Equal(descriptor.Address.ToString(), r.Error.Address!);
        MiniAssert.Equal(IndustrialDataType.UInt16, r.Error.DataType!.Value);
        MiniAssert.True(r.Error.ToUserMessage().Contains("Action:", StringComparison.Ordinal));
    }
}

static async Task ProtocolMismatchAsync()
{
    await using var server = new InProcessModbusServer { TransactionIdDelta = 1 };
    server.SetHolding(1, 654);
    server.Start();
    await using var client = CreateClient(server.Port);

    var value = new ushort[1];
    var invalid = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.False(invalid.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Protocol, invalid.Error.Kind);

    server.TransactionIdDelta = 0;
    var recovered = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess, recovered.Error.ToString());
    MiniAssert.Equal((ushort)654, value[0]);
    MiniAssert.True(server.ConnectionCount >= 2);
}

static async Task ProtocolFaultMatrixAsync()
{
    var cases = new[]
    {
        ServerFault.CorruptProtocolId,
        ServerFault.CorruptUnitId,
        ServerFault.InvalidMbapLength,
        ServerFault.WrongFunction,
        ServerFault.CloseBeforeResponse,
        ServerFault.CloseMidResponse
    };

    foreach (var fault in cases)
    {
        await using var server = new InProcessModbusServer { Fault = fault };
        server.SetHolding(2, 0x1234);
        server.Start();
        await using var client = CreateClient(server.Port, TimeSpan.FromMilliseconds(200));

        var value = new ushort[1];
        var failed = await client.ReadHoldingRegistersAsync(2, value).ConfigureAwait(false);
        MiniAssert.False(failed.IsSuccess, $"Fault {fault} unexpectedly succeeded.");
        MiniAssert.True(
            failed.Error.Kind is DriverErrorKind.Protocol or DriverErrorKind.Transport or DriverErrorKind.Timeout,
            $"Fault {fault} returned unexpected kind {failed.Error.Kind}.");

        server.Fault = ServerFault.None;
        var recovered = await client.ReadHoldingRegistersAsync(2, value).ConfigureAwait(false);
        MiniAssert.True(recovered.IsSuccess, $"Recovery after {fault} failed: {recovered.Error}");
        MiniAssert.Equal((ushort)0x1234, value[0]);
    }

    await using var exceptionServer = new InProcessModbusServer
    {
        Fault = ServerFault.MalformedException,
        ForcedExceptionCode = 2
    };
    exceptionServer.Start();
    await using var exceptionClient = CreateClient(exceptionServer.Port, TimeSpan.FromMilliseconds(100));
    var exceptionValue = new ushort[1];
    var malformed = await exceptionClient.ReadHoldingRegistersAsync(0, exceptionValue).ConfigureAwait(false);
    MiniAssert.False(malformed.IsSuccess);
    MiniAssert.True(malformed.Error.Kind is DriverErrorKind.Timeout or DriverErrorKind.Transport
        or DriverErrorKind.Protocol);
}

static async Task CorruptByteCountAsync()
{
    await using var server = new InProcessModbusServer { CorruptByteCount = true };
    server.SetHolding(1, 987);
    server.Start();
    await using var client = CreateClient(server.Port);

    var value = new ushort[1];
    var invalid = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.False(invalid.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Protocol, invalid.Error.Kind);

    server.CorruptByteCount = false;
    var recovered = await client.ReadHoldingRegistersAsync(1, value).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess, recovered.Error.ToString());
    MiniAssert.Equal((ushort)987, value[0]);
}

static async Task AddressOverflowAsync()
{
    await using var server = new InProcessModbusServer();
    server.Start();
    await using var client = CreateClient(server.Port);

    var values = new ushort[2];
    var result = await client.ReadHoldingRegistersAsync(ushort.MaxValue, values).ConfigureAwait(false);
    MiniAssert.False(result.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Configuration, result.Error.Kind);
    MiniAssert.Equal(0, server.RequestCount);
}

static async Task DisposeInterruptsRequestAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromSeconds(30) };
    server.SetHolding(1, 1);
    server.Start();

    var client = CreateClient(server.Port, Timeout.InfiniteTimeSpan);
    var buffer = new ushort[1];
    var operation = client.ReadHoldingRegistersAsync(1, buffer).AsTask();
    await server.FirstRequestObserved.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

    var dispose = client.DisposeAsync().AsTask();
    var completed = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
    MiniAssert.True(ReferenceEquals(completed, dispose), "Dispose did not interrupt the active request.");
    await dispose.ConfigureAwait(false);

    var result = await operation.ConfigureAwait(false);
    MiniAssert.False(result.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Disposed, result.Error.Kind);
}

static async Task RawFailureContextAsync()
{
    await using var server = new InProcessModbusServer { ResponseDelay = TimeSpan.FromSeconds(30) };
    server.SetHolding(1, 1);
    server.Start();

    await using (var client = CreateClient(server.Port, TimeSpan.FromMilliseconds(150)))
    {
        var holding = await client.ReadHoldingRegistersAsync(1, new ushort[1]).ConfigureAwait(false);
        AssertTransportContext(holding.Error, "MODBUS_TIMEOUT", DriverOperation.Read);
        var input = await client.ReadInputRegistersAsync(1, new ushort[1]).ConfigureAwait(false);
        AssertTransportContext(input.Error, "MODBUS_TIMEOUT", DriverOperation.Read);
        var coils = await client.ReadCoilsAsync(1, new bool[1]).ConfigureAwait(false);
        AssertTransportContext(coils.Error, "MODBUS_TIMEOUT", DriverOperation.Read);
        var single = await client.WriteSingleRegisterAsync(1, 7).ConfigureAwait(false);
        AssertTransportContext(single.Error, "MODBUS_TIMEOUT", DriverOperation.Write);
        var multiple = await client.WriteMultipleCoilsAsync(1, new[] { true }).ConfigureAwait(false);
        AssertTransportContext(multiple.Error, "MODBUS_TIMEOUT", DriverOperation.Write);
        var mask = await client.MaskWriteRegisterAsync(1, 0xFF00, 0x0001).ConfigureAwait(false);
        AssertTransportContext(mask.Error, "MODBUS_TIMEOUT", DriverOperation.Write);
    }

    await using var closing = new InProcessModbusServer { Fault = ServerFault.CloseBeforeResponse };
    closing.SetHolding(1, 1);
    closing.Start();
    await using var closingClient = CreateClient(closing.Port);
    var transport = await closingClient.ReadHoldingRegistersAsync(1, new ushort[1]).ConfigureAwait(false);
    AssertTransportContext(transport.Error, "MODBUS_TRANSPORT", DriverOperation.Read);

    var invalid = await closingClient.ReadHoldingRegistersAsync(1, new ushort[200]).ConfigureAwait(false);
    MiniAssert.False(invalid.IsSuccess);
    MiniAssert.Equal(DriverOperation.Validate, invalid.Error.Operation);
}

static async Task RefusedConnectionDispositionAsync()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();

    await using var client = CreateClient(port);
    var connect = await client.ConnectAsync().ConfigureAwait(false);
    AssertTransportContext(connect.Error, "MODBUS_CONNECT", DriverOperation.Connect);

    var implicitConnect = await client.ReadHoldingRegistersAsync(1, new ushort[1]).ConfigureAwait(false);
    AssertTransportContext(implicitConnect.Error, "MODBUS_CONNECT", DriverOperation.Read);

    var write = await client.WriteSingleCoilAsync(1, true).ConfigureAwait(false);
    AssertTransportContext(write.Error, "MODBUS_CONNECT", DriverOperation.Write);
}

static void AssertTransportContext(DriverError error, string code, DriverOperation operation)
{
    MiniAssert.Equal(code, error.Code);
    MiniAssert.Equal(operation, error.Operation);
    MiniAssert.Equal(ExpectedProtocol(), error.Protocol);
    MiniAssert.True(error.Endpoint is not null, $"{code} has no endpoint.");
    MiniAssert.Equal(DriverRetryDisposition.ReconnectAndRetry, error.Retry);
    MiniAssert.Equal(DriverConnectionDisposition.Broken, error.Connection);
}

static string ExpectedProtocol() => InProcessModbusServer.DefaultRtuFraming ? "Modbus RTU"
    : InProcessModbusServer.DefaultAsciiFraming ? "Modbus ASCII" : "Modbus TCP";

static async Task OverAsciiAsync(Func<Task> test)
{
    InProcessModbusServer.DefaultAsciiFraming = true;
    try
    {
        await test().ConfigureAwait(false);
    }
    finally
    {
        InProcessModbusServer.DefaultAsciiFraming = false;
    }
}

static async Task AsciiFrameKnownAnswerAsync()
{
    // LRC = two's complement of the byte sum: 01 03 00 00 00 0A sums to 0x0E, so the LRC is 0xF2;
    // 11 03 00 6B 00 03 (the specification's read example at slave 17) sums to 0x82, so the LRC is 0x7E.
    await using var server = new InProcessModbusServer { AsciiFraming = true };
    server.Start();
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1", Port = server.Port, UnitId = 1, Framing = ModbusFraming.Ascii
    });
    var ten = await client.ReadHoldingRegistersAsync(0, new ushort[10]).ConfigureAwait(false);
    MiniAssert.True(ten.IsSuccess, ten.IsSuccess ? null : ten.Error.ToUserMessage());
    MiniAssert.Equal(":01030000000AF2\r\n", System.Text.Encoding.ASCII.GetString(server.LastAsciiRequest));
    var three = await client.ReadHoldingRegistersAsync(0x6B, new ushort[3], unitId: 17).ConfigureAwait(false);
    MiniAssert.True(three.IsSuccess, three.IsSuccess ? null : three.Error.ToUserMessage());
    MiniAssert.Equal(":1103006B00037E\r\n", System.Text.Encoding.ASCII.GetString(server.LastAsciiRequest));
    var coil = await client.WriteSingleCoilAsync(0xAC, true).ConfigureAwait(false);
    MiniAssert.True(coil.IsSuccess);
    MiniAssert.Equal(":010500ACFF004F\r\n", System.Text.Encoding.ASCII.GetString(server.LastAsciiRequest));
}

static async Task AsciiFaultsAsync()
{
    await using var server = new InProcessModbusServer { AsciiFraming = true };
    server.SetHolding(5, 0x1234);
    server.Start();
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1", Port = server.Port, UnitId = 1, Framing = ModbusFraming.Ascii,
        RequestTimeout = TimeSpan.FromMilliseconds(500)
    });
    var buffer = new ushort[1];

    server.AsciiLowerCase = true;
    server.AsciiNoiseBeforeStart = "\0\r\n*";
    var tolerant = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.True(tolerant.IsSuccess && buffer[0] == 0x1234, "lower-case hex after line noise");
    server.AsciiLowerCase = false;

    server.CorruptNextAsciiLrc = true;
    var lrc = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_ASCII_LRC", lrc.Error.Code);
    MiniAssert.Equal("Modbus ASCII", lrc.Error.Protocol);
    MiniAssert.Equal(DriverOperation.Read, lrc.Error.Operation);
    MiniAssert.Equal(DriverRetryDisposition.ReconnectAndRetry, lrc.Error.Retry);
    MiniAssert.False(client.IsConnected, "an LRC error closes the transport");
    var recovered = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess && buffer[0] == 0x1234, "read after reconnect");

    server.Fault = ServerFault.CorruptUnitId;
    var unit = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_ASCII_UNIT_ID_MISMATCH", unit.Error.Code);
    server.Fault = ServerFault.None;

    var broadcast = await client.ReadHoldingRegistersAsync(5, buffer, unitId: 0).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_ASCII_UNIT_ID", broadcast.Error.Code);
    MiniAssert.Equal(DriverRetryDisposition.FixConfiguration, broadcast.Error.Retry);

    // Malformed replies from a raw socket peer: a non-hex character, and a frame without CR LF where its length ends.
    foreach (var (reply, code) in new[]
             {
                 (":0103020G34C6\r\n", "MODBUS_ASCII_FRAME"),
                 (":010302123406XX", "MODBUS_ASCII_FRAME"),
                 (":01030212340600\r\n", "MODBUS_ASCII_FRAME")
             })
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptSocketAsync().ConfigureAwait(false);
            var request = new byte[64];
            await socket.ReceiveAsync(request).ConfigureAwait(false);
            await socket.SendAsync(System.Text.Encoding.ASCII.GetBytes(reply)).ConfigureAwait(false);
            await Task.Delay(300).ConfigureAwait(false);
        });
        await using var raw = new ModbusTcpClient(new ModbusTcpOptions
        {
            Host = "127.0.0.1", Port = port, Framing = ModbusFraming.Ascii, RequestTimeout = TimeSpan.FromMilliseconds(200)
        });
        var result = await raw.ReadHoldingRegistersAsync(0, new ushort[1]).ConfigureAwait(false);
        MiniAssert.True(!result.IsSuccess && result.Error.Code == code,
            $"{reply.Trim()}: {(result.IsSuccess ? "success" : result.Error.Code)}");
        MiniAssert.False(raw.IsConnected);
        await peer.ConfigureAwait(false);
    }
}

static async Task AsciiStreamAndOptionsAsync()
{
    await using var server = new InProcessModbusServer { AsciiFraming = true };
    server.SetHolding(10, 4321);
    server.Start();
    var opened = 0;
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "loopback-serial",
        Framing = ModbusFraming.Ascii,
        RtuStreamFactory = async cancellationToken =>
        {
            Interlocked.Increment(ref opened);
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            await socket.ConnectAsync(System.Net.IPAddress.Loopback, server.Port, cancellationToken)
                .ConfigureAwait(false);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
    });

    var buffer = new ushort[1];
    var read = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.True(read.IsSuccess && buffer[0] == 4321, read.IsSuccess ? "stream read" : read.Error.ToUserMessage());
    server.CorruptNextAsciiLrc = true;
    var lrc = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_ASCII_LRC", lrc.Error.Code);
    MiniAssert.Equal("loopback-serial", lrc.Error.Endpoint);
    var again = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.True(again.IsSuccess, "the factory reopens the stream after a failure");
    MiniAssert.Equal(2, Volatile.Read(ref opened));

    async Task Expect(string code, ModbusTcpOptions options)
    {
        await using var invalid = new ModbusTcpClient(options);
        var result = await invalid.ConnectAsync().ConfigureAwait(false);
        MiniAssert.Equal(code, result.Error.Code);
        MiniAssert.Equal("Modbus ASCII", result.Error.Protocol);
    }

    await Expect("MODBUS_ASCII_UNIT_ID", new ModbusTcpOptions { Host = "127.0.0.1", Framing = ModbusFraming.Ascii, UnitId = 0 });
    await Expect("MODBUS_ASCII_UNIT_ID", new ModbusTcpOptions { Host = "127.0.0.1", Framing = ModbusFraming.Ascii, UnitId = 248 });
}

static async Task OverRtuAsync(Func<Task> test)
{
    InProcessModbusServer.DefaultRtuFraming = true;
    try
    {
        await test().ConfigureAwait(false);
    }
    finally
    {
        InProcessModbusServer.DefaultRtuFraming = false;
    }
}

static async Task RtuFrameKnownAnswerAsync()
{
    // Modbus over Serial Line examples: "read 10 holding registers from 0 at slave 1" is
    // 01 03 00 00 00 0A C5 CD, and "read 1 holding register from 0" is 01 03 00 00 00 01 84 0A.
    MiniAssert.Equal((ushort)0xCDC5, InProcessModbusServer.RtuCrc(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A }));
    MiniAssert.Equal((ushort)0x0A84, InProcessModbusServer.RtuCrc(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 }));

    await using var server = new InProcessModbusServer { RtuFraming = true };
    server.Start();
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1", Port = server.Port, UnitId = 1, Framing = ModbusFraming.Rtu
    });
    var ten = await client.ReadHoldingRegistersAsync(0, new ushort[10]).ConfigureAwait(false);
    MiniAssert.True(ten.IsSuccess, ten.IsSuccess ? null : ten.Error.ToUserMessage());
    MiniAssert.SequenceEqual(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0xC5, 0xCD }, server.LastRtuRequest);
    var one = await client.ReadHoldingRegistersAsync(0, new ushort[1]).ConfigureAwait(false);
    MiniAssert.True(one.IsSuccess);
    MiniAssert.SequenceEqual(new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01, 0x84, 0x0A }, server.LastRtuRequest);
}

static async Task RtuFaultsAsync()
{
    await using var server = new InProcessModbusServer { RtuFraming = true };
    server.SetHolding(5, 0x1234);
    server.Start();
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1", Port = server.Port, UnitId = 1, Framing = ModbusFraming.Rtu,
        RequestTimeout = TimeSpan.FromMilliseconds(500)
    });
    var buffer = new ushort[1];

    server.CorruptNextRtuCrc = true;
    var crc = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_RTU_CRC", crc.Error.Code);
    MiniAssert.Equal("Modbus RTU", crc.Error.Protocol);
    MiniAssert.Equal(DriverOperation.Read, crc.Error.Operation);
    MiniAssert.Equal(DriverRetryDisposition.ReconnectAndRetry, crc.Error.Retry);
    MiniAssert.False(client.IsConnected, "a CRC error closes the transport");
    var recovered = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.True(recovered.IsSuccess && buffer[0] == 0x1234, "read after reconnect");

    server.Fault = ServerFault.CorruptUnitId;
    var unit = await client.ReadHoldingRegistersAsync(5, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_RTU_UNIT_ID_MISMATCH", unit.Error.Code);
    server.Fault = ServerFault.None;

    var broadcast = await client.ReadHoldingRegistersAsync(5, buffer, unitId: 0).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_RTU_UNIT_ID", broadcast.Error.Code);
    MiniAssert.Equal(DriverRetryDisposition.FixConfiguration, broadcast.Error.Retry);

    var custom = await client.ExecuteRawPduAsync(new byte[] { 0x41, 0x00 }, new byte[16]).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_RTU_LENGTH", custom.Error.Code);
    MiniAssert.Equal(DriverErrorKind.Protocol, custom.Error.Kind);
}

static async Task RtuStreamTransportAsync()
{
    await using var server = new InProcessModbusServer { RtuFraming = true };
    server.SetHolding(10, 4321);
    server.Start();
    var opened = 0;
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "loopback-serial",
        Framing = ModbusFraming.Rtu,
        RtuInterFrameDelay = TimeSpan.FromMilliseconds(2),
        RtuStreamFactory = async cancellationToken =>
        {
            Interlocked.Increment(ref opened);
            var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);
            await socket.ConnectAsync(System.Net.IPAddress.Loopback, server.Port, cancellationToken)
                .ConfigureAwait(false);
            return new System.Net.Sockets.NetworkStream(socket, ownsSocket: true);
        }
    });

    var buffer = new ushort[1];
    var read = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.True(read.IsSuccess && buffer[0] == 4321, read.IsSuccess ? "stream read" : read.Error.ToUserMessage());
    var write = await client.WriteSingleRegisterAsync(11, 77).ConfigureAwait(false);
    MiniAssert.True(write.IsSuccess);
    MiniAssert.Equal((ushort)77, server.GetHolding(11));
    MiniAssert.True(client.IsConnected);

    server.CorruptNextRtuCrc = true;
    var crc = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_RTU_CRC", crc.Error.Code);
    MiniAssert.Equal("loopback-serial", crc.Error.Endpoint);
    var again = await client.ReadHoldingRegistersAsync(10, buffer).ConfigureAwait(false);
    MiniAssert.True(again.IsSuccess, "the factory reopens the stream after a failure");
    MiniAssert.Equal(2, Volatile.Read(ref opened));

    await using var failing = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "",
        Framing = ModbusFraming.Rtu,
        RtuStreamFactory = _ => throw new UnauthorizedAccessException("port busy")
    });
    var refused = await failing.ConnectAsync().ConfigureAwait(false);
    MiniAssert.Equal("MODBUS_CONNECT", refused.Error.Code);
    MiniAssert.Equal(DriverRetryDisposition.ReconnectAndRetry, refused.Error.Retry);
    MiniAssert.Equal("stream", refused.Error.Endpoint);
}

static async Task RtuOptionValidationAsync()
{
    async Task Expect(string code, ModbusTcpOptions options)
    {
        await using var client = new ModbusTcpClient(options);
        var result = await client.ConnectAsync().ConfigureAwait(false);
        MiniAssert.Equal(code, result.Error.Code);
        MiniAssert.Equal(DriverErrorKind.Configuration, result.Error.Kind);
    }

    await Expect("MODBUS_RTU_STREAM_FRAMING", new ModbusTcpOptions
        { Host = "", RtuStreamFactory = _ => ValueTask.FromResult<Stream>(new MemoryStream()) });
    await Expect("MODBUS_RTU_UNIT_ID", new ModbusTcpOptions { Host = "127.0.0.1", Framing = ModbusFraming.Rtu, UnitId = 0 });
    await Expect("MODBUS_RTU_UNIT_ID", new ModbusTcpOptions { Host = "127.0.0.1", Framing = ModbusFraming.Rtu, UnitId = 248 });
    await Expect("MODBUS_RTU_INTER_FRAME_DELAY", new ModbusTcpOptions
        { Host = "127.0.0.1", Framing = ModbusFraming.Rtu, RtuInterFrameDelay = TimeSpan.FromSeconds(2) });
    await Expect("MODBUS_FRAMING", new ModbusTcpOptions { Host = "127.0.0.1", Framing = (ModbusFraming)9 });
}

static async Task InvalidOptionsAsync()
{
    await using var client = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "",
        Port = 502
    });

    var result = await client.ConnectAsync().ConfigureAwait(false);
    MiniAssert.False(result.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Configuration, result.Error.Kind);

    await using var excessiveTimeout = new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1",
        RequestTimeout = TimeSpan.MaxValue
    });
    var excessive = await excessiveTimeout.ConnectAsync().ConfigureAwait(false);
    MiniAssert.False(excessive.IsSuccess);
    MiniAssert.Equal(DriverErrorKind.Configuration, excessive.Error.Kind);
}

static void RoundTrip(ModbusRegisterDescriptor descriptor, DriverValue value)
{
    Span<ushort> registers = stackalloc ushort[125];
    var encoded = ModbusDataCodec.Encode(descriptor, value, registers);
    MiniAssert.True(encoded.IsSuccess, encoded.Error.ToString());
    var decoded = ModbusDataCodec.Decode(descriptor, registers[..encoded.Value]);
    MiniAssert.True(decoded.IsSuccess, decoded.Error.ToString());
    MiniAssert.Equal(value.ToInvariantString(), decoded.Value.ToInvariantString());
}

static ModbusTcpClient CreateClient(int port, TimeSpan? requestTimeout = null)
{
    return new ModbusTcpClient(new ModbusTcpOptions
    {
        Host = "127.0.0.1",
        Port = port,
        UnitId = 1,
        Framing = InProcessModbusServer.DefaultRtuFraming ? ModbusFraming.Rtu
            : InProcessModbusServer.DefaultAsciiFraming ? ModbusFraming.Ascii : ModbusFraming.Mbap,
        ConnectTimeout = TimeSpan.FromSeconds(2),
        RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(2)
    });
}
