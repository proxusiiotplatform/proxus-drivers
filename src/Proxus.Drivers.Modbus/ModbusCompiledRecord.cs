namespace Proxus.Drivers.Modbus;

public sealed class ModbusCompiledReadRecord
{
    internal ModbusCompiledReadRecord(
        DriverRecordSchema<ModbusRegisterDescriptor> schema,
        ModbusCompiledReadPlan plan,
        DriverRecordPlanStatistics statistics)
    {
        Schema = schema;
        Plan = plan;
        Statistics = statistics;
    }

    public DriverRecordSchema<ModbusRegisterDescriptor> Schema { get; }
    public ModbusCompiledReadPlan Plan { get; }
    public DriverRecordPlanStatistics Statistics { get; }
}

public sealed class ModbusCompiledWriteRecord
{
    internal ModbusCompiledWriteRecord(
        DriverRecordSchema<ModbusRegisterDescriptor> schema,
        ModbusCompiledWritePlan plan,
        DriverRecordPlanStatistics statistics)
    {
        Schema = schema;
        Plan = plan;
        Statistics = statistics;
    }

    public DriverRecordSchema<ModbusRegisterDescriptor> Schema { get; }
    public ModbusCompiledWritePlan Plan { get; }
    public DriverRecordPlanStatistics Statistics { get; }
}

public static class ModbusRecordCompiler
{
    public static DriverResult<ModbusCompiledReadRecord> CompileRead(
        DriverRecordSchema<ModbusRegisterDescriptor> schema,
        ModbusReadPlannerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var items = new ModbusReadPlanItem[schema.Fields.Length];
        var fields = schema.Fields.Span;
        for (var i = 0; i < fields.Length; i++)
            items[i] = new ModbusReadPlanItem(fields[i].ValueOffset, fields[i].Descriptor);

        var plan = ModbusReadPlanner.Compile(items, options);
        if (!plan.IsSuccess)
            return DriverResult<ModbusCompiledReadRecord>.Failure(plan.Error);

        var physical = 0;
        foreach (var block in plan.Value.Blocks)
            physical = checked(physical + block.Quantity);
        return DriverResult<ModbusCompiledReadRecord>.Success(new ModbusCompiledReadRecord(
            schema,
            plan.Value,
            new DriverRecordPlanStatistics(fields.Length, schema.ValueCount, plan.Value.Blocks.Length, physical)));
    }

    public static DriverResult<ModbusCompiledWriteRecord> CompileWrite(
        DriverRecordSchema<ModbusRegisterDescriptor> schema)
    {
        ArgumentNullException.ThrowIfNull(schema);
        var items = new ModbusWritePlanItem[schema.Fields.Length];
        var fields = schema.Fields.Span;
        for (var i = 0; i < fields.Length; i++)
            items[i] = new ModbusWritePlanItem(fields[i].ValueOffset, fields[i].Descriptor);

        var plan = ModbusWritePlanner.Compile(items);
        if (!plan.IsSuccess)
            return DriverResult<ModbusCompiledWriteRecord>.Failure(plan.Error);

        var physical = 0;
        var baselines = 0;
        foreach (var block in plan.Value.Blocks)
        {
            physical = checked(physical + block.Quantity);
            if (block.ObjectType == ModbusObjectType.HoldingRegister)
                for (var i = 0; i < block.BindingCount; i++)
                    if (NeedsBaseline(plan.Value.Bindings[block.BindingOffset + i].Descriptor))
                    {
                        baselines++;
                        break;
                    }
        }

        return DriverResult<ModbusCompiledWriteRecord>.Success(new ModbusCompiledWriteRecord(
            schema,
            plan.Value,
            new DriverRecordPlanStatistics(fields.Length, schema.ValueCount, plan.Value.Blocks.Length, physical,
                baselines)));
    }

    private static bool NeedsBaseline(ModbusRegisterDescriptor descriptor)
    {
        return descriptor.DataType is IndustrialDataType.Int8 or IndustrialDataType.UInt8 or IndustrialDataType.Char8
            or IndustrialDataType.BitString8;
    }
}

public static class ModbusRecordExecutionExtensions
{
    public static ValueTask<DriverResult<int>> ReadRecordAsync(
        this ModbusTcpClient client,
        ModbusCompiledReadRecord record,
        Memory<DriverValue> destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(record);
        if (destination.Length < record.Schema.ValueCount)
            return ValueTask.FromResult(DriverResult<int>.Failure(BufferError(
                "MODBUS_RECORD_DESTINATION_SHORT", destination.Length, record.Schema.ValueCount)));
        return client.ReadPlanAsync(record.Plan, destination, cancellationToken);
    }

    public static ValueTask<DriverResult<int>> WriteRecordAsync(
        this ModbusTcpClient client,
        ModbusCompiledWriteRecord record,
        ReadOnlyMemory<DriverValue> source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(record);
        if (source.Length < record.Schema.ValueCount)
            return ValueTask.FromResult(DriverResult<int>.Failure(BufferError(
                "MODBUS_RECORD_SOURCE_SHORT", source.Length, record.Schema.ValueCount)));
        return client.WritePlanAsync(record.Plan, source, cancellationToken);
    }

    private static DriverError BufferError(string code, int actual, int required)
    {
        return new DriverError(
            DriverErrorKind.Configuration,
            code,
            $"Record buffer contains {actual} values; {required} are required by the compiled Modbus record.",
            Operation: DriverOperation.Validate,
            Protocol: "Modbus TCP",
            Cause: "The caller-provided value buffer does not match the compiled record shape.",
            SuggestedAction: "Allocate the buffer using record.Schema.ValueCount and retry.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}
