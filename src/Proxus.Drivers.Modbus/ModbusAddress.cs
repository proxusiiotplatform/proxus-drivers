using System.Globalization;

namespace Proxus.Drivers.Modbus;

/// <summary>
///     Compiled zero-based Modbus address. Parsing belongs to configuration time, not the
///     polling hot path.
/// </summary>
public readonly record struct ModbusAddress(
    ModbusObjectType ObjectType,
    ushort Offset,
    byte UnitId = 1)
{
    public static DriverResult<ModbusAddress> Parse(string text, byte defaultUnitId = 1)
    {
        if (TryParse(text, defaultUnitId, out var address, out var error))
            return DriverResult<ModbusAddress>.Success(address);

        return DriverResult<ModbusAddress>.Failure(new DriverError(
            DriverErrorKind.Configuration,
            "MODBUS_ADDRESS",
            error ?? "Invalid Modbus address."));
    }

    public static bool TryParse(
        string? text,
        byte defaultUnitId,
        out ModbusAddress address,
        out string? error)
    {
        address = default;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Address is empty.";
            return false;
        }

        var span = text.AsSpan().Trim();
        var unitId = defaultUnitId;
        var semicolon = span.IndexOf(';');
        if (semicolon >= 0)
        {
            var prefix = span[..semicolon].Trim();
            span = span[(semicolon + 1)..].Trim();
            if (!TryParseUnit(prefix, out unitId))
            {
                error = "Unit prefix must be 'unit=<0..255>' or 'u=<0..255>'.";
                return false;
            }
        }

        var colon = span.IndexOf(':');
        if (colon > 0)
        {
            var area = span[..colon].Trim();
            var offsetSpan = span[(colon + 1)..].Trim();
            if (!ushort.TryParse(offsetSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
            {
                error = "Explicit Modbus offsets must be zero-based UInt16 values.";
                return false;
            }

            if (!TryParseArea(area, out var objectType))
            {
                error = "Unknown Modbus area. Use coil, di, hr, or ir.";
                return false;
            }

            address = new ModbusAddress(objectType, offset, unitId);
            return true;
        }

        if (!uint.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out var reference))
        {
            error = "Address must use an explicit area such as hr:0 or reference notation such as 40001.";
            return false;
        }

        if (!TryParseReference(reference, out var type, out var zeroBased))
        {
            error = "Reference notation is outside the supported 0xxxx/1xxxx/3xxxx/4xxxx address space.";
            return false;
        }

        address = new ModbusAddress(type, zeroBased, unitId);
        return true;
    }

    public override string ToString()
    {
        var prefix = ObjectType switch
        {
            ModbusObjectType.Coil => "coil",
            ModbusObjectType.DiscreteInput => "di",
            ModbusObjectType.HoldingRegister => "hr",
            ModbusObjectType.InputRegister => "ir",
            _ => "unknown"
        };

        return $"unit={UnitId};{prefix}:{Offset}";
    }

    private static bool TryParseUnit(ReadOnlySpan<char> prefix, out byte unitId)
    {
        unitId = 0;
        var equals = prefix.IndexOf('=');
        if (equals <= 0)
            return false;

        var name = prefix[..equals].Trim();
        if (!name.Equals("unit", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("u", StringComparison.OrdinalIgnoreCase))
            return false;

        return byte.TryParse(
            prefix[(equals + 1)..].Trim(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out unitId);
    }

    private static bool TryParseArea(ReadOnlySpan<char> area, out ModbusObjectType objectType)
    {
        if (area.Equals("coil", StringComparison.OrdinalIgnoreCase) ||
            area.Equals("co", StringComparison.OrdinalIgnoreCase))
        {
            objectType = ModbusObjectType.Coil;
            return true;
        }

        if (area.Equals("di", StringComparison.OrdinalIgnoreCase) ||
            area.Equals("discrete", StringComparison.OrdinalIgnoreCase))
        {
            objectType = ModbusObjectType.DiscreteInput;
            return true;
        }

        if (area.Equals("hr", StringComparison.OrdinalIgnoreCase) ||
            area.Equals("holding", StringComparison.OrdinalIgnoreCase))
        {
            objectType = ModbusObjectType.HoldingRegister;
            return true;
        }

        if (area.Equals("ir", StringComparison.OrdinalIgnoreCase) ||
            area.Equals("input", StringComparison.OrdinalIgnoreCase))
        {
            objectType = ModbusObjectType.InputRegister;
            return true;
        }

        objectType = default;
        return false;
    }

    private static bool TryParseReference(
        uint reference,
        out ModbusObjectType objectType,
        out ushort zeroBased)
    {
        objectType = default;
        zeroBased = 0;

        uint baseValue;
        if (reference is >= 1 and <= 9_999)
        {
            objectType = ModbusObjectType.Coil;
            baseValue = 1;
        }
        else if (reference is >= 10_001 and <= 19_999)
        {
            objectType = ModbusObjectType.DiscreteInput;
            baseValue = 10_001;
        }
        else if (reference is >= 30_001 and <= 39_999)
        {
            objectType = ModbusObjectType.InputRegister;
            baseValue = 30_001;
        }
        else if (reference is >= 40_001 and <= 49_999)
        {
            objectType = ModbusObjectType.HoldingRegister;
            baseValue = 40_001;
        }
        else if (reference is >= 100_001 and <= 165_536)
        {
            objectType = ModbusObjectType.DiscreteInput;
            baseValue = 100_001;
        }
        else if (reference is >= 300_001 and <= 365_536)
        {
            objectType = ModbusObjectType.InputRegister;
            baseValue = 300_001;
        }
        else if (reference is >= 400_001 and <= 465_536)
        {
            objectType = ModbusObjectType.HoldingRegister;
            baseValue = 400_001;
        }
        else
        {
            return false;
        }

        zeroBased = checked((ushort)(reference - baseValue));
        return true;
    }
}
