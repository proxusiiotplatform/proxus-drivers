namespace Proxus.Drivers.Modbus;

public enum ModbusObjectType : byte
{
    Coil = 1,
    DiscreteInput = 2,
    HoldingRegister = 3,
    InputRegister = 4
}

internal enum ModbusExceptionCode : byte
{
    IllegalFunction = 1,
    IllegalDataAddress = 2,
    IllegalDataValue = 3,
    ServerDeviceFailure = 4,
    Acknowledge = 5,
    ServerDeviceBusy = 6,
    MemoryParityError = 8,
    GatewayPathUnavailable = 10,
    GatewayTargetDeviceFailedToRespond = 11
}

/// <summary>Standard MODBUS diagnostics (FC08) sub-functions.</summary>
public enum ModbusDiagnosticSubFunction : ushort
{
    ReturnQueryData = 0x0000,
    RestartCommunicationsOption = 0x0001,
    ReturnDiagnosticRegister = 0x0002,
    ChangeAsciiInputDelimiter = 0x0003,
    ForceListenOnlyMode = 0x0004,
    ClearCountersAndDiagnosticRegister = 0x000A,
    ReturnBusMessageCount = 0x000B,
    ReturnBusCommunicationErrorCount = 0x000C,
    ReturnBusExceptionErrorCount = 0x000D,
    ReturnServerMessageCount = 0x000E,
    ReturnServerNoResponseCount = 0x000F,
    ReturnServerNakCount = 0x0010,
    ReturnServerBusyCount = 0x0011,
    ReturnBusCharacterOverrunCount = 0x0012,
    ClearOverrunCounterAndFlag = 0x0014
}

/// <summary>MODBUS MEI (Encapsulated Interface Transport, FC2B) type.</summary>
public enum ModbusMeiType : byte
{
    CanOpenGeneralReference = 0x0D,
    ReadDeviceIdentification = 0x0E
}

/// <summary>Standard Read Device Identification access level.</summary>
public enum ModbusDeviceIdReadCode : byte
{
    Basic = 0x01,
    Regular = 0x02,
    Extended = 0x03,
    Specific = 0x04
}

/// <summary>Device Identification conformity level, including individual-access support.</summary>
public enum ModbusDeviceIdConformity : byte
{
    Basic = 0x01,
    Regular = 0x02,
    Extended = 0x03,
    BasicWithIndividualAccess = 0x81,
    RegularWithIndividualAccess = 0x82,
    ExtendedWithIndividualAccess = 0x83
}

public enum ModbusByteLane : byte
{
    Low = 0,
    High = 1
}

public enum ModbusByteWriteBehavior : byte
{
    Reject = 0,
    ZeroOtherLane = 1,
    PreserveOtherLane = 2
}

public enum ModbusStringEncoding : byte
{
    Ascii = 0,
    Utf8 = 1,
    Utf16LittleEndian = 2,
    Utf16BigEndian = 3
}

/// <summary>String framing/padding convention inside the fixed Modbus register span.</summary>
public enum ModbusStringTermination : byte
{
    FixedWidth = 0,
    NullTerminated = 1,
    SpacePadded = 2,
    ByteLengthPrefixed8 = 3,
    ByteLengthPrefixed16BigEndian = 4
}

/// <summary>Explicit vendor-selected wire width/epoch for semantic temporal values.</summary>
public enum ModbusTemporalFormat : byte
{
    None = 0,
    UnixSecondsInt32 = 1,
    UnixSecondsUInt32 = 2,
    UnixSecondsInt64 = 3,
    UnixMillisecondsInt64 = 4,
    DotNetTicksInt64 = 5,
    DurationMillisecondsInt32 = 6,
    DurationMillisecondsInt64 = 7,
    DurationTicksInt64 = 8,
    DateDaysSinceUnixInt32 = 9,
    DateDaysSince1990UInt16 = 10,
    TimeOfDayMillisecondsUInt32 = 11,
    TimeOfDaySecondsUInt32 = 12,
    TimeOfDayNanosecondsUInt64 = 13
}

/// <summary>Explicit fixed-point representation for Decimal because Modbus defines no decimal scalar type.</summary>
public enum ModbusDecimalFormat : byte
{
    None = 0,
    ScaledInt16 = 1,
    ScaledUInt16 = 2,
    ScaledInt32 = 3,
    ScaledUInt32 = 4,
    ScaledInt64 = 5,
    ScaledUInt64 = 6,

    /// <summary>Unsigned packed BCD digits; DecimalScale places the decimal point logically.</summary>
    PackedBcd16 = 7,
    PackedBcd32 = 8,
    PackedBcd64 = 9
}

/// <summary>Guid byte layout before the normal Modbus register-layout transform is applied.</summary>
public enum ModbusGuidFormat : byte
{
    None = 0,
    Rfc4122NetworkOrder = 1,
    DotNetMixedEndian = 2
}

/// <summary>How Modbus PDUs are framed on the wire.</summary>
public enum ModbusFraming : byte
{
    /// <summary>Modbus TCP: MBAP header with transaction identifier (Modbus Messaging on TCP/IP).</summary>
    Mbap = 0,

    /// <summary>
    ///     Modbus RTU: Unit ID, PDU and CRC-16 (Modbus over Serial Line). Used over a TCP socket for
    ///     transparent serial gateways ("RTU over TCP") or over a caller-supplied stream such as a serial port.
    /// </summary>
    Rtu = 1,

    /// <summary>
    ///     Modbus ASCII: ":", Unit ID, PDU and LRC as hexadecimal characters, then CR LF (Modbus over Serial
    ///     Line). Used over a caller-supplied stream such as a serial port, or over a TCP socket for serial
    ///     gateways that pass ASCII frames through.
    /// </summary>
    Ascii = 2
}
