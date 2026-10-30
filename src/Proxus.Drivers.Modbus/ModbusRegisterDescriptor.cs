namespace Proxus.Drivers.Modbus;

/// <summary>
///     Compiled typed Modbus description. Modbus standardizes bits and 16-bit registers, not
///     application scalar types, so semantic values that require vendor conventions (Decimal,
///     Guid, temporal values, strings and byte lanes) always carry explicit wire metadata.
/// </summary>
public readonly record struct ModbusRegisterDescriptor(
    ModbusAddress Address,
    IndustrialDataType DataType,
    ushort ElementCount = 1,
    ushort ByteLength = 0,
    ModbusRegisterLayout Layout = default,
    ModbusByteLane ByteLane = ModbusByteLane.Low,
    ModbusByteWriteBehavior ByteWriteBehavior = ModbusByteWriteBehavior.Reject,
    ModbusStringEncoding StringEncoding = ModbusStringEncoding.Ascii,
    ModbusStringTermination StringTermination = ModbusStringTermination.FixedWidth,
    ModbusTemporalFormat TemporalFormat = ModbusTemporalFormat.None,
    ushort RegisterBoolTrueValue = 1,
    bool TrimTrailingNulls = true,
    ModbusDecimalFormat DecimalFormat = ModbusDecimalFormat.None,
    byte DecimalScale = 0,
    ModbusGuidFormat GuidFormat = ModbusGuidFormat.None,
    IndustrialDataType EnumerationStorageType = IndustrialDataType.Int16)
{
    public DriverResult ValidateScalar()
    {
        if (ElementCount != 1)
            return Failure("MODBUS_SCALAR_REQUIRED",
                "This operation requires ElementCount=1. Use the typed-array API for multiple elements.");
        return ValidateCommon();
    }

    public DriverResult ValidateArray()
    {
        if (ElementCount == 0) return Failure("MODBUS_ELEMENT_COUNT", "ElementCount must be greater than zero.");
        return ValidateCommon();
    }

    public DriverResult<ushort> GetElementRegisterWidth()
    {
        var validation = ValidateCommon();
        if (!validation.IsSuccess) return DriverResult<ushort>.Failure(validation.Error);
        if (Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return DriverResult<ushort>.Success(1);

        var bytes = DataType switch
        {
            IndustrialDataType.Bool or IndustrialDataType.Int8 or IndustrialDataType.UInt8 or
                IndustrialDataType.Char8 or IndustrialDataType.BitString8 => 2,

            IndustrialDataType.Int16 or IndustrialDataType.UInt16 or IndustrialDataType.Float16 or
                IndustrialDataType.Char16 or IndustrialDataType.BitString16 => 2,

            IndustrialDataType.Int32 or IndustrialDataType.UInt32 or IndustrialDataType.Float32 or
                IndustrialDataType.BitString32 => 4,

            IndustrialDataType.Int64 or IndustrialDataType.UInt64 or IndustrialDataType.Float64 or
                IndustrialDataType.BitString64 => 8,

            IndustrialDataType.Int128 or IndustrialDataType.UInt128 => 16,
            IndustrialDataType.Decimal => GetDecimalByteWidth(DecimalFormat),
            IndustrialDataType.Guid => 16,
            IndustrialDataType.Enumeration => IndustrialDataTypeInfo.GetFixedWidthBytes(EnumerationStorageType),
            IndustrialDataType.Date or IndustrialDataType.TimeOfDay or IndustrialDataType.DateTime
                or IndustrialDataType.Duration => GetTemporalByteWidth(TemporalFormat),
            IndustrialDataType.String or IndustrialDataType.Bytes => ByteLength,
            _ => 0
        };
        if (bytes <= 0) return FailureWidth($"{DataType} requires an explicit valid wire format or ByteLength.");
        return DriverResult<ushort>.Success(checked((ushort)((bytes + 1) / 2)));
    }

    public DriverResult<ushort> GetRequiredRegisterCount()
    {
        var validation = ValidateArray();
        if (!validation.IsSuccess) return DriverResult<ushort>.Failure(validation.Error);
        if (Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
            return DriverResult<ushort>.Success(ElementCount);
        var width = GetElementRegisterWidth();
        if (!width.IsSuccess) return width;
        var total = checked((uint)width.Value * ElementCount);
        if (total > ushort.MaxValue)
            return FailureWidth(
                $"The logical value requires {total} registers, beyond the UInt16 Modbus address space.");
        return DriverResult<ushort>.Success((ushort)total);
    }

    public DriverResult<ushort> GetRequiredWriteRegisterCount()
    {
        return GetRequiredRegisterCount();
    }

    private DriverResult ValidateCommon()
    {
        if (ElementCount == 0) return Failure("MODBUS_ELEMENT_COUNT", "ElementCount must be greater than zero.");

        if (Address.ObjectType is ModbusObjectType.Coil or ModbusObjectType.DiscreteInput)
        {
            if (DataType != IndustrialDataType.Bool)
                return Failure("MODBUS_BIT_TYPE", "Bit areas support Bool values only.");
            if (ByteLength != 0 || TemporalFormat != ModbusTemporalFormat.None ||
                ByteLane != ModbusByteLane.Low || ByteWriteBehavior != ModbusByteWriteBehavior.Reject ||
                Layout != ModbusRegisterLayout.Standard || StringEncoding != ModbusStringEncoding.Ascii ||
                StringTermination != ModbusStringTermination.FixedWidth || !TrimTrailingNulls ||
                RegisterBoolTrueValue != 1 || DecimalFormat != ModbusDecimalFormat.None || DecimalScale != 0 ||
                GuidFormat != ModbusGuidFormat.None)
                return Failure("MODBUS_BIT_DESCRIPTOR",
                    "Bit descriptors cannot define register-layout, byte-lane, string, temporal, decimal, Guid or register-bool options.");
            return DriverResult.Success();
        }

        if (Address.ObjectType is not (ModbusObjectType.HoldingRegister or ModbusObjectType.InputRegister))
            return Failure("MODBUS_AREA", "Unknown Modbus object area.");

        if (DataType == IndustrialDataType.Bool && RegisterBoolTrueValue == 0)
            return Failure("MODBUS_BOOL_TRUE", "RegisterBoolTrueValue must be non-zero.");
        if (DataType != IndustrialDataType.Bool && RegisterBoolTrueValue != 1)
            return Failure("MODBUS_BOOL_OPTIONS", "RegisterBoolTrueValue is valid only for Bool values.");

        var byteLaneType = DataType is IndustrialDataType.Int8 or IndustrialDataType.UInt8 or
                               IndustrialDataType.Char8 or IndustrialDataType.BitString8 ||
                           (DataType == IndustrialDataType.Enumeration &&
                            EnumerationStorageType is IndustrialDataType.Int8 or IndustrialDataType.UInt8);
        if (!byteLaneType && (ByteLane != ModbusByteLane.Low || ByteWriteBehavior != ModbusByteWriteBehavior.Reject))
            return Failure("MODBUS_BYTE_OPTIONS",
                "ByteLane and ByteWriteBehavior are valid only for 8-bit scalar types.");

        if (DataType is not (IndustrialDataType.String or IndustrialDataType.Bytes) && ByteLength != 0)
            return Failure("MODBUS_BYTE_LENGTH", "ByteLength is valid only for String and Bytes.");

        if (DataType != IndustrialDataType.String &&
            (StringEncoding != ModbusStringEncoding.Ascii || StringTermination != ModbusStringTermination.FixedWidth ||
             !TrimTrailingNulls))
            return Failure("MODBUS_STRING_OPTIONS",
                "String encoding/termination options are valid only for String values.");

        if (DataType == IndustrialDataType.String)
        {
            if (ByteLength == 0) return Failure("MODBUS_STRING_LENGTH", "String requires a positive ByteLength.");
            var prefixBytes = StringTermination switch
            {
                ModbusStringTermination.ByteLengthPrefixed8 => 1,
                ModbusStringTermination.ByteLengthPrefixed16BigEndian => 2,
                _ => 0
            };
            if (ByteLength <= prefixBytes)
                return Failure("MODBUS_STRING_LENGTH",
                    "String ByteLength must leave room for framing and at least one payload byte.");
            if (StringEncoding is ModbusStringEncoding.Utf16LittleEndian or ModbusStringEncoding.Utf16BigEndian &&
                ((ByteLength - prefixBytes) & 1) != 0)
                return Failure("MODBUS_STRING_LENGTH",
                    "UTF-16 string payload capacity must contain whole 16-bit code units after framing bytes.");
        }

        if (DataType == IndustrialDataType.Bytes && ByteLength == 0)
            return Failure("MODBUS_BYTES_LENGTH", "Bytes requires a positive ByteLength.");

        if (IndustrialDataTypeInfo.IsTemporal(DataType))
        {
            if (!IsTemporalFormatCompatible(DataType, TemporalFormat))
                return Failure("MODBUS_TEMPORAL", $"{DataType} requires a compatible explicit ModbusTemporalFormat.");
        }
        else if (TemporalFormat != ModbusTemporalFormat.None)
        {
            return Failure("MODBUS_TEMPORAL", "TemporalFormat is valid only for temporal semantic values.");
        }

        if (DataType == IndustrialDataType.Decimal)
        {
            if (DecimalFormat == ModbusDecimalFormat.None)
                return Failure("MODBUS_DECIMAL_FORMAT", "Decimal requires an explicit fixed-point storage format.");
            if (DecimalScale > 28)
                return Failure("MODBUS_DECIMAL_SCALE", "DecimalScale must be between 0 and 28.");
        }
        else if (DecimalFormat != ModbusDecimalFormat.None || DecimalScale != 0)
        {
            return Failure("MODBUS_DECIMAL_FORMAT", "DecimalFormat and DecimalScale are valid only for Decimal.");
        }

        if (DataType == IndustrialDataType.Guid)
        {
            if (GuidFormat == ModbusGuidFormat.None)
                return Failure("MODBUS_GUID_FORMAT", "Guid requires an explicit Guid byte layout.");
        }
        else if (GuidFormat != ModbusGuidFormat.None)
        {
            return Failure("MODBUS_GUID_FORMAT", "GuidFormat is valid only for Guid.");
        }

        if (DataType == IndustrialDataType.Enumeration)
        {
            if (!IndustrialDataTypeInfo.IsEnumerationStorageType(EnumerationStorageType))
                return Failure("MODBUS_ENUM_STORAGE",
                    "Enumeration requires Int8/UInt8/Int16/UInt16/Int32/UInt32/Int64/UInt64 storage.");
        }
        else if (EnumerationStorageType != IndustrialDataType.Int16)
        {
            return Failure("MODBUS_ENUM_STORAGE", "EnumerationStorageType is valid only for Enumeration values.");
        }

        return DriverResult.Success();
    }

    internal static bool IsTemporalFormatCompatible(IndustrialDataType type, ModbusTemporalFormat format)
    {
        return type switch
        {
            IndustrialDataType.DateTime => format is ModbusTemporalFormat.UnixSecondsInt32
                or ModbusTemporalFormat.UnixSecondsUInt32 or
                ModbusTemporalFormat.UnixSecondsInt64 or ModbusTemporalFormat.UnixMillisecondsInt64
                or ModbusTemporalFormat.DotNetTicksInt64,
            IndustrialDataType.Duration => format is ModbusTemporalFormat.DurationMillisecondsInt32 or
                ModbusTemporalFormat.DurationMillisecondsInt64 or ModbusTemporalFormat.DurationTicksInt64,
            IndustrialDataType.Date => format is ModbusTemporalFormat.DateDaysSinceUnixInt32
                or ModbusTemporalFormat.DateDaysSince1990UInt16,
            IndustrialDataType.TimeOfDay => format is ModbusTemporalFormat.TimeOfDayMillisecondsUInt32 or
                ModbusTemporalFormat.TimeOfDaySecondsUInt32 or ModbusTemporalFormat.TimeOfDayNanosecondsUInt64,
            _ => format == ModbusTemporalFormat.None
        };
    }

    internal static int GetTemporalByteWidth(ModbusTemporalFormat format)
    {
        return format switch
        {
            ModbusTemporalFormat.DateDaysSince1990UInt16 => 2,
            ModbusTemporalFormat.UnixSecondsInt32 or ModbusTemporalFormat.UnixSecondsUInt32 or
                ModbusTemporalFormat.DurationMillisecondsInt32 or ModbusTemporalFormat.DateDaysSinceUnixInt32 or
                ModbusTemporalFormat.TimeOfDayMillisecondsUInt32 or ModbusTemporalFormat.TimeOfDaySecondsUInt32 => 4,
            ModbusTemporalFormat.UnixSecondsInt64 or ModbusTemporalFormat.UnixMillisecondsInt64 or
                ModbusTemporalFormat.DotNetTicksInt64 or ModbusTemporalFormat.DurationMillisecondsInt64 or
                ModbusTemporalFormat.DurationTicksInt64 or ModbusTemporalFormat.TimeOfDayNanosecondsUInt64 => 8,
            _ => 0
        };
    }

    internal static int GetDecimalByteWidth(ModbusDecimalFormat format)
    {
        return format switch
        {
            ModbusDecimalFormat.ScaledInt16 or ModbusDecimalFormat.ScaledUInt16 => 2,
            ModbusDecimalFormat.ScaledInt32 or ModbusDecimalFormat.ScaledUInt32 => 4,
            ModbusDecimalFormat.ScaledInt64 or ModbusDecimalFormat.ScaledUInt64 => 8,
            ModbusDecimalFormat.PackedBcd16 => 2,
            ModbusDecimalFormat.PackedBcd32 => 4,
            ModbusDecimalFormat.PackedBcd64 => 8,
            _ => 0
        };
    }

    private static DriverResult Failure(string code, string message)
    {
        return DriverResult.Failure(new DriverError(DriverErrorKind.Configuration, code, message));
    }

    private static DriverResult<ushort> FailureWidth(string message)
    {
        return DriverResult<ushort>.Failure(
            new DriverError(DriverErrorKind.Configuration, "MODBUS_DESCRIPTOR", message));
    }
}
