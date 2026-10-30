namespace Proxus.Drivers;

/// <summary>One symbolic member of a PLC/industrial enumeration.</summary>
public readonly record struct DriverEnumerationMember(string Name, DriverValue Value);

/// <summary>
///     Immutable, AOT-safe symbolic enumeration metadata. It deliberately stays separate from
///     protocol wire descriptors: the schema owns names, while Modbus/S7 descriptors own the
///     signedness and storage width. Construction is a configuration-path operation; lookup is
///     allocation-free and optimized for the small member counts typical of controller enums.
/// </summary>
public sealed class DriverEnumerationSchema
{
    private readonly DriverEnumerationMember[] _members;

    private DriverEnumerationSchema(string name, DriverEnumerationMember[] members)
    {
        Name = name;
        _members = members;
    }

    public string Name { get; }
    public ReadOnlyMemory<DriverEnumerationMember> Members => _members;

    public static DriverResult<DriverEnumerationSchema> Create(
        string name,
        ReadOnlySpan<DriverEnumerationMember> members)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Failure("ENUM_SCHEMA_NAME", "Enumeration schema name cannot be empty.");
        if (members.IsEmpty)
            return Failure("ENUM_SCHEMA_EMPTY", "Enumeration schema must contain at least one member.");

        var copy = members.ToArray();
        for (var i = 0; i < copy.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(copy[i].Name))
                return Failure("ENUM_MEMBER_NAME", $"Enumeration member {i} has an empty name.");
            if (copy[i].Value.Type != IndustrialDataType.Enumeration)
                return Failure("ENUM_MEMBER_TYPE",
                    $"Enumeration member '{copy[i].Name}' contains {copy[i].Value.Type}, not Enumeration.");

            for (var j = 0; j < i; j++)
            {
                if (string.Equals(copy[i].Name, copy[j].Name, StringComparison.Ordinal))
                    return Failure("ENUM_MEMBER_DUPLICATE_NAME",
                        $"Enumeration member name '{copy[i].Name}' is duplicated.");
                if (copy[i].Value == copy[j].Value)
                    return Failure("ENUM_MEMBER_DUPLICATE_VALUE",
                        $"Enumeration value {copy[i].Value} is assigned to both '{copy[j].Name}' and '{copy[i].Name}'.");
            }
        }

        return DriverResult<DriverEnumerationSchema>.Success(new DriverEnumerationSchema(name.Trim(), copy));
    }

    public bool TryGetName(DriverValue value, out string name)
    {
        if (value.Type == IndustrialDataType.Enumeration)
            for (var i = 0; i < _members.Length; i++)
                if (_members[i].Value == value)
                {
                    name = _members[i].Name;
                    return true;
                }

        name = string.Empty;
        return false;
    }

    public bool TryGetValue(string name, out DriverValue value)
    {
        for (var i = 0; i < _members.Length; i++)
            if (string.Equals(_members[i].Name, name, StringComparison.Ordinal))
            {
                value = _members[i].Value;
                return true;
            }

        value = default;
        return false;
    }

    private static DriverResult<DriverEnumerationSchema> Failure(string code, string message)
    {
        return DriverResult<DriverEnumerationSchema>.Failure(new DriverError(
            DriverErrorKind.Configuration,
            code,
            message,
            Operation: DriverOperation.Validate,
            Cause: "The symbolic enumeration metadata is inconsistent.",
            SuggestedAction: "Use unique member names and unique Enumeration values, then rebuild the schema.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged));
    }
}
