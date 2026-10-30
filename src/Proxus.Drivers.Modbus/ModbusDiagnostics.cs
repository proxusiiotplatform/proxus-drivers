namespace Proxus.Drivers.Modbus;

internal static class ModbusDiagnostics
{
    public static DriverError DeviceException(byte code)
    {
        var (meaning, cause, action, retry) = (ModbusExceptionCode)code switch
        {
            ModbusExceptionCode.IllegalFunction => ("Illegal Function.",
                "The target does not support the requested Modbus function for this endpoint.",
                "Verify the configured object area and that the device supports the requested read/write function.",
                DriverRetryDisposition.FixConfiguration),
            ModbusExceptionCode.IllegalDataAddress => ("Illegal Data Address.",
                "The start address, quantity, or addressed object is not valid in the target device.",
                "Check the zero/one-based address mapping, object area, start offset, and requested element count against the device register map.",
                DriverRetryDisposition.FixConfiguration),
            ModbusExceptionCode.IllegalDataValue => ("Illegal Data Value.",
                "The request contains a quantity or value the device does not accept.",
                "Check the configured data type, array length, write value, and device-specific limits.",
                DriverRetryDisposition.FixConfiguration),
            ModbusExceptionCode.ServerDeviceFailure => ("Server Device Failure.",
                "The device reported an internal failure while executing the request.",
                "Inspect the PLC/device diagnostics and retry after the device fault is cleared.",
                DriverRetryDisposition.RetrySameConnection),
            ModbusExceptionCode.Acknowledge => ("Acknowledge.",
                "The device accepted a long-running request but has not completed it yet.",
                "Wait briefly and retry the operation according to the device documentation.",
                DriverRetryDisposition.RetrySameConnection),
            ModbusExceptionCode.ServerDeviceBusy => ("Server Device Busy.",
                "The device is temporarily unable to process the request.",
                "Retry with backoff; reduce polling pressure if this repeats.",
                DriverRetryDisposition.RetrySameConnection),
            ModbusExceptionCode.MemoryParityError => ("Memory Parity Error.",
                "The device detected a memory/parity problem while servicing the request.",
                "Inspect device hardware diagnostics before retrying repeatedly.",
                DriverRetryDisposition.RetrySameConnection),
            ModbusExceptionCode.GatewayPathUnavailable => ("Gateway Path Unavailable.",
                "A Modbus gateway could not route the request to the target path.",
                "Verify gateway routing, downstream network configuration, and Unit ID.",
                DriverRetryDisposition.FixConfiguration),
            ModbusExceptionCode.GatewayTargetDeviceFailedToRespond => ("Gateway Target Device Failed to Respond.",
                "The gateway could not obtain a response from the downstream target.",
                "Check the downstream device power, link, serial settings, Unit ID, and gateway timeout configuration.",
                DriverRetryDisposition.RetrySameConnection),
            _ => ($"Unknown Modbus exception 0x{code:X2}.",
                "The target returned an exception code not recognized by this driver version.",
                "Consult the device vendor's Modbus exception documentation for this code.",
                DriverRetryDisposition.DoNotRetry)
        };

        return new DriverError(
            DriverErrorKind.Device,
            "MODBUS_EXCEPTION",
            $"Device rejected the Modbus request with exception 0x{code:X2}.",
            code,
            NativeMeaning: meaning,
            Cause: cause,
            SuggestedAction: action,
            Retry: retry,
            Connection: DriverConnectionDisposition.Healthy);
    }
}
