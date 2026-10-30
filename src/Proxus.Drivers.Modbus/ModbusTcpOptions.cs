using Proxus.Drivers.Core;

namespace Proxus.Drivers.Modbus;

/// <summary>Connection options for <see cref="ModbusTcpClient" />.</summary>
public sealed record ModbusTcpOptions
{
    /// <summary>
    ///     Device or gateway host name or IP address. With <see cref="RtuStreamFactory" /> it is only a label
    ///     used in diagnostics (for example the serial port name) and may be empty.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>TCP port; ignored when <see cref="RtuStreamFactory" /> is set.</summary>
    public int Port { get; init; } = 502;

    /// <summary>
    ///     Default Unit ID (slave address). Modbus RTU and ASCII require 1 to 247; broadcast (0) is not
    ///     supported because it has no response to validate.
    /// </summary>
    public byte UnitId { get; init; } = 1;

    /// <summary>Wire framing. <see cref="ModbusFraming.Mbap" /> (Modbus TCP) is the default.</summary>
    public ModbusFraming Framing { get; init; } = ModbusFraming.Mbap;

    /// <summary>
    ///     Opens the byte stream for Modbus RTU or ASCII over a non-TCP transport, for example
    ///     <c>SerialPort.BaseStream</c> after the caller configured baud rate, data bits, parity and stop bits
    ///     (ASCII devices often use 7 data bits). The driver owns and disposes the returned stream and calls the
    ///     factory again after a transport failure. Requires <see cref="Framing" /> <see cref="ModbusFraming.Rtu" />
    ///     or <see cref="ModbusFraming.Ascii" />.
    /// </summary>
    public Func<CancellationToken, ValueTask<Stream>>? RtuStreamFactory { get; init; }

    /// <summary>
    ///     Minimum idle time between the end of one RTU frame and the start of the next request. On a serial
    ///     line set it to at least 3.5 character times (about 4 ms at 9600 baud; the specification fixes
    ///     1.75 ms above 19200 baud). Zero (default) suits RTU over TCP. At most one second. Not used by
    ///     Modbus ASCII, whose frames are delimited by ":" and CR LF.
    /// </summary>
    public TimeSpan RtuInterFrameDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool NoDelay { get; init; } = true;
    public bool EnableTcpKeepAlive { get; init; } = true;

    internal DriverError? Validate()
    {
        if (Framing is not (ModbusFraming.Mbap or ModbusFraming.Rtu or ModbusFraming.Ascii))
            return Configuration("MODBUS_FRAMING", $"Unknown Modbus framing {(byte)Framing}.");

        if (RtuStreamFactory is not null && Framing == ModbusFraming.Mbap)
            return Configuration("MODBUS_RTU_STREAM_FRAMING", "RtuStreamFactory requires Framing Rtu or Ascii.");

        if (RtuStreamFactory is null && string.IsNullOrWhiteSpace(Host))
            return Configuration("MODBUS_HOST", "Host is required.");

        if (RtuStreamFactory is null && Port is < 1 or > 65_535)
            return Configuration("MODBUS_PORT", "Port must be between 1 and 65535.");

        if (Framing == ModbusFraming.Rtu && UnitId is 0 or > ModbusRtu.MaximumUnitId)
            return Configuration("MODBUS_RTU_UNIT_ID",
                $"Modbus RTU UnitId must be between 1 and {ModbusRtu.MaximumUnitId}; broadcast is not supported.");

        if (Framing == ModbusFraming.Ascii && UnitId is 0 or > ModbusRtu.MaximumUnitId)
            return Configuration("MODBUS_ASCII_UNIT_ID",
                $"Modbus ASCII UnitId must be between 1 and {ModbusRtu.MaximumUnitId}; broadcast is not supported.");

        if (RtuInterFrameDelay < TimeSpan.Zero || RtuInterFrameDelay > TimeSpan.FromSeconds(1))
            return Configuration("MODBUS_RTU_INTER_FRAME_DELAY",
                "RtuInterFrameDelay must be between zero and one second.");

        if (ConnectTimeout == Timeout.InfiniteTimeSpan || ConnectTimeout <= TimeSpan.Zero ||
            ConnectTimeout > TimeoutTokenScope.MaximumSupportedTimeout)
            return Configuration(
                "MODBUS_CONNECT_TIMEOUT",
                $"ConnectTimeout must be positive and no greater than {TimeoutTokenScope.MaximumSupportedTimeout}.");

        if (RequestTimeout != Timeout.InfiniteTimeSpan &&
            (RequestTimeout <= TimeSpan.Zero || RequestTimeout > TimeoutTokenScope.MaximumSupportedTimeout))
            return Configuration(
                "MODBUS_REQUEST_TIMEOUT",
                $"RequestTimeout must be positive, infinite, or no greater than {TimeoutTokenScope.MaximumSupportedTimeout}.");

        return null;
    }

    internal string ProtocolName => Framing switch
    {
        ModbusFraming.Rtu => "Modbus RTU",
        ModbusFraming.Ascii => "Modbus ASCII",
        _ => "Modbus TCP"
    };

    private DriverError Configuration(string code, string message)
    {
        return new DriverError(
            DriverErrorKind.Configuration, code, message,
            Operation: DriverOperation.Validate,
            Protocol: ProtocolName,
            Cause: "The driver configuration is invalid and no network operation should be attempted.",
            SuggestedAction: "Correct the configuration value shown in the error and retry after reconfiguration.",
            Retry: DriverRetryDisposition.FixConfiguration,
            Connection: DriverConnectionDisposition.Unchanged);
    }
}
