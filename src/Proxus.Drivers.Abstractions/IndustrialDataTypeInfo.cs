namespace Proxus.Drivers;

/// <summary>Central semantic classification for canonical industrial value types.</summary>
public static class IndustrialDataTypeInfo
{
    public static bool IsSignedInteger(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.Int8 or IndustrialDataType.Int16 or IndustrialDataType.Int32 or
            IndustrialDataType.Int64 or IndustrialDataType.Int128;
    }

    public static bool IsUnsignedInteger(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.UInt8 or IndustrialDataType.UInt16 or IndustrialDataType.UInt32 or
            IndustrialDataType.UInt64 or IndustrialDataType.UInt128;
    }

    public static bool IsInteger(IndustrialDataType type)
    {
        return IsSignedInteger(type) || IsUnsignedInteger(type);
    }

    public static bool IsFloatingPoint(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.Float16 or IndustrialDataType.Float32 or IndustrialDataType.Float64;
    }

    public static bool IsNumeric(IndustrialDataType type)
    {
        return IsInteger(type) || IsFloatingPoint(type) || type == IndustrialDataType.Decimal;
    }

    public static bool IsBitString(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.BitString8 or IndustrialDataType.BitString16 or
            IndustrialDataType.BitString32 or IndustrialDataType.BitString64;
    }

    public static bool IsEnumeration(IndustrialDataType type)
    {
        return type == IndustrialDataType.Enumeration;
    }

    public static bool IsEnumerationStorageType(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.Int8 or IndustrialDataType.UInt8 or
            IndustrialDataType.Int16 or IndustrialDataType.UInt16 or
            IndustrialDataType.Int32 or IndustrialDataType.UInt32 or
            IndustrialDataType.Int64 or IndustrialDataType.UInt64;
    }

    public static bool IsUnsignedEnumerationStorageType(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.UInt8 or IndustrialDataType.UInt16 or
            IndustrialDataType.UInt32 or IndustrialDataType.UInt64;
    }

    public static bool IsTemporal(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.Date or IndustrialDataType.TimeOfDay or
            IndustrialDataType.DateTime or IndustrialDataType.Duration;
    }

    public static bool IsTextual(IndustrialDataType type)
    {
        return type is
            IndustrialDataType.Char8 or IndustrialDataType.Char16 or IndustrialDataType.String;
    }

    /// <summary>
    ///     Returns the canonical scalar payload width for fixed-width values. String and Bytes
    ///     return zero because their wire width must always be supplied by protocol metadata.
    ///     Bool returns one byte as an in-memory scalar; protocols may encode it as a bit.
    /// </summary>
    public static int GetFixedWidthBytes(IndustrialDataType type)
    {
        return type switch
        {
            IndustrialDataType.Bool or IndustrialDataType.Int8 or IndustrialDataType.UInt8 or
                IndustrialDataType.Char8 or IndustrialDataType.BitString8 => 1,

            IndustrialDataType.Int16 or IndustrialDataType.UInt16 or IndustrialDataType.Float16 or
                IndustrialDataType.Char16 or IndustrialDataType.BitString16 => 2,

            IndustrialDataType.Int32 or IndustrialDataType.UInt32 or IndustrialDataType.Float32 or
                IndustrialDataType.Date or IndustrialDataType.TimeOfDay or IndustrialDataType.BitString32 or
                IndustrialDataType.StatusCode => 4,

            IndustrialDataType.Int64 or IndustrialDataType.UInt64 or IndustrialDataType.Float64 or
                IndustrialDataType.DateTime or IndustrialDataType.Duration or IndustrialDataType.BitString64 => 8,

            IndustrialDataType.Int128 or IndustrialDataType.UInt128 or IndustrialDataType.Decimal or
                IndustrialDataType.Guid => 16,

            _ => 0
        };
    }
}
