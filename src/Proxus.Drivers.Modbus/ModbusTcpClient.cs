using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Proxus.Drivers.Core;

namespace Proxus.Drivers.Modbus;

/// <summary>
///     Native-async Modbus client. A physical connection processes one request at a
///     time. This prevents request interleaving and permits reusable internal buffers.
///     Framing is Modbus TCP (MBAP) by default, or Modbus RTU or Modbus ASCII over TCP or over a
///     caller-supplied stream (see <see cref="ModbusTcpOptions.Framing" />).
/// </summary>
public sealed partial class ModbusTcpClient : IIndustrialDriver<ModbusRegisterDescriptor>
{
    private static readonly DriverCapabilities sCapabilities = new(
        "Modbus TCP",
        DriverFeature.ReadBits |
        DriverFeature.ReadRegisters |
        DriverFeature.WriteBits |
        DriverFeature.WriteRegisters |
        DriverFeature.ReadMultipleContiguous |
        DriverFeature.WriteMultipleContiguous |
        DriverFeature.PersistentConnection |
        DriverFeature.NativeAsyncIo |
        DriverFeature.Cancellation |
        DriverFeature.AddressCompilation |
        DriverFeature.TypedArrays |
        DriverFeature.ArbitraryBatchRead |
        DriverFeature.ArbitraryBatchWrite |
        DriverFeature.MaskWriteRegister |
        DriverFeature.ReadWriteMultipleRegisters |
        DriverFeature.CompiledReadPlans |
        DriverFeature.CompiledWritePlans |
        DriverFeature.DiagnosticsServices |
        DriverFeature.FileRecordServices |
        DriverFeature.FifoQueue |
        DriverFeature.DeviceIdentification |
        DriverFeature.RawProtocolPdu |
        DriverFeature.EncapsulatedInterfaceTransport,
        ModbusProtocol.MaximumReadRegisters,
        ModbusProtocol.MaximumWriteRegisters,
        ModbusProtocol.MaximumReadBits,
        ModbusProtocol.MaximumWriteBits,
        false);

    private readonly bool[] _bitScratch = new bool[ModbusProtocol.MaximumReadBits];
    private readonly DriverError? _configurationError;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly string _host;

    private readonly ModbusTcpOptions _options;
    private readonly ushort[] _registerScratch = new ushort[ModbusProtocol.MaximumReadRegisters];
    private readonly byte[] _requestBuffer = new byte[ModbusProtocol.MaximumAduLength];
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private readonly byte[] _responseHeader = new byte[ModbusProtocol.MbapHeaderLength];
    private readonly byte[] _responsePdu = new byte[ModbusProtocol.MaximumPduLength];
    private int _disposeState;

    private readonly byte[] _rtuFrame = new byte[ModbusRtu.MaximumFrameLength];
    private readonly byte[] _asciiFrame = new byte[ModbusAscii.MaximumFrameLength];
    private long _rtuLastFrameTimestamp;
    private Socket? _socket;
    private Stream? _stream;
    private int _transactionCounter;

    public ModbusTcpClient(ModbusTcpOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _host = options.Host.Trim();
        _configurationError = options.Validate();
    }

    private bool IsDisposed => Volatile.Read(ref _disposeState) != 0;

    public DriverCapabilities Capabilities => sCapabilities;
    public bool IsConnected => _options.RtuStreamFactory is null ? _socket is { Connected: true } : _stream is not null;

    private bool IsRtu => _options.Framing == ModbusFraming.Rtu;
    private bool IsAscii => _options.Framing == ModbusFraming.Ascii;
    private bool IsSerialLine => IsRtu || IsAscii;

    private string EndpointLabel => _options.RtuStreamFactory is null
        ? $"{_host}:{_options.Port}"
        : string.IsNullOrEmpty(_host) ? "stream" : _host;

