namespace Proxus.Drivers.Modbus;

public readonly record struct ModbusReadPlanItem(int DestinationOffset, ModbusRegisterDescriptor Descriptor);

public sealed record ModbusReadPlannerOptions
{
    public ushort MaximumGapRegisters { get; init; } = 2;
    public ushort MaximumGapBits { get; init; } = 8;

    internal DriverError? Validate()
    {
        if (MaximumGapRegisters > ModbusProtocol.MaximumReadRegisters)
            return Error("MODBUS_PLAN_GAP_REGISTERS",
                $"MaximumGapRegisters cannot exceed {ModbusProtocol.MaximumReadRegisters}.");
        if (MaximumGapBits > ModbusProtocol.MaximumReadBits)
            return Error("MODBUS_PLAN_GAP_BITS", $"MaximumGapBits cannot exceed {ModbusProtocol.MaximumReadBits}.");
        return null;
    }

    private static DriverError Error(string code, string message)
    {
        return new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Protocol: "Modbus TCP",
            Cause: "The read planner has an invalid merge threshold.",
            SuggestedAction: "Reduce the configured planner gap to a value within the Modbus protocol limits.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}

public readonly record struct ModbusReadPlanBinding(
    int DestinationOffset,
    ushort RelativeOffset,
    ModbusRegisterDescriptor Descriptor);

public readonly record struct ModbusReadBlock(
    ModbusObjectType ObjectType,
    byte UnitId,
    ushort StartOffset,
    ushort Quantity,
    int BindingOffset,
    ushort BindingCount);

public sealed class ModbusCompiledReadPlan
{
    internal ModbusCompiledReadPlan(ModbusReadBlock[] blocks, ModbusReadPlanBinding[] bindings, int valueCount)
    {
        Blocks = blocks;
        Bindings = bindings;
        ValueCount = valueCount;
    }

    public ModbusReadBlock[] Blocks { get; }
    public ModbusReadPlanBinding[] Bindings { get; }
    public int ValueCount { get; }
}

public static class ModbusReadPlanner
{
    public static DriverResult<ModbusCompiledReadPlan> Compile(
        ReadOnlySpan<ModbusReadPlanItem> items,
        ModbusReadPlannerOptions? options = null)
    {
        options ??= new ModbusReadPlannerOptions();
        if (options.Validate() is { } optionError)
            return DriverResult<ModbusCompiledReadPlan>.Failure(optionError);
        if (items.IsEmpty)
            return Failure("MODBUS_PLAN_EMPTY", "At least one register is required to compile a read plan.",
                "Provide one or more configured Modbus tags.");

        var valueCount = 0;
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var validation = item.Descriptor.ElementCount == 1
                ? item.Descriptor.ValidateScalar()
                : item.Descriptor.ValidateArray();
            if (!validation.IsSuccess)
                return DriverResult<ModbusCompiledReadPlan>.Failure(validation.Error.WithContext(
                    "Modbus TCP", DriverOperation.Validate, address: item.Descriptor.Address.ToString(),
                    dataType: item.Descriptor.DataType,
                    retry: DriverRetryDisposition.FixConfiguration, connection: DriverConnectionDisposition.Unchanged));
            if (item.DestinationOffset < 0)
                return Failure("MODBUS_PLAN_DESTINATION", "DestinationOffset cannot be negative.",
                    "Assign each planned value a non-negative destination slot.");
            valueCount = Math.Max(valueCount, checked(item.DestinationOffset + item.Descriptor.ElementCount));
        }

        var occupiedDestinations = new bool[valueCount];
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            for (var element = 0; element < item.Descriptor.ElementCount; element++)
            {
                var destination = item.DestinationOffset + element;
                if (occupiedDestinations[destination])
                    return Failure("MODBUS_PLAN_DESTINATION_OVERLAP",
                        $"Destination slot {destination} is assigned by more than one planned tag.",
                        "Assign unique destination slots to every scalar or array element.");
                occupiedDestinations[destination] = true;
            }
        }

        // Expand logical arrays into protocol-sized fragments at configuration time. Runtime
        // plan execution therefore never has to discover Modbus limits or split a descriptor.
        var ranges = new List<Range>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var descriptor = item.Descriptor;
            var isBits = descriptor.Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput;
            var physicalWidth = 1;
            var maxElements = (int)ModbusProtocol.MaximumReadBits;
            if (!isBits)
            {
                var width = descriptor.GetElementRegisterWidth();
                if (!width.IsSuccess)
                    return DriverResult<ModbusCompiledReadPlan>.Failure(width.Error);
                physicalWidth = width.Value;
                if (physicalWidth > ModbusProtocol.MaximumReadRegisters)
                    return Failure(
                        "MODBUS_PLAN_ELEMENT_TOO_WIDE",
                        $"One {descriptor.DataType} element requires {physicalWidth} registers, above the {ModbusProtocol.MaximumReadRegisters}-register read limit.",
                        "Reduce the element wire width or use raw protocol-specific access for that vendor object.");
                maxElements = Math.Max(1, ModbusProtocol.MaximumReadRegisters / physicalWidth);
            }

            var done = 0;
            while (done < descriptor.ElementCount)
            {
                var elementCount = Math.Min(maxElements, descriptor.ElementCount - done);
                var physicalCount = checked(elementCount * physicalWidth);
                var startInt = checked(descriptor.Address.Offset + done * physicalWidth);
                var end = checked(startInt + physicalCount);
                if (end > ushort.MaxValue + 1)
                    return Failure("MODBUS_PLAN_RANGE",
                        "A planned register range exceeds the UInt16 Modbus address space.",
                        "Correct the start address or reduce the element count.");

                var fragment = descriptor with
                {
                    Address = descriptor.Address with { Offset = checked((ushort)startInt) },
                    ElementCount = checked((ushort)elementCount)
                };
                ranges.Add(new Range(
                    new ModbusReadPlanItem(item.DestinationOffset + done, fragment),
                    checked((ushort)startInt),
                    end));
                done += elementCount;
            }
        }

