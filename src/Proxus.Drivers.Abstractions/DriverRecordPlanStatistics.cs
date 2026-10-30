namespace Proxus.Drivers;

/// <summary>Stable configuration-time metrics for a compiled record/batch plan.</summary>
// ReSharper disable once UnusedType.Global -- public result model returned by compiled Modbus and S7 record plans.
public readonly record struct DriverRecordPlanStatistics(
    int FieldCount,
    int ValueCount,
    int BlockCount,
    int PhysicalUnitCount,
    int BaselineReadBlockCount = 0);
