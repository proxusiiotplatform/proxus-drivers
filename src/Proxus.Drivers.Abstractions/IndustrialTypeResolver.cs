namespace Proxus.Drivers;

/// <summary>
///     Source-language/protocol naming dialect. A type token is never interpreted without
///     its dialect because names such as INT, BYTE and WORD do not carry the same semantics
///     in .NET, IEC 61131-3, CIP and OPC UA.
/// </summary>
public enum IndustrialTypeDialect : byte
{
    DotNet = 0,
    Iec61131 = 1,
    Siemens = 2,
    AllenBradleyCip = 3,
    OpcUa = 4
}

/// <summary>Canonical semantic result while retaining the original native spelling.</summary>
public readonly record struct IndustrialTypeResolution(
    IndustrialDataType DataType,
    IndustrialTypeDialect Dialect,
    string NativeType);

public static class IndustrialTypeResolver
{
    public static DriverResult<IndustrialTypeResolution> Resolve(
        string typeName,
        IndustrialTypeDialect dialect)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return Failure(dialect, "Type name is empty.");

        var native = typeName.Trim();
        var key = native.ToUpperInvariant(); // configuration/import path, never polling hot path
        var type = dialect switch
        {
            IndustrialTypeDialect.DotNet => ResolveDotNet(key),
            IndustrialTypeDialect.Iec61131 => ResolveIec(key),
            IndustrialTypeDialect.Siemens => ResolveSiemens(key),
            IndustrialTypeDialect.AllenBradleyCip => ResolveCip(key),
            IndustrialTypeDialect.OpcUa => ResolveOpcUa(key),
            _ => null
        };

