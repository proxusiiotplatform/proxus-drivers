namespace Proxus.Drivers;

public enum DriverQualityLevel : byte
{
    Good = 0,
    Uncertain = 1,
    Bad = 2
}

[Flags]
public enum DriverQualityFlags : ushort
{
    None = 0,
    Stale = 1 << 0,
    Substituted = 1 << 1,
    OverRange = 1 << 2,
    UnderRange = 1 << 3,
    SensorFailure = 1 << 4,
    CommunicationFailure = 1 << 5,
    ConfigurationError = 1 << 6
}

/// <summary>Protocol-neutral quality plus an optional native status code.</summary>
public readonly record struct DriverQuality(
    DriverQualityLevel Level,
    DriverQualityFlags Flags = DriverQualityFlags.None,
    uint NativeStatusCode = 0)
{
    public static DriverQuality Good => new(DriverQualityLevel.Good);
    public bool IsGood => Level == DriverQualityLevel.Good && Flags == DriverQualityFlags.None;
}

/// <summary>One value with industrial quality/status and source/server timestamps.</summary>
public readonly record struct DriverSample(
    DriverValue Value,
    DriverQuality Quality,
    DateTime SourceTimestampUtc,
    DateTime ServerTimestampUtc,
    ushort SourcePicoseconds = 0,
    ushort ServerPicoseconds = 0)
{
    public static DriverSample Good(DriverValue value, DateTime sourceTimestampUtc, DateTime serverTimestampUtc)
    {
        if (sourceTimestampUtc.Kind != DateTimeKind.Utc || serverTimestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DriverSample timestamps must be UTC.");
        return new DriverSample(value, DriverQuality.Good, sourceTimestampUtc, serverTimestampUtc);
    }
}
