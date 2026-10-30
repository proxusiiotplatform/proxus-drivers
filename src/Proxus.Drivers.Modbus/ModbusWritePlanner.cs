namespace Proxus.Drivers.Modbus;

public readonly record struct ModbusWritePlanItem(int SourceOffset, ModbusRegisterDescriptor Descriptor);

public readonly record struct ModbusWritePlanBinding(
    int SourceOffset,
    ushort RelativeOffset,
    ModbusRegisterDescriptor Descriptor);

public readonly record struct ModbusWriteBlock(
    ModbusObjectType ObjectType,
    byte UnitId,
    ushort StartOffset,
    ushort Quantity,
    int BindingOffset,
    ushort BindingCount);

public sealed class ModbusCompiledWritePlan
{
    internal ModbusCompiledWritePlan(ModbusWriteBlock[] blocks, ModbusWritePlanBinding[] bindings, int sourceValueCount)
    {
        Blocks = blocks;
        Bindings = bindings;
        SourceValueCount = sourceValueCount;
    }

    public ModbusWriteBlock[] Blocks { get; }
    public ModbusWritePlanBinding[] Bindings { get; }
    public int SourceValueCount { get; }
}

public static class ModbusWritePlanner
{
    public static DriverResult<ModbusCompiledWritePlan> Compile(ReadOnlySpan<ModbusWritePlanItem> items)
    {
        if (items.IsEmpty)
            return Failure("MODBUS_WRITE_PLAN_EMPTY", "At least one writable item is required.",
                "Provide one or more coils or holding-register values.");

        var sourceValueCount = 0;
        var ranges = new List<Range>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var descriptor = item.Descriptor;
            var validation = descriptor.ElementCount == 1 ? descriptor.ValidateScalar() : descriptor.ValidateArray();
            if (!validation.IsSuccess)
                return DriverResult<ModbusCompiledWritePlan>.Failure(validation.Error.WithContext(
                    "Modbus TCP", DriverOperation.Validate, address: descriptor.Address.ToString(),
                    dataType: descriptor.DataType,
                    retry: DriverRetryDisposition.FixConfiguration, connection: DriverConnectionDisposition.Unchanged));
            if (descriptor.Address.ObjectType is not (ModbusObjectType.Coil or ModbusObjectType.HoldingRegister))
                return Failure("MODBUS_WRITE_PLAN_READ_ONLY", $"{descriptor.Address.ObjectType} is read-only.",
                    "Use Coil or HoldingRegister for writes.");
            if (item.SourceOffset < 0)
                return Failure("MODBUS_WRITE_PLAN_SOURCE", "SourceOffset cannot be negative.",
                    "Assign non-negative source slots.");
            sourceValueCount = Math.Max(sourceValueCount, checked(item.SourceOffset + descriptor.ElementCount));

            var isBits = descriptor.Address.ObjectType == ModbusObjectType.Coil;
            var physicalWidth = 1;
            var maxElements = (int)ModbusProtocol.MaximumWriteBits;
            if (!isBits)
            {
                var width = descriptor.GetElementRegisterWidth();
                if (!width.IsSuccess) return DriverResult<ModbusCompiledWritePlan>.Failure(width.Error);
                physicalWidth = width.Value;
                if (physicalWidth > ModbusProtocol.MaximumWriteRegisters)
                    return Failure("MODBUS_WRITE_PLAN_ELEMENT_TOO_WIDE",
                        $"One {descriptor.DataType} element requires {physicalWidth} registers, above the {ModbusProtocol.MaximumWriteRegisters}-register write limit.",
                        "Use a narrower explicit wire representation or raw protocol-specific access.");
                maxElements = Math.Max(1, ModbusProtocol.MaximumWriteRegisters / physicalWidth);
            }

            var done = 0;
            while (done < descriptor.ElementCount)
            {
                var elementCount = Math.Min(maxElements, descriptor.ElementCount - done);
                var physicalCount = checked(elementCount * physicalWidth);
                var startInt = checked(descriptor.Address.Offset + done * physicalWidth);
                var end = checked(startInt + physicalCount);
                if (end > ushort.MaxValue + 1)
                    return Failure("MODBUS_WRITE_PLAN_RANGE",
                        "A planned write exceeds the UInt16 Modbus address space.",
                        "Correct the start address or reduce the element count.");
                var fragment = descriptor with
                {
                    Address = descriptor.Address with { Offset = checked((ushort)startInt) },
                    ElementCount = checked((ushort)elementCount)
                };
                ranges.Add(new Range(
                    new ModbusWritePlanItem(item.SourceOffset + done, fragment),
                    checked((ushort)startInt), end));
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

        // Overlapping physical writes are ambiguous after sorting/coalescing. Require the
        // caller to express intentional overwrite order as separate operations instead.
        for (var i = 1; i < ranges.Count; i++)
        {
            var a = ranges[i - 1];
            var b = ranges[i];
            if (a.Item.Descriptor.Address.UnitId == b.Item.Descriptor.Address.UnitId &&
                a.Item.Descriptor.Address.ObjectType == b.Item.Descriptor.Address.ObjectType &&
                b.Start < a.EndExclusive)
                return Failure("MODBUS_WRITE_PLAN_OVERLAP", "Two batch items write overlapping physical addresses.",
                    "Remove the overlap or issue intentionally ordered writes separately.");
        }

        var blocks = new List<ModbusWriteBlock>(ranges.Count);
        var bindings = new List<ModbusWritePlanBinding>(ranges.Count);
        var cursor = 0;
        while (cursor < ranges.Count)
        {
            var first = ranges[cursor];
            var type = first.Item.Descriptor.Address.ObjectType;
            var unit = first.Item.Descriptor.Address.UnitId;
            var start = first.Start;
            var end = first.EndExclusive;
            var bindingOffset = bindings.Count;
            var maxQuantity = type == ModbusObjectType.Coil
                ? ModbusProtocol.MaximumWriteBits
                : ModbusProtocol.MaximumWriteRegisters;

            while (cursor < ranges.Count)
            {
                var current = ranges[cursor];
                var address = current.Item.Descriptor.Address;
                if (address.UnitId != unit || address.ObjectType != type) break;
                if (bindings.Count > bindingOffset && current.Start != end) break; // no gaps on writes
                var proposedEnd = Math.Max(end, current.EndExclusive);
                if (proposedEnd - start > maxQuantity) break;
                bindings.Add(new ModbusWritePlanBinding(
                    current.Item.SourceOffset,
                    checked((ushort)(current.Start - start)),
                    current.Item.Descriptor));
                end = proposedEnd;
                cursor++;
            }

            blocks.Add(new ModbusWriteBlock(type, unit, start, checked((ushort)(end - start)),
                bindingOffset, checked((ushort)(bindings.Count - bindingOffset))));
        }

        return DriverResult<ModbusCompiledWritePlan>.Success(new ModbusCompiledWritePlan([.. blocks],
            [.. bindings], sourceValueCount));
    }

    private static DriverResult<ModbusCompiledWritePlan> Failure(string code, string message, string action)
    {
        return DriverResult<ModbusCompiledWritePlan>.Failure(new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate, Protocol: "Modbus TCP",
            Cause: "The requested writes cannot be compiled into an unambiguous Modbus write plan.",
            SuggestedAction: action, Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }

    private readonly record struct Range(ModbusWritePlanItem Item, ushort Start, int EndExclusive);
}