    public async ValueTask<DriverResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(unavailable);

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Connect));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Connect));

            var result = await EnsureConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? result
                : DriverResult.Failure(Contextualize(result.Error, DriverOperation.Connect));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsDisposed)
            return DriverResult.Failure(Contextualize(DisposedError(), DriverOperation.Disconnect));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Disconnect));

        try
        {
            if (IsDisposed)
                return DriverResult.Failure(Contextualize(DisposedError(), DriverOperation.Disconnect));

            CloseSocket();
            return DriverResult.Success();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<DriverValue>> ReadValueAsync(
        ModbusRegisterDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        var validation = descriptor.ValidateScalar();
        if (!validation.IsSuccess)
            return DriverResult<DriverValue>.Failure(Contextualize(validation.Error, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<DriverValue>.Failure(Contextualize(unavailable, DriverOperation.Read,
                descriptor.Address.ToString(), descriptor.DataType));

        var rangeError = ValidateDescriptorRange(descriptor, false);
        if (rangeError is { } invalidRange)
            return DriverResult<DriverValue>.Failure(Contextualize(invalidRange, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<DriverValue>.Failure(Contextualize(waitError, DriverOperation.Read,
                descriptor.Address.ToString(), descriptor.DataType));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult<DriverValue>.Failure(Contextualize(unavailable, DriverOperation.Read,
                    descriptor.Address.ToString(), descriptor.DataType));

            if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            {
                var bitResult = await ReadBitsCoreAsync(
                        descriptor.Address.ObjectType == ModbusObjectType.Coil
                            ? ModbusProtocol.ReadCoils
                            : ModbusProtocol.ReadDiscreteInputs,
                        descriptor.Address.Offset,
                        _bitScratch.AsMemory(0, 1),
                        descriptor.Address.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false);

                return bitResult.IsSuccess
                    ? DriverResult<DriverValue>.Success(DriverValue.FromBoolean(_bitScratch[0]))
                    : DriverResult<DriverValue>.Failure(Contextualize(bitResult.Error, DriverOperation.Read,
                        descriptor.Address.ToString(), descriptor.DataType));
            }

            var countResult = descriptor.GetRequiredRegisterCount();
            if (!countResult.IsSuccess)
                return DriverResult<DriverValue>.Failure(Contextualize(countResult.Error, DriverOperation.Validate,
                    descriptor.Address.ToString(), descriptor.DataType));

            var count = countResult.Value;
            var registerResult = await ReadRegistersCoreAsync(
                    descriptor.Address.ObjectType == ModbusObjectType.HoldingRegister
                        ? ModbusProtocol.ReadHoldingRegisters
                        : ModbusProtocol.ReadInputRegisters,
                    descriptor.Address.Offset,
                    _registerScratch.AsMemory(0, count),
                    descriptor.Address.UnitId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!registerResult.IsSuccess)
                return DriverResult<DriverValue>.Failure(Contextualize(registerResult.Error, DriverOperation.Read,
                    descriptor.Address.ToString(), descriptor.DataType));
            var decoded = ModbusDataCodec.Decode(descriptor, _registerScratch.AsSpan(0, count));
            return decoded.IsSuccess
                ? decoded
                : DriverResult<DriverValue>.Failure(Contextualize(decoded.Error, DriverOperation.Decode,
                    descriptor.Address.ToString(), descriptor.DataType));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<int>> ReadValuesAsync(
        ModbusRegisterDescriptor descriptor,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
    {
        var validation = descriptor.ValidateArray();
        if (!validation.IsSuccess)
            return DriverResult<int>.Failure(Contextualize(validation.Error, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        if (destination.Length < descriptor.ElementCount)
            return DriverResult<int>.Failure(ConfigurationError(
                "MODBUS_DESTINATION_SHORT",
                $"Destination has {destination.Length} values; {descriptor.ElementCount} are required."));

        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read,
                descriptor.Address.ToString(), descriptor.DataType));

        var rangeError = ValidateLogicalDescriptorRange(descriptor);
        if (rangeError is { } invalidRange)
            return DriverResult<int>.Failure(Contextualize(invalidRange, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.Read,
                descriptor.Address.ToString(), descriptor.DataType));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read,
                    descriptor.Address.ToString(), descriptor.DataType));

            if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            {
                var completed = 0;
                var function = descriptor.Address.ObjectType == ModbusObjectType.Coil
                    ? ModbusProtocol.ReadCoils
                    : ModbusProtocol.ReadDiscreteInputs;
                while (completed < descriptor.ElementCount)
                {
                    var chunk = Math.Min(ModbusProtocol.MaximumReadBits, descriptor.ElementCount - completed);
                    var bitResult = await ReadBitsCoreAsync(
                            function,
                            checked((ushort)(descriptor.Address.Offset + completed)),
                            _bitScratch.AsMemory(0, chunk),
                            descriptor.Address.UnitId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!bitResult.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(bitResult.Error, DriverOperation.Read,
                            descriptor.Address.ToString(), descriptor.DataType));

                    var target = destination.Span;
                    for (var i = 0; i < chunk; i++)
                        target[completed + i] = DriverValue.FromBoolean(_bitScratch[i]);
                    completed += chunk;
                }

                return DriverResult<int>.Success(completed);
            }

            var widthResult = descriptor.GetElementRegisterWidth();
            if (!widthResult.IsSuccess)
                return DriverResult<int>.Failure(Contextualize(widthResult.Error, DriverOperation.Validate,
                    descriptor.Address.ToString(), descriptor.DataType));
            var width = widthResult.Value;
            if (width > ModbusProtocol.MaximumReadRegisters)
                return DriverResult<int>.Failure(ConfigurationError(
                    "MODBUS_ELEMENT_TOO_WIDE",
                    $"One {descriptor.DataType} element requires {width} registers; a Modbus read request can carry at most {ModbusProtocol.MaximumReadRegisters}."));

            var elementsPerChunk = Math.Max(1, ModbusProtocol.MaximumReadRegisters / width);
            var decodedCount = 0;
            var readFunction = descriptor.Address.ObjectType == ModbusObjectType.HoldingRegister
                ? ModbusProtocol.ReadHoldingRegisters
                : ModbusProtocol.ReadInputRegisters;
            while (decodedCount < descriptor.ElementCount)
            {
                var chunkElements = Math.Min(elementsPerChunk, descriptor.ElementCount - decodedCount);
                var chunkRegisters = checked((ushort)(chunkElements * width));
                var chunkDescriptor = descriptor with
                {
                    Address = descriptor.Address with
                    {
                        Offset = checked((ushort)(descriptor.Address.Offset + decodedCount * width))
                    },
                    ElementCount = checked((ushort)chunkElements)
                };
                var registerResult = await ReadRegistersCoreAsync(
                        readFunction,
                        chunkDescriptor.Address.Offset,
                        _registerScratch.AsMemory(0, chunkRegisters),
                        descriptor.Address.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!registerResult.IsSuccess)
                    return DriverResult<int>.Failure(Contextualize(registerResult.Error, DriverOperation.Read,
                        chunkDescriptor.Address.ToString(), descriptor.DataType));

                var decoded = ModbusDataCodec.DecodeMany(
                    chunkDescriptor,
                    _registerScratch.AsSpan(0, chunkRegisters),
                    destination.Span.Slice(decodedCount, chunkElements));
                if (!decoded.IsSuccess)
                    return DriverResult<int>.Failure(Contextualize(decoded.Error, DriverOperation.Decode,
                        chunkDescriptor.Address.ToString(), descriptor.DataType));
                decodedCount += decoded.Value;
            }

            return DriverResult<int>.Success(decodedCount);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>
    ///     Executes a heterogeneous one-shot read batch. The tag set is compiled into the same
    ///     coalesced block planner used by long-lived polling plans, so callers get correct
    ///     arbitrary-batch behavior without forfeiting contiguous-read optimization.
    /// </summary>
    public async ValueTask<DriverResult<int>> ReadBatchAsync(
        ReadOnlyMemory<DriverReadBatchItem<ModbusRegisterDescriptor>> items,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
    {
        if (items.IsEmpty)
            return DriverResult<int>.Success(0);

        var planItems = new ModbusReadPlanItem[items.Length];
        var source = items.Span;
        for (var i = 0; i < source.Length; i++)
            planItems[i] = new ModbusReadPlanItem(source[i].DestinationOffset, source[i].Descriptor);

        var compiled = ModbusReadPlanner.Compile(planItems);
        if (!compiled.IsSuccess)
            return DriverResult<int>.Failure(Contextualize(
                compiled.Error, DriverOperation.ReadBatch,
                retry: DriverRetryDisposition.FixConfiguration,
                connection: DriverConnectionDisposition.Unchanged));

        return await ReadPlanAsync(compiled.Value, destination, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<DriverResult> WriteValueAsync(
        ModbusRegisterDescriptor descriptor,
        DriverValue value,
        CancellationToken cancellationToken = default)
    {
        var validation = descriptor.ValidateScalar();
        if (!validation.IsSuccess)
            return DriverResult.Failure(Contextualize(validation.Error, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        if (value.Type != descriptor.DataType)
            return DriverResult.Failure(ConfigurationError(
                "MODBUS_VALUE_TYPE",
                $"Descriptor expects {descriptor.DataType}, but value is {value.Type}."));

        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write, descriptor.Address.ToString(),
                descriptor.DataType));

        if (descriptor.Address.ObjectType is not (ModbusObjectType.HoldingRegister or ModbusObjectType.Coil))
            return DriverResult.Failure(ConfigurationError("MODBUS_READ_ONLY",
                "Only coils and holding registers are writable."));

        var rangeError = ValidateDescriptorRange(descriptor, true);
        if (rangeError is { } invalidRange)
            return DriverResult.Failure(Contextualize(invalidRange, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        var bytePolicyError = ValidateByteWritePolicy(descriptor);
        if (bytePolicyError is { } invalidPolicy)
            return DriverResult.Failure(Contextualize(invalidPolicy, DriverOperation.Validate,
                descriptor.Address.ToString(), descriptor.DataType, DriverRetryDisposition.FixConfiguration,
                DriverConnectionDisposition.Unchanged));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write, descriptor.Address.ToString(),
                descriptor.DataType));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write,
                    descriptor.Address.ToString(), descriptor.DataType));

            if (descriptor.Address.ObjectType == ModbusObjectType.Coil)
            {
                var bitWrite = await WriteSingleCoilCoreAsync(
                        descriptor.Address.Offset,
                        value.AsBoolean(),
                        descriptor.Address.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false);
                return bitWrite.IsSuccess
                    ? bitWrite
                    : DriverResult.Failure(Contextualize(bitWrite.Error, DriverOperation.Write,
                        descriptor.Address.ToString(), descriptor.DataType));
            }

            var countResult = descriptor.GetRequiredWriteRegisterCount();
            if (!countResult.IsSuccess)
                return DriverResult.Failure(countResult.Error);

            var count = countResult.Value;
            DriverResult<int> encoded;
            if (NeedsByteBaseline(descriptor))
            {
                var baseline = await ReadRegistersCoreAsync(
                        ModbusProtocol.ReadHoldingRegisters,
                        descriptor.Address.Offset,
                        _registerScratch.AsMemory(0, count),
                        descriptor.Address.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (!baseline.IsSuccess)
                    return DriverResult.Failure(baseline.Error);

                encoded = ModbusDataCodec.Encode(
                    descriptor,
                    value,
                    _registerScratch.AsSpan(0, count),
                    _registerScratch.AsSpan(0, count));
            }
            else
            {
                encoded = ModbusDataCodec.Encode(
                    descriptor,
                    value,
                    _registerScratch.AsSpan(0, count));
            }

            if (!encoded.IsSuccess)
                return DriverResult.Failure(encoded.Error);

            var write = await WriteEncodedRegistersCoreAsync(
                    descriptor.Address.Offset,
                    encoded.Value,
                    descriptor.Address.UnitId,
                    cancellationToken)
                .ConfigureAwait(false);
            return write.IsSuccess
                ? write
                : DriverResult.Failure(Contextualize(write.Error, DriverOperation.Write, descriptor.Address.ToString(),
                    descriptor.DataType));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteValuesAsync(
        ModbusRegisterDescriptor descriptor,
        ReadOnlyMemory<DriverValue> values,
        CancellationToken cancellationToken = default)
    {
        var validation = descriptor.ValidateArray();
        if (!validation.IsSuccess)
            return DriverResult.Failure(validation.Error);

        if (values.Length != descriptor.ElementCount)
            return DriverResult.Failure(ConfigurationError(
                "MODBUS_ELEMENT_COUNT",
                $"Expected {descriptor.ElementCount} values, received {values.Length}."));

        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(unavailable);

        if (descriptor.Address.ObjectType is not (ModbusObjectType.HoldingRegister or ModbusObjectType.Coil))
            return DriverResult.Failure(ConfigurationError("MODBUS_READ_ONLY",
                "Only coils and holding registers are writable."));

        var rangeError = ValidateLogicalDescriptorRange(descriptor);
        if (rangeError is { } invalidRange)
            return DriverResult.Failure(invalidRange);

        var bytePolicyError = ValidateByteWritePolicy(descriptor);
        if (bytePolicyError is { } invalidPolicy)
            return DriverResult.Failure(invalidPolicy);

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(waitError);

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(unavailable);

            if (descriptor.Address.ObjectType == ModbusObjectType.Coil)
            {
                var completed = 0;
                while (completed < values.Length)
                {
                    var chunk = Math.Min(ModbusProtocol.MaximumWriteBits, values.Length - completed);
                    {
                        var source = values.Span.Slice(completed, chunk);
                        for (var i = 0; i < source.Length; i++)
                        {
                            if (source[i].Type != IndustrialDataType.Bool)
                                return DriverResult.Failure(ConfigurationError(
                                    "MODBUS_VALUE_TYPE",
                                    $"Coil element {completed + i} is {source[i].Type}; Bool is required."));
                            _bitScratch[i] = source[i].AsBoolean();
                        }
                    }

                    var write = chunk == 1
                        ? await WriteSingleCoilCoreAsync(
                                checked((ushort)(descriptor.Address.Offset + completed)),
                                _bitScratch[0], descriptor.Address.UnitId, cancellationToken)
                            .ConfigureAwait(false)
                        : await WriteMultipleCoilsCoreAsync(
                                checked((ushort)(descriptor.Address.Offset + completed)),
                                _bitScratch.AsMemory(0, chunk), descriptor.Address.UnitId, cancellationToken)
                            .ConfigureAwait(false);
                    if (!write.IsSuccess)
                        return DriverResult.Failure(Contextualize(write.Error, DriverOperation.Write,
                            descriptor.Address.ToString(), descriptor.DataType));
                    completed += chunk;
                }

                return DriverResult.Success();
            }

            var widthResult = descriptor.GetElementRegisterWidth();
            if (!widthResult.IsSuccess)
                return DriverResult.Failure(widthResult.Error);
            var width = widthResult.Value;
            if (width > ModbusProtocol.MaximumWriteRegisters)
                return DriverResult.Failure(ConfigurationError(
                    "MODBUS_ELEMENT_TOO_WIDE",
                    $"One {descriptor.DataType} element requires {width} registers; a Modbus write request can carry at most {ModbusProtocol.MaximumWriteRegisters}."));

            var elementsPerChunk = Math.Max(1, ModbusProtocol.MaximumWriteRegisters / width);
            var completedElements = 0;
            while (completedElements < values.Length)
            {
                var chunkElements = Math.Min(elementsPerChunk, values.Length - completedElements);
                var chunkRegisters = checked((ushort)(chunkElements * width));
                var chunkDescriptor = descriptor with
                {
                    Address = descriptor.Address with
                    {
                        Offset = checked((ushort)(descriptor.Address.Offset + completedElements * width))
                    },
                    ElementCount = checked((ushort)chunkElements)
                };

                DriverResult<int> encoded;
                if (NeedsByteBaseline(chunkDescriptor))
                {
                    var baseline = await ReadRegistersCoreAsync(
                            ModbusProtocol.ReadHoldingRegisters,
                            chunkDescriptor.Address.Offset,
                            _registerScratch.AsMemory(0, chunkRegisters),
                            descriptor.Address.UnitId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!baseline.IsSuccess)
                        return DriverResult.Failure(Contextualize(baseline.Error, DriverOperation.Read,
                            chunkDescriptor.Address.ToString(), descriptor.DataType));

                    encoded = ModbusDataCodec.EncodeMany(
                        chunkDescriptor, values.Span.Slice(completedElements, chunkElements),
                        _registerScratch.AsSpan(0, chunkRegisters), _registerScratch.AsSpan(0, chunkRegisters));
                }
                else
                {
                    encoded = ModbusDataCodec.EncodeMany(
                        chunkDescriptor, values.Span.Slice(completedElements, chunkElements),
                        _registerScratch.AsSpan(0, chunkRegisters));
                }

                if (!encoded.IsSuccess)
                    return DriverResult.Failure(Contextualize(encoded.Error, DriverOperation.Encode,
                        chunkDescriptor.Address.ToString(), descriptor.DataType));

                var write = await WriteEncodedRegistersCoreAsync(
                        chunkDescriptor.Address.Offset, encoded.Value, descriptor.Address.UnitId, cancellationToken)
                    .ConfigureAwait(false);
                if (!write.IsSuccess)
                    return DriverResult.Failure(Contextualize(write.Error, DriverOperation.Write,
                        chunkDescriptor.Address.ToString(), descriptor.DataType));
                completedElements += chunkElements;
            }

            return DriverResult.Success();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>Compiles and executes an arbitrary heterogeneous write batch.</summary>
    public async ValueTask<DriverResult<int>> WriteBatchAsync(
        ReadOnlyMemory<DriverWriteBatchItem<ModbusRegisterDescriptor>> items,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default)
    {
        if (items.IsEmpty) return DriverResult<int>.Success(0);
        var planItems = new ModbusWritePlanItem[items.Length];
        for (var i = 0; i < items.Length; i++)
            planItems[i] = new ModbusWritePlanItem(items.Span[i].SourceOffset, items.Span[i].Descriptor);
        var compiled = ModbusWritePlanner.Compile(planItems);
        if (!compiled.IsSuccess)
            return DriverResult<int>.Failure(Contextualize(compiled.Error, DriverOperation.WriteBatch,
                retry: DriverRetryDisposition.FixConfiguration, connection: DriverConnectionDisposition.Unchanged));
        return await WritePlanAsync(compiled.Value, source, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            return;

        await _disposeCts.CancelAsync().ConfigureAwait(false);
        CloseSocket();

        await _requestGate.WaitAsync().ConfigureAwait(false);
        try
        {
            CloseSocket();
        }
        finally
        {
            _requestGate.Release();
            _disposeCts.Dispose();
        }
    }

    public ValueTask<DriverResult<int>> ReadHoldingRegistersAsync(
        ushort startAddress,
        Memory<ushort> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return ReadRegistersAsync(
            ModbusProtocol.ReadHoldingRegisters,
            startAddress,
            destination,
            unitId,
            cancellationToken);
    }

    public ValueTask<DriverResult<int>> ReadInputRegistersAsync(
        ushort startAddress,
        Memory<ushort> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return ReadRegistersAsync(
            ModbusProtocol.ReadInputRegisters,
            startAddress,
            destination,
            unitId,
            cancellationToken);
    }

    public ValueTask<DriverResult<int>> ReadCoilsAsync(
        ushort startAddress,
        Memory<bool> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return ReadBitsAsync(
            ModbusProtocol.ReadCoils,
            startAddress,
            destination,
            unitId,
            cancellationToken);
    }

    public ValueTask<DriverResult<int>> ReadDiscreteInputsAsync(
        ushort startAddress,
        Memory<bool> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return ReadBitsAsync(
            ModbusProtocol.ReadDiscreteInputs,
            startAddress,
            destination,
            unitId,
            cancellationToken);
    }

    public async ValueTask<DriverResult> WriteSingleRegisterAsync(
        ushort address,
        ushort value,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await WriteSingleRegisterCoreAsync(
                    address,
                    value,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteMultipleRegistersAsync(
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

        var rangeError = ValidateAddressRange(
            startAddress,
            values.Length,
            ModbusProtocol.MaximumWriteRegisters,
            "register");
        if (rangeError is { } invalidRange)
            return DriverResult.Failure(Contextualize(invalidRange, DriverOperation.Validate));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await WriteMultipleRegistersCoreAsync(
                    startAddress,
                    values,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteSingleCoilAsync(
        ushort address,
        bool value,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await WriteSingleCoilCoreAsync(
                    address,
                    value,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteMultipleCoilsAsync(
        ushort startAddress,
        ReadOnlyMemory<bool> values,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

        var rangeError = ValidateAddressRange(
            startAddress,
            values.Length,
            ModbusProtocol.MaximumWriteBits,
            "coil");
        if (rangeError is { } invalidRange)
            return DriverResult.Failure(Contextualize(invalidRange, DriverOperation.Validate));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await WriteMultipleCoilsCoreAsync(
                    startAddress,
                    values,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>
    ///     Atomically modifies selected bits in one holding register using Modbus function 0x16.
    ///     The resulting value is <c>(current AND andMask) OR (orMask AND NOT andMask)</c>.
    /// </summary>
    public async ValueTask<DriverResult> MaskWriteRegisterAsync(
        ushort address,
        ushort andMask,
        ushort orMask,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await MaskWriteRegisterCoreAsync(
                    address,
                    andMask,
                    orMask,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>
    ///     Executes Modbus function 0x17. The device applies the write portion and returns a
    ///     contiguous holding-register range in the same transaction.
    /// </summary>
    public async ValueTask<DriverResult<int>> ReadWriteMultipleRegistersAsync(
        ushort readStartAddress,
        Memory<ushort> readDestination,
        ushort writeStartAddress,
        ReadOnlyMemory<ushort> writeValues,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Write));

        var readRangeError = ValidateAddressRange(
            readStartAddress,
            readDestination.Length,
            ModbusProtocol.MaximumReadRegisters,
            "read register");
        if (readRangeError is { } invalidReadRange)
            return DriverResult<int>.Failure(Contextualize(invalidReadRange, DriverOperation.Validate));

        var writeRangeError = ValidateAddressRange(
            writeStartAddress,
            writeValues.Length,
            ModbusProtocol.MaximumReadWriteWriteRegisters,
            "write register");
        if (writeRangeError is { } invalidWriteRange)
            return DriverResult<int>.Failure(Contextualize(invalidWriteRange, DriverOperation.Validate));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.Write));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Write));

            return WithOperation(
                await ReadWriteMultipleRegistersCoreAsync(
                    readStartAddress,
                    readDestination,
                    writeStartAddress,
                    writeValues,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Write);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>Executes a configuration-time compiled Modbus read plan.</summary>
    public async ValueTask<DriverResult<int>> ReadPlanAsync(
        ModbusCompiledReadPlan plan,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (destination.Length < plan.ValueCount)
            return DriverResult<int>.Failure(Contextualize(new DriverError(
                    DriverErrorKind.Configuration,
                    "MODBUS_PLAN_DESTINATION_SHORT",
                    $"Destination has {destination.Length} slots; the compiled plan requires {plan.ValueCount}."),
                DriverOperation.ReadBatch, retry: DriverRetryDisposition.FixConfiguration,
                connection: DriverConnectionDisposition.Unchanged));
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.ReadBatch));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.ReadBatch));
        try
        {
            var valuesWritten = 0;
            for (var blockIndex = 0; blockIndex < plan.Blocks.Length; blockIndex++)
            {
                var block = plan.Blocks[blockIndex];
                if (block.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
                {
                    var read = await ReadBitsCoreAsync(
                            block.ObjectType == ModbusObjectType.Coil
                                ? ModbusProtocol.ReadCoils
                                : ModbusProtocol.ReadDiscreteInputs,
                            block.StartOffset,
                            _bitScratch.AsMemory(0, block.Quantity),
                            block.UnitId,
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!read.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(read.Error, DriverOperation.ReadBatch,
                            $"unit={block.UnitId};{block.ObjectType}:{block.StartOffset}"));

                    for (var i = 0; i < block.BindingCount; i++)
                    {
                        var binding = plan.Bindings[block.BindingOffset + i];
                        var target = destination.Span;
                        for (var element = 0; element < binding.Descriptor.ElementCount; element++)
                            target[binding.DestinationOffset + element] =
                                DriverValue.FromBoolean(_bitScratch[binding.RelativeOffset + element]);
                        valuesWritten += binding.Descriptor.ElementCount;
                    }

                    continue;
                }

                var registerRead = await ReadRegistersCoreAsync(
                        block.ObjectType == ModbusObjectType.HoldingRegister
                            ? ModbusProtocol.ReadHoldingRegisters
                            : ModbusProtocol.ReadInputRegisters,
                        block.StartOffset,
                        _registerScratch.AsMemory(0, block.Quantity),
                        block.UnitId,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (!registerRead.IsSuccess)
                    return DriverResult<int>.Failure(Contextualize(registerRead.Error, DriverOperation.ReadBatch,
                        $"unit={block.UnitId};{block.ObjectType}:{block.StartOffset}"));

                for (var i = 0; i < block.BindingCount; i++)
                {
                    var binding = plan.Bindings[block.BindingOffset + i];
                    var descriptor = binding.Descriptor;
                    var required = descriptor.GetRequiredRegisterCount();
                    if (!required.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(required.Error, DriverOperation.Decode,
                            descriptor.Address.ToString(), descriptor.DataType));
                    var source = _registerScratch.AsSpan(binding.RelativeOffset, required.Value);
                    if (descriptor.ElementCount == 1)
                    {
                        var decoded = ModbusDataCodec.Decode(descriptor, source);
                        if (!decoded.IsSuccess)
                            return DriverResult<int>.Failure(Contextualize(decoded.Error, DriverOperation.Decode,
                                descriptor.Address.ToString(), descriptor.DataType));
                        destination.Span[binding.DestinationOffset] = decoded.Value;
                        valuesWritten++;
                    }
                    else
                    {
                        var decoded = ModbusDataCodec.DecodeMany(
                            descriptor, source,
                            destination.Span.Slice(binding.DestinationOffset, descriptor.ElementCount));
                        if (!decoded.IsSuccess)
                            return DriverResult<int>.Failure(Contextualize(decoded.Error, DriverOperation.Decode,
                                descriptor.Address.ToString(), descriptor.DataType));
                        valuesWritten += decoded.Value;
                    }
                }
            }

            return DriverResult<int>.Success(valuesWritten);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    /// <summary>Executes a precompiled heterogeneous write plan under one connection gate.</summary>
    public async ValueTask<DriverResult<int>> WritePlanAsync(
        ModbusCompiledWritePlan plan,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (source.Length < plan.SourceValueCount)
            return DriverResult<int>.Failure(Contextualize(new DriverError(
                    DriverErrorKind.Configuration, "MODBUS_WRITE_PLAN_SOURCE_SHORT",
                    $"Source has {source.Length} values; the compiled write plan requires {plan.SourceValueCount}."),
                DriverOperation.WriteBatch, retry: DriverRetryDisposition.FixConfiguration,
                connection: DriverConnectionDisposition.Unchanged));
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.WriteBatch));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.WriteBatch));
        try
        {
            var valuesWritten = 0;
            for (var blockIndex = 0; blockIndex < plan.Blocks.Length; blockIndex++)
            {
                var block = plan.Blocks[blockIndex];
                if (block.ObjectType == ModbusObjectType.Coil)
                {
                    _bitScratch.AsSpan(0, block.Quantity).Clear();
                    for (var i = 0; i < block.BindingCount; i++)
                    {
                        var binding = plan.Bindings[block.BindingOffset + i];
                        var values = source.Span.Slice(binding.SourceOffset, binding.Descriptor.ElementCount);
                        for (var element = 0; element < values.Length; element++)
                        {
                            if (values[element].Type != IndustrialDataType.Bool)
                                return DriverResult<int>.Failure(Contextualize(ConfigurationError(
                                        "MODBUS_VALUE_TYPE",
                                        $"Coil source value {binding.SourceOffset + element} is {values[element].Type}; Bool is required."),
                                    DriverOperation.WriteBatch, binding.Descriptor.Address.ToString(),
                                    binding.Descriptor.DataType));
                            _bitScratch[binding.RelativeOffset + element] = values[element].AsBoolean();
                        }

                        valuesWritten += values.Length;
                    }

                    var write = block.Quantity == 1
                        ? await WriteSingleCoilCoreAsync(block.StartOffset, _bitScratch[0], block.UnitId,
                            cancellationToken).ConfigureAwait(false)
                        : await WriteMultipleCoilsCoreAsync(block.StartOffset, _bitScratch.AsMemory(0, block.Quantity),
                            block.UnitId, cancellationToken).ConfigureAwait(false);
                    if (!write.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(write.Error, DriverOperation.WriteBatch,
                            $"unit={block.UnitId};coil:{block.StartOffset}"));
                    continue;
                }

                var needsBaseline = false;
                for (var i = 0; i < block.BindingCount; i++)
                    needsBaseline |= NeedsByteBaseline(plan.Bindings[block.BindingOffset + i].Descriptor);
                if (needsBaseline)
                {
                    var baseline = await ReadRegistersCoreAsync(
                            ModbusProtocol.ReadHoldingRegisters, block.StartOffset,
                            _registerScratch.AsMemory(0, block.Quantity), block.UnitId, cancellationToken)
                        .ConfigureAwait(false);
                    if (!baseline.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(baseline.Error, DriverOperation.WriteBatch,
                            $"unit={block.UnitId};hr:{block.StartOffset}"));
                }
                else
                {
                    _registerScratch.AsSpan(0, block.Quantity).Clear();
                }

                for (var i = 0; i < block.BindingCount; i++)
                {
                    var binding = plan.Bindings[block.BindingOffset + i];
                    var descriptor = binding.Descriptor;
                    var required = descriptor.GetRequiredWriteRegisterCount();
                    if (!required.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(required.Error, DriverOperation.Encode,
                            descriptor.Address.ToString(), descriptor.DataType));
                    var target = _registerScratch.AsSpan(binding.RelativeOffset, required.Value);
                    DriverResult<int> encoded;
                    if (descriptor.ElementCount == 1)
                    {
                        var value = source.Span[binding.SourceOffset];
                        encoded = NeedsByteBaseline(descriptor)
                            ? ModbusDataCodec.Encode(descriptor, value, target, target)
                            : ModbusDataCodec.Encode(descriptor, value, target);
                        valuesWritten++;
                    }
                    else
                    {
                        var values = source.Span.Slice(binding.SourceOffset, descriptor.ElementCount);
                        encoded = NeedsByteBaseline(descriptor)
                            ? ModbusDataCodec.EncodeMany(descriptor, values, target, target)
                            : ModbusDataCodec.EncodeMany(descriptor, values, target);
                        valuesWritten += descriptor.ElementCount;
                    }

                    if (!encoded.IsSuccess)
                        return DriverResult<int>.Failure(Contextualize(encoded.Error, DriverOperation.Encode,
                            descriptor.Address.ToString(), descriptor.DataType));
                }

                var registerWrite = await WriteEncodedRegistersCoreAsync(
                        block.StartOffset, block.Quantity, block.UnitId, cancellationToken)
                    .ConfigureAwait(false);
                if (!registerWrite.IsSuccess)
                    return DriverResult<int>.Failure(Contextualize(registerWrite.Error, DriverOperation.WriteBatch,
                        $"unit={block.UnitId};hr:{block.StartOffset}"));
            }

            return DriverResult<int>.Success(valuesWritten);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async ValueTask<DriverResult<int>> ReadRegistersAsync(
        byte function,
        ushort startAddress,
        Memory<ushort> destination,
        byte? unitId,
        CancellationToken cancellationToken)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read));

        var rangeError = ValidateAddressRange(
            startAddress,
            destination.Length,
            ModbusProtocol.MaximumReadRegisters,
            "register");
        if (rangeError is { } invalidRange)
            return DriverResult<int>.Failure(Contextualize(invalidRange, DriverOperation.Validate));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.Read));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read));

            return WithOperation(
                await ReadRegistersCoreAsync(
                    function,
                    startAddress,
                    destination,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Read);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async ValueTask<DriverResult<int>> ReadRegistersCoreAsync(
        byte function,
        ushort startAddress,
        Memory<ushort> destination,
        byte unitId,
        CancellationToken cancellationToken)
    {
        PrepareReadPdu(function, startAddress, checked((ushort)destination.Length));
        var result = await ExecutePreparedPduCoreAsync(5, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult<int>.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, function);
        if (functionError is { } error)
            return DriverResult<int>.Failure(ObserveResponseError(error));

        var expectedBytes = destination.Length * 2;
        if (response.Length != expectedBytes + 2 || response[1] != expectedBytes)
            return DriverResult<int>.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_BYTE_COUNT",
                $"Expected {expectedBytes} data bytes, received {(response.Length > 1 ? response[1] : -1)}.")));

        var target = destination.Span;
        for (var i = 0; i < target.Length; i++)
            target[i] = BinaryPrimitives.ReadUInt16BigEndian(response[(2 + i * 2)..]);

        return DriverResult<int>.Success(target.Length);
    }

    private async ValueTask<DriverResult<int>> ReadBitsAsync(
        byte function,
        ushort startAddress,
        Memory<bool> destination,
        byte? unitId,
        CancellationToken cancellationToken)
    {
        if (TryGetUnavailableError(out var unavailable))
            return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read));

        var rangeError = ValidateAddressRange(
            startAddress,
            destination.Length,
            ModbusProtocol.MaximumReadBits,
            "bit");
        if (rangeError is { } invalidRange)
            return DriverResult<int>.Failure(Contextualize(invalidRange, DriverOperation.Validate));

        var gateError = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gateError is { } waitError)
            return DriverResult<int>.Failure(Contextualize(waitError, DriverOperation.Read));

        try
        {
            if (TryGetUnavailableError(out unavailable))
                return DriverResult<int>.Failure(Contextualize(unavailable, DriverOperation.Read));

            return WithOperation(
                await ReadBitsCoreAsync(
                    function,
                    startAddress,
                    destination,
                    unitId ?? _options.UnitId,
                    cancellationToken)
                .ConfigureAwait(false),
                DriverOperation.Read);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async ValueTask<DriverResult<int>> ReadBitsCoreAsync(
        byte function,
        ushort startAddress,
        Memory<bool> destination,
        byte unitId,
        CancellationToken cancellationToken)
    {
        PrepareReadPdu(function, startAddress, checked((ushort)destination.Length));
        var result = await ExecutePreparedPduCoreAsync(5, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult<int>.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, function);
        if (functionError is { } error)
            return DriverResult<int>.Failure(ObserveResponseError(error));

        var expectedBytes = (destination.Length + 7) / 8;
        if (response.Length != expectedBytes + 2 || response[1] != expectedBytes)
            return DriverResult<int>.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_BYTE_COUNT",
                $"Expected {expectedBytes} packed bytes, received {(response.Length > 1 ? response[1] : -1)}.")));

        var target = destination.Span;
        for (var i = 0; i < target.Length; i++)
            target[i] = (response[2 + (i >> 3)] & (1 << (i & 7))) != 0;

        return DriverResult<int>.Success(target.Length);
    }

    private async ValueTask<DriverResult> WriteEncodedRegistersCoreAsync(
        ushort startAddress,
        int count,
        byte unitId,
        CancellationToken cancellationToken)
    {
        return count == 1
            ? await WriteSingleRegisterCoreAsync(
                    startAddress,
                    _registerScratch[0],
                    unitId,
                    cancellationToken)
                .ConfigureAwait(false)
            : await WriteMultipleRegistersCoreAsync(
                    startAddress,
                    _registerScratch.AsMemory(0, count),
                    unitId,
                    cancellationToken)
                .ConfigureAwait(false);
    }

    private async ValueTask<DriverResult> WriteSingleRegisterCoreAsync(
        ushort address,
        ushort value,
        byte unitId,
        CancellationToken cancellationToken)
    {
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5);
            pdu[0] = ModbusProtocol.WriteSingleRegister;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], value);
        }

        var result = await ExecutePreparedPduCoreAsync(5, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteSingleRegister);
        if (functionError is { } error)
            return DriverResult.Failure(ObserveResponseError(error));

        if (response.Length != 5 ||
            !response.SequenceEqual(_requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5)))
            return DriverResult.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_WRITE_ECHO",
                "Single-register write response did not echo the request.")));

        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> WriteMultipleRegistersCoreAsync(
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        byte unitId,
        CancellationToken cancellationToken)
    {
        var pduLength = 6 + values.Length * 2;
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.WriteMultipleRegisters;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], checked((ushort)values.Length));
            pdu[5] = checked((byte)(values.Length * 2));

            var source = values.Span;
            for (var i = 0; i < source.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(pdu[(6 + i * 2)..], source[i]);
        }

        var result = await ExecutePreparedPduCoreAsync(pduLength, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteMultipleRegisters);
        if (functionError is { } error)
            return DriverResult.Failure(ObserveResponseError(error));

        if (response.Length != 5 ||
            BinaryPrimitives.ReadUInt16BigEndian(response[1..]) != startAddress ||
            BinaryPrimitives.ReadUInt16BigEndian(response[3..]) != values.Length)
            return DriverResult.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_WRITE_ECHO",
                "Multiple-register write response contains an invalid address or quantity.")));

        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> WriteSingleCoilCoreAsync(
        ushort address,
        bool value,
        byte unitId,
        CancellationToken cancellationToken)
    {
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5);
            pdu[0] = ModbusProtocol.WriteSingleCoil;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], value ? (ushort)0xFF00 : (ushort)0x0000);
        }

        var result = await ExecutePreparedPduCoreAsync(5, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteSingleCoil);
        if (functionError is { } error)
            return DriverResult.Failure(ObserveResponseError(error));

        if (response.Length != 5 ||
            !response.SequenceEqual(_requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5)))
            return DriverResult.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_WRITE_ECHO",
                "Single-coil write response did not echo the request.")));

        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> WriteMultipleCoilsCoreAsync(
        ushort startAddress,
        ReadOnlyMemory<bool> values,
        byte unitId,
        CancellationToken cancellationToken)
    {
        var byteCount = (values.Length + 7) / 8;
        var pduLength = 6 + byteCount;
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu.Clear();
            pdu[0] = ModbusProtocol.WriteMultipleCoils;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], checked((ushort)values.Length));
            pdu[5] = checked((byte)byteCount);

            var source = values.Span;
            for (var i = 0; i < source.Length; i++)
                if (source[i])
                    pdu[6 + (i >> 3)] |= (byte)(1 << (i & 7));
        }

        var result = await ExecutePreparedPduCoreAsync(pduLength, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteMultipleCoils);
        if (functionError is { } error)
            return DriverResult.Failure(ObserveResponseError(error));

        if (response.Length != 5 ||
            BinaryPrimitives.ReadUInt16BigEndian(response[1..]) != startAddress ||
            BinaryPrimitives.ReadUInt16BigEndian(response[3..]) != values.Length)
            return DriverResult.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_WRITE_ECHO",
                "Multiple-coil write response contains an invalid address or quantity.")));

        return DriverResult.Success();
    }

    private async ValueTask<DriverResult> MaskWriteRegisterCoreAsync(
        ushort address,
        ushort andMask,
        ushort orMask,
        byte unitId,
        CancellationToken cancellationToken)
    {
        const int pduLength = 7;
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.MaskWriteRegister;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], address);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], andMask);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[5..], orMask);
        }

        var result = await ExecutePreparedPduCoreAsync(pduLength, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.MaskWriteRegister);
        if (functionError is { } error)
            return DriverResult.Failure(ObserveResponseError(error));

        if (response.Length != pduLength ||
            !response.SequenceEqual(_requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength)))
            return DriverResult.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_WRITE_ECHO",
                "Mask-write response did not echo the request.")));

        return DriverResult.Success();
    }

    private async ValueTask<DriverResult<int>> ReadWriteMultipleRegistersCoreAsync(
        ushort readStartAddress,
        Memory<ushort> readDestination,
        ushort writeStartAddress,
        ReadOnlyMemory<ushort> writeValues,
        byte unitId,
        CancellationToken cancellationToken)
    {
        var pduLength = 10 + writeValues.Length * 2;
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.ReadWriteMultipleRegisters;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], readStartAddress);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], checked((ushort)readDestination.Length));
            BinaryPrimitives.WriteUInt16BigEndian(pdu[5..], writeStartAddress);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[7..], checked((ushort)writeValues.Length));
            pdu[9] = checked((byte)(writeValues.Length * 2));

            var source = writeValues.Span;
            for (var i = 0; i < source.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(pdu[(10 + i * 2)..], source[i]);
        }

        var result = await ExecutePreparedPduCoreAsync(pduLength, unitId, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
            return DriverResult<int>.Failure(result.Error);

        var response = _responsePdu.AsSpan(0, result.Value);
        var functionError =
            ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReadWriteMultipleRegisters);
        if (functionError is { } error)
            return DriverResult<int>.Failure(ObserveResponseError(error));

        var expectedBytes = readDestination.Length * 2;
        if (response.Length != expectedBytes + 2 || response[1] != expectedBytes)
            return DriverResult<int>.Failure(ObserveResponseError(ModbusProtocol.ProtocolError(
                "MODBUS_BYTE_COUNT",
                $"Expected {expectedBytes} read/write response bytes, received " +
                $"{(response.Length > 1 ? response[1] : -1)}.")));

        var target = readDestination.Span;
        for (var i = 0; i < target.Length; i++)
            target[i] = BinaryPrimitives.ReadUInt16BigEndian(response[(2 + i * 2)..]);

        return DriverResult<int>.Success(target.Length);
    }

    private void PrepareReadPdu(byte function, ushort startAddress, ushort quantity)
    {
        var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5);
        pdu[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], startAddress);
        BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], quantity);
    }

    private async ValueTask<DriverResult<int>> ExecutePreparedPduCoreAsync(
        int pduLength,
        byte unitId,
        CancellationToken cancellationToken)
    {
        if (IsSerialLine)
            return await ExecuteSerialLineCoreAsync(pduLength, unitId, cancellationToken).ConfigureAwait(false);

        var connect = await EnsureConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
        if (!connect.IsSuccess)
            return DriverResult<int>.Failure(connect.Error);

        var transaction = unchecked((ushort)Interlocked.Increment(ref _transactionCounter));
        var requestLength = ModbusProtocol.WriteAdu(
            _requestBuffer,
            transaction,
            unitId,
            _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength));

        using var timeout = TimeoutTokenScope.Create(cancellationToken, _options.RequestTimeout);
        try
        {
            var socket = _socket!;
            await SocketIo.SendAllAsync(
                    socket,
                    _requestBuffer.AsMemory(0, requestLength),
                    timeout.Token)
                .ConfigureAwait(false);

            await SocketIo.ReceiveExactlyAsync(socket, _responseHeader, timeout.Token)
                .ConfigureAwait(false);

            var headerError = ModbusProtocol.ValidateResponseHeader(
                _responseHeader,
                transaction,
                unitId,
                out var responsePduLength);

            if (headerError is { } invalidHeader)
            {
                CloseSocket();
                return DriverResult<int>.Failure(invalidHeader);
            }

            await SocketIo.ReceiveExactlyAsync(
                    socket,
                    _responsePdu.AsMemory(0, responsePduLength),
                    timeout.Token)
                .ConfigureAwait(false);

            return DriverResult<int>.Success(responsePduLength);
        }
        catch (OperationCanceledException)
        {
            CloseSocket();
            return DriverResult<int>.Failure(CancellationOrTimeoutError(
                cancellationToken,
                "MODBUS_CANCELLED",
                "The Modbus operation was cancelled by the caller.",
                "MODBUS_TIMEOUT",
                $"The Modbus operation exceeded {_options.RequestTimeout}."));
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException)
        {
            CloseSocket();
            return DriverResult<int>.Failure(IsDisposed
                ? DisposedError()
                : TransportError("MODBUS_TRANSPORT", ex.Message, ex.HResult));
        }
    }

    private async ValueTask<DriverResult> EnsureConnectedCoreAsync(CancellationToken cancellationToken)
    {
        if (IsDisposed)
            return DriverResult.Failure(DisposedError());

        if (_options.RtuStreamFactory is { } streamFactory)
            return await EnsureStreamCoreAsync(streamFactory, cancellationToken).ConfigureAwait(false);

        if (_socket is { Connected: true })
            return DriverResult.Success();

        CloseSocket();
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _disposeCts.Token);
        connectCts.CancelAfter(_options.ConnectTimeout);
        var connectToken = connectCts.Token;

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(_host, connectToken).ConfigureAwait(false);
            if (addresses.Length == 0)
                return DriverResult.Failure(TransportError(
                    "MODBUS_DNS",
                    $"No IP address was found for '{_host}'."));

            Exception? lastError = null;
            foreach (var address in addresses)
            {
                connectToken.ThrowIfCancellationRequested();
                var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = _options.NoDelay
                };

                if (_options.EnableTcpKeepAlive)
                    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);

                _socket = socket;
                try
                {
                    await socket.ConnectAsync(
                            new IPEndPoint(address, _options.Port),
                            connectToken)
                        .ConfigureAwait(false);

                    return DriverResult.Success();
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException
                                               or ObjectDisposedException)
                {
                    lastError = ex;
                    if (ReferenceEquals(_socket, socket))
                        _socket = null;

                    socket.Dispose();
                    if (ex is OperationCanceledException || IsDisposed)
                        break;
                }
            }

            if (IsDisposed)
                return DriverResult.Failure(DisposedError());

            if (lastError is OperationCanceledException)
                return DriverResult.Failure(CancellationOrTimeoutError(
                    cancellationToken,
                    "MODBUS_CONNECT_CANCELLED",
                    "Connection was cancelled by the caller.",
                    "MODBUS_CONNECT_TIMEOUT",
                    $"Connection exceeded {_options.ConnectTimeout}."));

            return DriverResult.Failure(TransportError(
                "MODBUS_CONNECT",
                lastError?.Message ?? $"Unable to connect to {_host}:{_options.Port}.",
                lastError?.HResult));
        }
        catch (OperationCanceledException)
        {
            return DriverResult.Failure(CancellationOrTimeoutError(
                cancellationToken,
                "MODBUS_CONNECT_CANCELLED",
                "Connection was cancelled by the caller.",
                "MODBUS_CONNECT_TIMEOUT",
                $"Connection exceeded {_options.ConnectTimeout}."));
        }
        catch (Exception ex) when (ex is SocketException or IOException or ArgumentException or ObjectDisposedException)
        {
            return DriverResult.Failure(IsDisposed
                ? DisposedError()
                : TransportError("MODBUS_CONNECT", ex.Message, ex.HResult));
        }
    }

    private async ValueTask<DriverResult> EnsureStreamCoreAsync(
        Func<CancellationToken, ValueTask<Stream>> streamFactory,
        CancellationToken cancellationToken)
    {
        if (_stream is not null)
            return DriverResult.Success();

        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeCts.Token);
        connectCts.CancelAfter(_options.ConnectTimeout);
        try
        {
            var stream = await streamFactory(connectCts.Token).ConfigureAwait(false);
            if (stream is null || !stream.CanRead || !stream.CanWrite)
            {
                stream?.Dispose();
                return DriverResult.Failure(TransportError("MODBUS_CONNECT",
                    "RtuStreamFactory returned no readable and writable stream."));
            }

            if (IsDisposed)
            {
                stream.Dispose();
                return DriverResult.Failure(DisposedError());
            }

            _stream = stream;
            _rtuLastFrameTimestamp = 0;
            return DriverResult.Success();
        }
        catch (OperationCanceledException)
        {
            return DriverResult.Failure(CancellationOrTimeoutError(
                cancellationToken,
                "MODBUS_CONNECT_CANCELLED",
                "Connection was cancelled by the caller.",
                "MODBUS_CONNECT_TIMEOUT",
                $"Connection exceeded {_options.ConnectTimeout}."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException
                                       or ArgumentException or TimeoutException or ObjectDisposedException)
        {
            return DriverResult.Failure(IsDisposed
                ? DisposedError()
                : TransportError("MODBUS_CONNECT", ex.Message, ex.HResult));
        }
    }

    /// <summary>
    ///     Sends the PDU prepared at <see cref="ModbusProtocol.MbapHeaderLength" /> as one Modbus RTU or ASCII
    ///     frame and copies the response PDU into the response buffer. Any framing, checksum or Unit ID error,
    ///     timeout or transport failure closes the transport, because the byte stream can no longer be
    ///     resynchronised.
    /// </summary>
    private async ValueTask<DriverResult<int>> ExecuteSerialLineCoreAsync(
        int pduLength,
        byte unitId,
        CancellationToken cancellationToken)
    {
        var label = IsAscii ? "MODBUS_ASCII" : "MODBUS_RTU";
        if (unitId is 0 or > ModbusRtu.MaximumUnitId)
            return DriverResult<int>.Failure(ConfigurationError($"{label}_UNIT_ID",
                $"{_options.ProtocolName} UnitId must be between 1 and {ModbusRtu.MaximumUnitId}; broadcast is not supported."));

        var connect = await EnsureConnectedCoreAsync(cancellationToken).ConfigureAwait(false);
        if (!connect.IsSuccess)
            return DriverResult<int>.Failure(connect.Error);

        using var timeout = TimeoutTokenScope.Create(cancellationToken, _options.RequestTimeout);
        // Some streams (serial ports on several platforms) ignore cancellation; disposing the stream is the
        // only way to interrupt a blocked read when the request times out or is cancelled.
        using var abortStream = _stream is { } activeStream
            ? timeout.Token.UnsafeRegister(static state => ((Stream)state!).Dispose(), activeStream)
            : default;
        try
        {
            var result = IsAscii
                ? await ExchangeAsciiAsync(pduLength, unitId, timeout.Token).ConfigureAwait(false)
                : await ExchangeRtuAsync(pduLength, unitId, timeout.Token).ConfigureAwait(false);
            if (!result.IsSuccess)
                CloseSocket();
            return result;
        }
        catch (OperationCanceledException)
        {
            CloseSocket();
            return DriverResult<int>.Failure(CancellationOrTimeoutError(
                cancellationToken,
                "MODBUS_CANCELLED",
                "The Modbus operation was cancelled by the caller.",
                "MODBUS_TIMEOUT",
                $"The Modbus operation exceeded {_options.RequestTimeout}."));
        }
        catch (Exception ex) when (ex is SocketException or IOException or ObjectDisposedException
                                       or TimeoutException or InvalidOperationException)
        {
            CloseSocket();
            if (!IsDisposed && timeout.Token.IsCancellationRequested)
                return DriverResult<int>.Failure(CancellationOrTimeoutError(
                    cancellationToken,
                    "MODBUS_CANCELLED",
                    "The Modbus operation was cancelled by the caller.",
                    "MODBUS_TIMEOUT",
                    $"The Modbus operation exceeded {_options.RequestTimeout}."));
            return DriverResult<int>.Failure(IsDisposed
                ? DisposedError()
                : TransportError("MODBUS_TRANSPORT", ex.Message, ex.HResult));
        }
    }

    private async ValueTask<DriverResult<int>> ExchangeRtuAsync(int pduLength, byte unitId, CancellationToken cancellationToken)
    {
        var frameLength = ModbusRtu.WriteFrame(
            _rtuFrame,
            unitId,
            _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength));
        await WaitInterFrameDelayAsync(cancellationToken).ConfigureAwait(false);
        await SendSerialLineAsync(_rtuFrame.AsMemory(0, frameLength), cancellationToken).ConfigureAwait(false);

        var received = 0;
        while (true)
        {
            var total = ModbusRtu.ResponseLength(_rtuFrame.AsSpan(0, received), out var required);
            if (total == ModbusRtu.UnknownLength)
                return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_RTU_LENGTH",
                    $"Function 0x{_rtuFrame[1]:X2} has no Modbus RTU response length rule; use Modbus TCP or ASCII framing for custom functions."));

            var target = total > 0 ? total : required;
            if (target > ModbusRtu.MaximumFrameLength)
                return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_RTU_LENGTH",
                    $"Modbus RTU response would exceed {ModbusRtu.MaximumFrameLength} bytes."));

            if (received < target)
            {
                await ReceiveSerialLineExactlyAsync(_rtuFrame.AsMemory(received, target - received), cancellationToken)
                    .ConfigureAwait(false);
                received = target;
            }

            if (total > 0)
                break;
        }

        _rtuLastFrameTimestamp = Stopwatch.GetTimestamp();
        var frame = _rtuFrame.AsSpan(0, received);
        if (!ModbusRtu.HasValidCrc(frame))
            return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_RTU_CRC",
                "The Modbus RTU response CRC does not match its contents."));

        if (frame[0] != unitId)
            return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_RTU_UNIT_ID_MISMATCH",
                $"Expected Unit ID {unitId}, received {frame[0]}."));

        var responsePduLength = received - 1 - ModbusRtu.CrcLength;
        frame.Slice(1, responsePduLength).CopyTo(_responsePdu);
        return DriverResult<int>.Success(responsePduLength);
    }

    /// <summary>
    ///     Sends one ASCII frame and reads the response: characters before the colon are discarded, as a colon
    ///     starts a new message. The function's length rule (shared with RTU) decides how many characters to
    ///     read; functions without a rule are read up to CR LF.
    /// </summary>
    private async ValueTask<DriverResult<int>> ExchangeAsciiAsync(int pduLength, byte unitId, CancellationToken cancellationToken)
    {
        var frameLength = ModbusAscii.WriteFrame(
            _asciiFrame,
            unitId,
            _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength));
        await SendSerialLineAsync(_asciiFrame.AsMemory(0, frameLength), cancellationToken).ConfigureAwait(false);

        var one = _asciiFrame.AsMemory(0, 1);
        for (var skipped = 0; ; skipped++)
        {
            if (skipped > ModbusAscii.MaximumFrameLength)
                return DriverResult<int>.Failure(AsciiFrameError("no ':' starts it"));
            await ReceiveSerialLineExactlyAsync(one, cancellationToken).ConfigureAwait(false);
            if (_asciiFrame[0] == ModbusAscii.Start)
                break;
        }

        // Decoded bytes go to _rtuFrame (unit, PDU, LRC); characters are read into _asciiFrame.
        var binary = 0;
        var delimited = false;
        while (true)
        {
            var rtuTotal = ModbusRtu.ResponseLength(_rtuFrame.AsSpan(0, binary), out var required);
            if (rtuTotal == ModbusRtu.UnknownLength)
            {
                delimited = true;
                break;
            }

            var target = rtuTotal > 0 ? rtuTotal - ModbusRtu.CrcLength + ModbusAscii.LrcLength : required;
            if (target > ModbusAscii.MaximumBinaryLength)
                return DriverResult<int>.Failure(AsciiFrameError(
                    $"it would exceed {ModbusAscii.MaximumFrameLength} characters"));

            if (binary < target)
            {
                var characters = _asciiFrame.AsMemory(0, 2 * (target - binary));
                await ReceiveSerialLineExactlyAsync(characters, cancellationToken).ConfigureAwait(false);
                if (!ModbusAscii.TryDecodeHex(characters.Span, _rtuFrame.AsSpan(binary, target - binary)))
                    return DriverResult<int>.Failure(AsciiFrameError("it contains a character that is not hexadecimal"));
                binary = target;
            }

            if (rtuTotal > 0)
                break;
        }

        if (delimited)
        {
            // Read pairs up to CR LF; binary keeps the bytes decoded so far.
            while (true)
            {
                var pair = _asciiFrame.AsMemory(0, 2);
                await ReceiveSerialLineExactlyAsync(pair, cancellationToken).ConfigureAwait(false);
                if (_asciiFrame[0] == ModbusAscii.CarriageReturn && _asciiFrame[1] == ModbusAscii.LineFeed)
                    break;
                if (binary >= ModbusAscii.MaximumBinaryLength)
                    return DriverResult<int>.Failure(AsciiFrameError(
                        $"it exceeds {ModbusAscii.MaximumFrameLength} characters"));
                if (!ModbusAscii.TryDecodeHex(pair.Span, _rtuFrame.AsSpan(binary, 1)))
                    return DriverResult<int>.Failure(AsciiFrameError("it contains a character that is not hexadecimal"));
                binary++;
            }
        }
        else
        {
            var end = _asciiFrame.AsMemory(0, 2);
            await ReceiveSerialLineExactlyAsync(end, cancellationToken).ConfigureAwait(false);
            if (_asciiFrame[0] != ModbusAscii.CarriageReturn || _asciiFrame[1] != ModbusAscii.LineFeed)
                return DriverResult<int>.Failure(AsciiFrameError("it does not end with CR LF where its length says"));
        }

        var frame = _rtuFrame.AsSpan(0, binary);
        if (binary < 1 + 1 + ModbusAscii.LrcLength)
            return DriverResult<int>.Failure(AsciiFrameError("it is too short"));

        if (!ModbusAscii.HasValidLrc(frame))
            return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_ASCII_LRC",
                "The Modbus ASCII response LRC does not match its contents."));

        if (frame[0] != unitId)
            return DriverResult<int>.Failure(ModbusProtocol.ProtocolError("MODBUS_ASCII_UNIT_ID_MISMATCH",
                $"Expected Unit ID {unitId}, received {frame[0]}."));

        var responsePduLength = binary - 1 - ModbusAscii.LrcLength;
        frame.Slice(1, responsePduLength).CopyTo(_responsePdu);
        return DriverResult<int>.Success(responsePduLength);
    }

    private static DriverError AsciiFrameError(string detail) =>
        ModbusProtocol.ProtocolError("MODBUS_ASCII_FRAME", $"The Modbus ASCII response is malformed: {detail}.");

    private async ValueTask WaitInterFrameDelayAsync(CancellationToken cancellationToken)
    {
        if (_options.RtuInterFrameDelay <= TimeSpan.Zero || _rtuLastFrameTimestamp == 0)
            return;
        var remaining = _options.RtuInterFrameDelay - Stopwatch.GetElapsedTime(_rtuLastFrameTimestamp);
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask SendSerialLineAsync(ReadOnlyMemory<byte> frame, CancellationToken cancellationToken)
    {
        if (_stream is { } stream)
        {
            await stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await SocketIo.SendAllAsync(_socket ?? throw new ObjectDisposedException(nameof(Socket)), frame,
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ReceiveSerialLineExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        if (_stream is { } stream)
        {
            // Stream.ReadExactlyAsync throws EndOfStreamException (an IOException) if the stream ends.
            await stream.ReadExactlyAsync(destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        await SocketIo.ReceiveExactlyAsync(_socket ?? throw new ObjectDisposedException(nameof(Socket)),
            destination, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<DriverError?> EnterRequestGateAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        catch (OperationCanceledException)
        {
            return IsDisposed
                ? DisposedError()
                : new DriverError(
                    DriverErrorKind.Cancelled,
                    "MODBUS_QUEUE_CANCELLED",
                    "The operation was cancelled while waiting for the connection request gate.",
                    Protocol: "Modbus TCP",
                    Cause:
                    "The caller cancelled before this operation acquired the per-connection serialization gate; no request was sent to the device.",
                    SuggestedAction:
                    "No reconnect is needed. Reschedule only if the application still requires the operation.",
                    Retry: DriverRetryDisposition.DoNotRetry,
                    Connection: DriverConnectionDisposition.Unchanged);
        }
    }

    private DriverError ObserveResponseError(DriverError error)
    {
        if (error.Kind == DriverErrorKind.Protocol)
            CloseSocket();

        return error;
    }

    private static DriverError TransportError(string code, string message, int? native = null)
    {
        return new DriverError(
            DriverErrorKind.Transport, code, message, native,
            Protocol: "Modbus TCP",
            Cause:
            "The TCP connection could not be established or was interrupted while the Modbus request was in flight.",
            SuggestedAction:
            "Verify host/port, routing, firewall, device power and TCP reachability; then reconnect and retry.",
            Retry: DriverRetryDisposition.ReconnectAndRetry,
            Connection: DriverConnectionDisposition.Broken);
    }

    private DriverResult WithOperation(DriverResult result, DriverOperation operation)
    {
        return result.IsSuccess ? result : DriverResult.Failure(Contextualize(result.Error, operation));
    }

    private DriverResult<int> WithOperation(DriverResult<int> result, DriverOperation operation)
    {
        return result.IsSuccess ? result : DriverResult<int>.Failure(Contextualize(result.Error, operation));
    }

    private DriverError Contextualize(
        DriverError error,
        DriverOperation operation,
        string? address = null,
        IndustrialDataType? dataType = null,
        DriverRetryDisposition? retry = null,
        DriverConnectionDisposition? connection = null)
    {
        // Shared codec, planner and transport helpers label errors "Modbus TCP"; relabel them for RTU and ASCII.
        if (IsSerialLine && error.Protocol == "Modbus TCP")
            error = error with { Protocol = null };
        return error.WithContext(
            _options.ProtocolName,
            operation,
            EndpointLabel,
            address,
            dataType,
            retry,
            connection);
    }

    private void CloseSocket()
    {
        Interlocked.Exchange(ref _stream, null)?.Dispose();
        var socket = Interlocked.Exchange(ref _socket, null);
        if (socket is null)
            return;

        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        socket.Dispose();
    }

    private bool TryGetUnavailableError(out DriverError error)
    {
        if (IsDisposed)
        {
            error = DisposedError();
            return true;
        }

        if (_configurationError is { } configurationError)
        {
            error = configurationError;
            return true;
        }

        error = DriverError.None;
        return false;
    }

    private DriverError CancellationOrTimeoutError(
        CancellationToken callerToken,
        string cancellationCode,
        string cancellationMessage,
        string timeoutCode,
        string timeoutMessage)
    {
        if (IsDisposed)
            return DisposedError();

        return callerToken.IsCancellationRequested
            ? new DriverError(
                DriverErrorKind.Cancelled, cancellationCode, cancellationMessage,
                Protocol: "Modbus TCP",
                Cause: "The caller cancelled an in-flight I/O operation.",
                SuggestedAction:
                "No automatic retry is required unless the application intentionally reschedules the operation.",
                Retry: DriverRetryDisposition.DoNotRetry,
                Connection: DriverConnectionDisposition.Broken)
            : new DriverError(
                DriverErrorKind.Timeout, timeoutCode, timeoutMessage,
                Protocol: "Modbus TCP",
                Cause: "The target did not complete the network operation within the configured timeout.",
                SuggestedAction:
                "Check target availability and network latency; retry on the fresh connection and increase the timeout only if the measured device response time requires it.",
                Retry: DriverRetryDisposition.ReconnectAndRetry,
                Connection: DriverConnectionDisposition.Broken);
    }

    private static bool NeedsByteBaseline(ModbusRegisterDescriptor descriptor)
    {
        return descriptor.DataType is IndustrialDataType.Int8 or IndustrialDataType.UInt8 or IndustrialDataType.Char8
                   or IndustrialDataType.BitString8 &&
               descriptor.ByteWriteBehavior == ModbusByteWriteBehavior.PreserveOtherLane;
    }

    private static DriverError? ValidateByteWritePolicy(ModbusRegisterDescriptor descriptor)
    {
        if (descriptor.DataType is IndustrialDataType.Int8 or IndustrialDataType.UInt8 or IndustrialDataType.Char8
                or IndustrialDataType.BitString8 &&
            descriptor.ByteWriteBehavior == ModbusByteWriteBehavior.Reject)
            return ConfigurationError(
                "MODBUS_BYTE_WRITE",
                "Int8/UInt8 writes require ZeroOtherLane or PreserveOtherLane.");

        return null;
    }

    private static DriverError? ValidateLogicalDescriptorRange(ModbusRegisterDescriptor descriptor)
    {
        int count;
        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
        {
            count = descriptor.ElementCount;
        }
        else
        {
            var required = descriptor.GetRequiredRegisterCount();
            if (!required.IsSuccess) return required.Error;
            count = required.Value;
        }

        if (count < 1)
            return ConfigurationError("MODBUS_COUNT", "Logical element/register count must be greater than zero.");
        if (descriptor.Address.Offset + (uint)count > ushort.MaxValue + 1U)
            return ConfigurationError(
                "MODBUS_ADDRESS_RANGE",
                $"Start address {descriptor.Address.Offset} plus logical count {count} exceeds the UInt16 Modbus address space.");
        return null;
    }

    private static DriverError? ValidateDescriptorRange(
        ModbusRegisterDescriptor descriptor,
        bool forWrite)
    {
        if (descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return ValidateAddressRange(
                descriptor.Address.Offset,
                descriptor.ElementCount,
                forWrite ? ModbusProtocol.MaximumWriteBits : ModbusProtocol.MaximumReadBits,
                "bit");

        var count = forWrite
            ? descriptor.GetRequiredWriteRegisterCount()
            : descriptor.GetRequiredRegisterCount();

        return count.IsSuccess
            ? ValidateAddressRange(
                descriptor.Address.Offset,
                count.Value,
                forWrite ? ModbusProtocol.MaximumWriteRegisters : ModbusProtocol.MaximumReadRegisters,
                "register")
            : count.Error;
    }

    private static DriverError? ValidateAddressRange(
        ushort startAddress,
        int count,
        int maximumCount,
        string itemName)
    {
        if (count < 1 || count > maximumCount)
            return ConfigurationError(
                "MODBUS_COUNT",
                $"{itemName} count must be between 1 and {maximumCount}.");

        if (startAddress + (uint)count > ushort.MaxValue + 1U)
            return ConfigurationError(
                "MODBUS_ADDRESS_RANGE",
                $"Start address {startAddress} plus count {count} exceeds the UInt16 Modbus address space.");

        return null;
    }

    private static DriverError ConfigurationError(string code, string message)
    {
        return new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Protocol: "Modbus TCP",
            Cause: "The requested operation is invalid before any network I/O is attempted.",
            SuggestedAction:
            "Correct the address, data type, count, write policy, or descriptor option identified by the error.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }

    private static DriverError DisposedError()
    {
        return new DriverError(
            DriverErrorKind.Disposed, "MODBUS_DISPOSED", "The Modbus client has been disposed.",
            Protocol: "Modbus TCP",
            Cause: "The application attempted to use a driver instance after disposal.",
            SuggestedAction: "Create or obtain a live driver instance; disposed drivers cannot reconnect.",
            Retry: DriverRetryDisposition.DoNotRetry,
            Connection: DriverConnectionDisposition.Broken);
    }
}