        return type is { } resolved
            ? DriverResult<IndustrialTypeResolution>.Success(new IndustrialTypeResolution(resolved, dialect, native))
            : Failure(dialect, $"'{native}' is not mapped in the {dialect} type dialect.");
    }

    private static IndustrialDataType? ResolveDotNet(string key)
    {
        return key switch
        {
            "BOOL" or "BOOLEAN" or "SYSTEM.BOOLEAN" => IndustrialDataType.Bool,
            "SBYTE" or "SYSTEM.SBYTE" => IndustrialDataType.Int8,
            "BYTE" or "SYSTEM.BYTE" => IndustrialDataType.UInt8,
            "SHORT" or "INT16" or "SYSTEM.INT16" => IndustrialDataType.Int16,
            "USHORT" or "UINT16" or "SYSTEM.UINT16" => IndustrialDataType.UInt16,
            "INT" or "INT32" or "SYSTEM.INT32" => IndustrialDataType.Int32,
            "UINT" or "UINT32" or "SYSTEM.UINT32" => IndustrialDataType.UInt32,
            "LONG" or "INT64" or "SYSTEM.INT64" => IndustrialDataType.Int64,
            "ULONG" or "UINT64" or "SYSTEM.UINT64" => IndustrialDataType.UInt64,
            "INT128" or "SYSTEM.INT128" => IndustrialDataType.Int128,
            "UINT128" or "SYSTEM.UINT128" => IndustrialDataType.UInt128,
            "HALF" or "SYSTEM.HALF" => IndustrialDataType.Float16,
            "FLOAT" or "SINGLE" or "SYSTEM.SINGLE" => IndustrialDataType.Float32,
            "DOUBLE" or "SYSTEM.DOUBLE" => IndustrialDataType.Float64,
            "DECIMAL" or "SYSTEM.DECIMAL" => IndustrialDataType.Decimal,
            "CHAR" or "SYSTEM.CHAR" => IndustrialDataType.Char16,
            "STRING" or "SYSTEM.STRING" => IndustrialDataType.String,
            "BYTE[]" or "SYSTEM.BYTE[]" or "READONLYMEMORY<BYTE>" or "MEMORY<BYTE>" => IndustrialDataType.Bytes,
            "GUID" or "SYSTEM.GUID" => IndustrialDataType.Guid,
            "DATEONLY" or "SYSTEM.DATEONLY" => IndustrialDataType.Date,
            "TIMEONLY" or "SYSTEM.TIMEONLY" => IndustrialDataType.TimeOfDay,
            "DATETIME" or "SYSTEM.DATETIME" or "DATETIMEOFFSET" or "SYSTEM.DATETIMEOFFSET" => IndustrialDataType
                .DateTime,
            "TIMESPAN" or "SYSTEM.TIMESPAN" => IndustrialDataType.Duration,
            "ENUM" or "SYSTEM.ENUM" => IndustrialDataType.Enumeration,
            _ => null
        };
    }

    private static IndustrialDataType? ResolveIec(string key)
    {
        return key switch
        {
            "BOOL" => IndustrialDataType.Bool,
            "SINT" => IndustrialDataType.Int8,
            "USINT" => IndustrialDataType.UInt8,
            "BYTE" => IndustrialDataType.BitString8,
            "INT" => IndustrialDataType.Int16,
            "UINT" => IndustrialDataType.UInt16,
            "WORD" => IndustrialDataType.BitString16,
            "DINT" => IndustrialDataType.Int32,
            "UDINT" => IndustrialDataType.UInt32,
            "DWORD" => IndustrialDataType.BitString32,
            "LINT" => IndustrialDataType.Int64,
            "ULINT" => IndustrialDataType.UInt64,
            "LWORD" => IndustrialDataType.BitString64,
            "REAL" => IndustrialDataType.Float32,
            "LREAL" => IndustrialDataType.Float64,
            "CHAR" => IndustrialDataType.Char8,
            "WCHAR" => IndustrialDataType.Char16,
            "STRING" or "WSTRING" => IndustrialDataType.String,
            "DATE" => IndustrialDataType.Date,
            "TOD" or "TIME_OF_DAY" => IndustrialDataType.TimeOfDay,
            "DT" or "DATE_AND_TIME" => IndustrialDataType.DateTime,
            "TIME" or "LTIME" => IndustrialDataType.Duration,
            "ENUM" or "ENUMERATION" => IndustrialDataType.Enumeration,
            _ => null
        };
    }

    private static IndustrialDataType? ResolveSiemens(string key)
    {
        return key switch
        {
            "DTL" or "LDT" or "LDATE_AND_TIME" or "DATE_AND_TIME" or "DT" => IndustrialDataType.DateTime,
            "ENUM" => IndustrialDataType.Enumeration,
            "LTOD" => IndustrialDataType.TimeOfDay,
            _ => ResolveIec(key)
        };
    }

    private static IndustrialDataType? ResolveCip(string key)
    {
        return key switch
        {
            "BOOL" => IndustrialDataType.Bool,
            "SINT" => IndustrialDataType.Int8,
            "USINT" => IndustrialDataType.UInt8,
            "BYTE" => IndustrialDataType.BitString8,
            "INT" => IndustrialDataType.Int16,
            "UINT" => IndustrialDataType.UInt16,
            "WORD" => IndustrialDataType.BitString16,
            "DINT" => IndustrialDataType.Int32,
            "UDINT" => IndustrialDataType.UInt32,
            "DWORD" => IndustrialDataType.BitString32,
            "LINT" => IndustrialDataType.Int64,
            "ULINT" => IndustrialDataType.UInt64,
            "LWORD" => IndustrialDataType.BitString64,
            "REAL" => IndustrialDataType.Float32,
            "LREAL" => IndustrialDataType.Float64,
            "STRING" or "STRING2" or "SHORT_STRING" => IndustrialDataType.String,
            _ => null
        };
    }

    private static IndustrialDataType? ResolveOpcUa(string key)
    {
        return key switch
        {
            "BOOLEAN" => IndustrialDataType.Bool,
            "SBYTE" => IndustrialDataType.Int8,
            "BYTE" => IndustrialDataType.UInt8,
            "INT16" => IndustrialDataType.Int16,
            "UINT16" => IndustrialDataType.UInt16,
            "INT32" => IndustrialDataType.Int32,
            "UINT32" => IndustrialDataType.UInt32,
            "INT64" => IndustrialDataType.Int64,
            "UINT64" => IndustrialDataType.UInt64,
            "FLOAT" => IndustrialDataType.Float32,
            "DOUBLE" => IndustrialDataType.Float64,
            "DECIMAL" => IndustrialDataType.Decimal,
            "STRING" => IndustrialDataType.String,
            "STATUSCODE" => IndustrialDataType.StatusCode,
            "NODEID" => IndustrialDataType.NodeId,
            "EXPANDEDNODEID" => IndustrialDataType.ExpandedNodeId,
            "QUALIFIEDNAME" => IndustrialDataType.QualifiedName,
            "LOCALIZEDTEXT" => IndustrialDataType.LocalizedText,
            "VARIANT" => IndustrialDataType.Variant,
            "EXTENSIONOBJECT" => IndustrialDataType.ExtensionObject,
            "BYTESTRING" => IndustrialDataType.Bytes,
            "XMLELEMENT" => IndustrialDataType.XmlElement,
            "DATETIME" => IndustrialDataType.DateTime,
            "GUID" => IndustrialDataType.Guid,
            "DURATION" => IndustrialDataType.Duration,
            "ENUM" or "ENUMERATION" => IndustrialDataType.Enumeration,
            _ => null
        };
    }

    private static DriverResult<IndustrialTypeResolution> Failure(
        IndustrialTypeDialect dialect,
        string message)
    {
        return DriverResult<IndustrialTypeResolution>.Failure(new DriverError(
            DriverErrorKind.Configuration,
            "TYPE_NAME_UNSUPPORTED",
            message,
            Operation: DriverOperation.Validate,
            Protocol: dialect.ToString(),
            Cause: "The source type token is unknown in the selected type dialect.",
            SuggestedAction:
            "Select the correct source dialect or supply an explicit canonical type and protocol wire descriptor.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