        ranges.Sort(static (a, b) =>
        {
            var unit = a.Item.Descriptor.Address.UnitId.CompareTo(b.Item.Descriptor.Address.UnitId);
            if (unit != 0) return unit;
            var area = a.Item.Descriptor.Address.ObjectType.CompareTo(b.Item.Descriptor.Address.ObjectType);
            if (area != 0) return area;
            return a.Start.CompareTo(b.Start);
        });

        var blocks = new List<ModbusReadBlock>(ranges.Count);
        var bindings = new List<ModbusReadPlanBinding>(ranges.Count);
        var cursor = 0;
        while (cursor < ranges.Count)
        {
            var first = ranges[cursor];
            var type = first.Item.Descriptor.Address.ObjectType;
            var unit = first.Item.Descriptor.Address.UnitId;
            var start = first.Start;
            var end = first.EndExclusive;
            var bindingOffset = bindings.Count;
            var isBits = type is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput;
            var maxQuantity = isBits ? ModbusProtocol.MaximumReadBits : ModbusProtocol.MaximumReadRegisters;
            var maxGap = isBits ? options.MaximumGapBits : options.MaximumGapRegisters;

            while (cursor < ranges.Count)
            {
                var current = ranges[cursor];
                var address = current.Item.Descriptor.Address;
                if (address.UnitId != unit || address.ObjectType != type)
                    break;
                var proposedEnd = Math.Max(end, current.EndExclusive);
                var gap = Math.Max(0, current.Start - end);
                if (bindings.Count > bindingOffset && (gap > maxGap || proposedEnd - start > maxQuantity))
                    break;

                bindings.Add(new ModbusReadPlanBinding(
                    current.Item.DestinationOffset,
                    checked((ushort)(current.Start - start)),
                    current.Item.Descriptor));
                end = proposedEnd;
                cursor++;
            }

            blocks.Add(new ModbusReadBlock(
                type,
                unit,
                start,
                checked((ushort)(end - start)),
                bindingOffset,
                checked((ushort)(bindings.Count - bindingOffset))));
        }

        return DriverResult<ModbusCompiledReadPlan>.Success(new ModbusCompiledReadPlan([.. blocks],
            [.. bindings], valueCount));
    }

    private static DriverResult<ModbusCompiledReadPlan> Failure(string code, string message, string action)
    {
        return DriverResult<ModbusCompiledReadPlan>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Protocol: "Modbus TCP",
            Cause: "The requested tag set cannot be compiled into a safe Modbus read plan.",
            SuggestedAction: action,
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }

    private readonly record struct Range(ModbusReadPlanItem Item, ushort Start, int EndExclusive);
}
