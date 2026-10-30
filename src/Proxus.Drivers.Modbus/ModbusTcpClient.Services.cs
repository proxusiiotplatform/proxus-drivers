using System.Buffers.Binary;

namespace Proxus.Drivers.Modbus;

public sealed partial class ModbusTcpClient
{
    public async ValueTask<DriverResult<byte>> ReadExceptionStatusAsync(
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<byte>.Failure(Contextualize(gateError, DriverOperation.Diagnose));
        try
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 1);
            pdu[0] = ModbusProtocol.ReadExceptionStatus;
            var result = await ExecutePreparedPduCoreAsync(1, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<byte>.Failure(Contextualize(result.Error, DriverOperation.Diagnose));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReadExceptionStatus);
            if (error is { } e)
                return DriverResult<byte>.Failure(Contextualize(ObserveResponseError(e), DriverOperation.Diagnose));
            if (response.Length != 2)
                return DriverResult<byte>.Failure(ServiceProtocolError("MODBUS_FC07_LENGTH",
                    $"FC07 response must contain 2 bytes, received {response.Length}."));
            return DriverResult<byte>.Success(response[1]);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public ValueTask<DriverResult<ushort>> DiagnosticsAsync(
        ModbusDiagnosticSubFunction subFunction,
        ushort data = 0,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return DiagnosticsRawAsync((ushort)subFunction, data, unitId, cancellationToken);
    }

    /// <summary>Executes a raw FC08 sub-function for vendor-specific diagnostic extensions.</summary>
    public async ValueTask<DriverResult<ushort>> DiagnosticsRawAsync(
        ushort subFunction,
        ushort data,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ushort>.Failure(Contextualize(gateError, DriverOperation.Diagnose));
        try
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 5);
            pdu[0] = ModbusProtocol.Diagnostics;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], subFunction);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], data);
            var result = await ExecutePreparedPduCoreAsync(5, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ushort>.Failure(Contextualize(result.Error, DriverOperation.Diagnose));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.Diagnostics);
            if (error is { } e)
                return DriverResult<ushort>.Failure(Contextualize(ObserveResponseError(e), DriverOperation.Diagnose));
            if (response.Length != 5 || BinaryPrimitives.ReadUInt16BigEndian(response[1..]) != subFunction)
                return DriverResult<ushort>.Failure(ServiceProtocolError("MODBUS_FC08_ECHO",
                    "FC08 response did not echo the requested diagnostic sub-function."));
            return DriverResult<ushort>.Success(BinaryPrimitives.ReadUInt16BigEndian(response[3..]));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<ModbusCommEventCounter>> GetCommEventCounterAsync(
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ModbusCommEventCounter>.Failure(Contextualize(gateError, DriverOperation.Diagnose));
        try
        {
            _requestBuffer[ModbusProtocol.MbapHeaderLength] = ModbusProtocol.GetCommEventCounter;
            var result = await ExecutePreparedPduCoreAsync(1, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ModbusCommEventCounter>.Failure(Contextualize(result.Error,
                    DriverOperation.Diagnose));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.GetCommEventCounter);
            if (error is { } e)
                return DriverResult<ModbusCommEventCounter>.Failure(Contextualize(ObserveResponseError(e),
                    DriverOperation.Diagnose));
            if (response.Length != 5)
                return DriverResult<ModbusCommEventCounter>.Failure(ServiceProtocolError("MODBUS_FC0B_LENGTH",
                    $"FC0B response must contain 5 bytes, received {response.Length}."));
            return DriverResult<ModbusCommEventCounter>.Success(new ModbusCommEventCounter(
                BinaryPrimitives.ReadUInt16BigEndian(response[1..]),
                BinaryPrimitives.ReadUInt16BigEndian(response[3..])));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<ModbusCommEventLog>> GetCommEventLogAsync(
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ModbusCommEventLog>.Failure(Contextualize(gateError, DriverOperation.Diagnose));
        try
        {
            _requestBuffer[ModbusProtocol.MbapHeaderLength] = ModbusProtocol.GetCommEventLog;
            var result = await ExecutePreparedPduCoreAsync(1, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ModbusCommEventLog>.Failure(Contextualize(result.Error, DriverOperation.Diagnose));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.GetCommEventLog);
            if (error is { } e)
                return DriverResult<ModbusCommEventLog>.Failure(Contextualize(ObserveResponseError(e),
                    DriverOperation.Diagnose));
            if (response.Length < 8 || response[1] != response.Length - 2 || response.Length - 8 > 64)
                return DriverResult<ModbusCommEventLog>.Failure(ServiceProtocolError("MODBUS_FC0C_LENGTH",
                    "FC0C byte count is invalid or contains more than the standard maximum of 64 event bytes."));
            return DriverResult<ModbusCommEventLog>.Success(new ModbusCommEventLog(
                BinaryPrimitives.ReadUInt16BigEndian(response[2..]),
                BinaryPrimitives.ReadUInt16BigEndian(response[4..]),
                BinaryPrimitives.ReadUInt16BigEndian(response[6..]),
                [.. response[8..]]));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<ModbusServerId>> ReportServerIdAsync(
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ModbusServerId>.Failure(Contextualize(gateError, DriverOperation.Identify));
        try
        {
            _requestBuffer[ModbusProtocol.MbapHeaderLength] = ModbusProtocol.ReportServerId;
            var result = await ExecutePreparedPduCoreAsync(1, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ModbusServerId>.Failure(Contextualize(result.Error, DriverOperation.Identify));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReportServerId);
            if (error is { } e)
                return DriverResult<ModbusServerId>.Failure(Contextualize(ObserveResponseError(e),
                    DriverOperation.Identify));
            // V1.1b3 6.13: the Server ID length is device specific and the Run Indicator Status follows it,
            // so only the byte count can be validated. A one-byte Server ID is assumed only when the next
            // byte is a valid Run Indicator; otherwise the reply is returned uninterpreted in Data.
            if (response.Length < 3 || response[1] != response.Length - 2)
                return DriverResult<ModbusServerId>.Failure(ServiceProtocolError("MODBUS_FC11_LENGTH",
                    "FC11 byte count does not match the response or the Server ID is empty.", DriverOperation.Identify));
            var data = response[2..].ToArray();
            var oneByteLayout = data.Length >= 2 && data[1] is (0x00 or 0xFF);
            return DriverResult<ModbusServerId>.Success(oneByteLayout
                ? new ModbusServerId(data[0], data[1] == 0xFF, data[2..]) { Data = data }
                : new ModbusServerId(data[0], false, []) { Data = data, HasOneByteServerIdLayout = false });
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<int>> ReadFileRecordAsync(
        ushort fileNumber,
        ushort recordNumber,
        Memory<ushort> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (fileNumber == 0 || destination.IsEmpty || destination.Length > 121 || recordNumber > 9999 ||
            (uint)recordNumber + destination.Length > 10000)
            return DriverResult<int>.Failure(ConfigurationError("MODBUS_FILE_RANGE",
                "File-record read requires FileNumber 1..65535, RecordNumber 0..9999, and 1..121 sequential registers within the 10000-record file."));
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<int>.Failure(Contextualize(gateError, DriverOperation.ReadBlock));
        try
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 9);
            pdu[0] = ModbusProtocol.ReadFileRecord;
            pdu[1] = 7;
            pdu[2] = 6;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], fileNumber);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[5..], recordNumber);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[7..], checked((ushort)destination.Length));
            var result = await ExecutePreparedPduCoreAsync(9, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<int>.Failure(Contextualize(result.Error, DriverOperation.ReadBlock));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReadFileRecord);
            if (error is { } e)
                return DriverResult<int>.Failure(Contextualize(ObserveResponseError(e), DriverOperation.ReadBlock));
            var dataBytes = checked(destination.Length * 2);
            if (response.Length != 4 + dataBytes || response[1] != 2 + dataBytes || response[2] != 1 + dataBytes ||
                response[3] != 6)
                return DriverResult<int>.Failure(ServiceProtocolError("MODBUS_FC14_LENGTH",
                    "FC14 sub-response length/reference type is invalid.", DriverOperation.ReadBlock));
            for (var i = 0; i < destination.Length; i++)
                destination.Span[i] = BinaryPrimitives.ReadUInt16BigEndian(response[(4 + i * 2)..]);
            return DriverResult<int>.Success(destination.Length);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteFileRecordAsync(
        ushort fileNumber,
        ushort recordNumber,
        ReadOnlyMemory<ushort> values,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (fileNumber == 0 || values.IsEmpty || values.Length > 122 || recordNumber > 9999 ||
            (uint)recordNumber + values.Length > 10000)
            return DriverResult.Failure(ConfigurationError("MODBUS_FILE_RANGE",
                "File-record write requires FileNumber 1..65535, RecordNumber 0..9999, and 1..122 sequential registers within the 10000-record file."));
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError) return DriverResult.Failure(Contextualize(gateError, DriverOperation.WriteBlock));
        try
        {
            var byteCount = checked(7 + values.Length * 2);
            var pduLength = 2 + byteCount;
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.WriteFileRecord;
            pdu[1] = checked((byte)byteCount);
            pdu[2] = 6;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[3..], fileNumber);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[5..], recordNumber);
            BinaryPrimitives.WriteUInt16BigEndian(pdu[7..], checked((ushort)values.Length));
            for (var i = 0; i < values.Length; i++)
                BinaryPrimitives.WriteUInt16BigEndian(pdu[(9 + i * 2)..], values.Span[i]);
            var expected = pdu.ToArray(); // service path; preserves request for exact echo validation
            var result = await ExecutePreparedPduCoreAsync(pduLength, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess) return DriverResult.Failure(Contextualize(result.Error, DriverOperation.WriteBlock));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteFileRecord);
            if (error is { } e)
                return DriverResult.Failure(Contextualize(ObserveResponseError(e), DriverOperation.WriteBlock));
            if (!response.SequenceEqual(expected))
                return DriverResult.Failure(ServiceProtocolError("MODBUS_FC15_ECHO",
                    "FC15 response did not echo the validated file-record request.", DriverOperation.WriteBlock));
            return DriverResult.Success();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<int>> ReadFifoQueueAsync(
        ushort fifoPointerAddress,
        Memory<ushort> destination,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<int>.Failure(Contextualize(gateError, DriverOperation.ReadBlock));
        try
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 3);
            pdu[0] = ModbusProtocol.ReadFifoQueue;
            BinaryPrimitives.WriteUInt16BigEndian(pdu[1..], fifoPointerAddress);
            var result = await ExecutePreparedPduCoreAsync(3, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<int>.Failure(Contextualize(result.Error, DriverOperation.ReadBlock));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReadFifoQueue);
            if (error is { } e)
                return DriverResult<int>.Failure(Contextualize(ObserveResponseError(e), DriverOperation.ReadBlock));
            if (response.Length < 5)
                return DriverResult<int>.Failure(ServiceProtocolError("MODBUS_FC18_LENGTH",
                    "FC18 response is truncated.", DriverOperation.ReadBlock));
            var byteCount = BinaryPrimitives.ReadUInt16BigEndian(response[1..]);
            var count = BinaryPrimitives.ReadUInt16BigEndian(response[3..]);
            if (count > 31 || byteCount != 2 + count * 2 || response.Length != 3 + byteCount)
                return DriverResult<int>.Failure(ServiceProtocolError("MODBUS_FC18_COUNT",
                    "FC18 FIFO byte count/item count is invalid.", DriverOperation.ReadBlock));
            if (destination.Length < count)
                return DriverResult<int>.Failure(ConfigurationError("MODBUS_FIFO_DESTINATION",
                    $"FIFO response contains {count} registers; destination has {destination.Length}."));
            for (var i = 0; i < count; i++)
                destination.Span[i] = BinaryPrimitives.ReadUInt16BigEndian(response[(5 + i * 2)..]);
            return DriverResult<int>.Success(count);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<ModbusDeviceIdentification>> ReadDeviceIdentificationAsync(
        ModbusDeviceIdReadCode readDeviceIdCode = ModbusDeviceIdReadCode.Basic,
        byte objectId = 0,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if ((byte)readDeviceIdCode is < (byte)ModbusDeviceIdReadCode.Basic or > (byte)ModbusDeviceIdReadCode.Specific)
            return DriverResult<ModbusDeviceIdentification>.Failure(ConfigurationError("MODBUS_DEVICE_ID_CODE",
                "Read Device ID code must be Basic, Regular, Extended, or Specific."));
        var readCode = (byte)readDeviceIdCode;
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ModbusDeviceIdentification>.Failure(Contextualize(gateError, DriverOperation.Identify));
        try
        {
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, 4);
            pdu[0] = ModbusProtocol.EncapsulatedInterfaceTransport;
            pdu[1] = ModbusProtocol.MeiReadDeviceIdentification;
            pdu[2] = readCode;
            pdu[3] = objectId;
            var result = await ExecutePreparedPduCoreAsync(4, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ModbusDeviceIdentification>.Failure(Contextualize(result.Error,
                    DriverOperation.Identify));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response,
                ModbusProtocol.EncapsulatedInterfaceTransport);
            if (error is { } e)
                return DriverResult<ModbusDeviceIdentification>.Failure(Contextualize(ObserveResponseError(e),
                    DriverOperation.Identify));
            if (response.Length < 7 || response[1] != ModbusProtocol.MeiReadDeviceIdentification ||
                response[2] != readCode ||
                response[3] is not (0x01 or 0x02 or 0x03 or 0x81 or 0x82 or 0x83) || response[4] is not (0x00 or 0xFF))
                return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_HEADER",
                    "MEI Read Device Identification response header/read-code/conformity/more-follows fields are invalid.",
                    DriverOperation.Identify));
            var count = response[6];
            var objects = new ModbusDeviceIdObject[count];
            var offset = 7;
            for (var i = 0; i < count; i++)
            {
                if (offset + 2 > response.Length)
                    return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_OBJECT",
                        "Device identification object header is truncated.", DriverOperation.Identify));
                var id = response[offset++];
                var len = response[offset++];
                if (offset + len > response.Length)
                    return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_OBJECT",
                        $"Device identification object 0x{id:X2} is truncated.", DriverOperation.Identify));
                objects[i] = new ModbusDeviceIdObject(id, [.. response.Slice(offset, len)]);
                offset += len;
            }

            if (offset != response.Length)
                return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_TRAILING",
                    "Device identification response contains unexpected trailing bytes.", DriverOperation.Identify));
            return DriverResult<ModbusDeviceIdentification>.Success(new ModbusDeviceIdentification(
                (ModbusDeviceIdReadCode)response[2],
                (ModbusDeviceIdConformity)response[3],
                response[4] != 0,
                response[5],
                objects));
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<ModbusFileRecordReadResult[]>> ReadFileRecordsAsync(
        ReadOnlyMemory<ModbusFileRecordReadRequest> requests,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (requests.IsEmpty)
            return DriverResult<ModbusFileRecordReadResult[]>.Failure(ConfigurationError("MODBUS_FILE_REQUESTS",
                "At least one file-record read sub-request is required."));

        var requestByteCount = checked(requests.Length * 7);
        var responseDataLength = 0;
        foreach (var request in requests.Span)
        {
            if (request.FileNumber == 0 || request.RecordLength == 0 || request.RecordNumber > 9999 ||
                (uint)request.RecordNumber + request.RecordLength > 10000)
                return DriverResult<ModbusFileRecordReadResult[]>.Failure(ConfigurationError("MODBUS_FILE_RANGE",
                    "Each file-record read sub-request requires FileNumber 1..65535 and a non-empty RecordNumber/RecordLength range within 0..9999."));
            responseDataLength = checked(responseDataLength + 2 + request.RecordLength * 2);
        }

        if (requestByteCount > 0xF5 || responseDataLength > 0xF5)
            return DriverResult<ModbusFileRecordReadResult[]>.Failure(ConfigurationError("MODBUS_FILE_PDU",
                "Combined FC14 sub-requests exceed the MODBUS 253-byte PDU / 0xF5 response-data limits. Split the request into multiple calls."));

        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError)
            return DriverResult<ModbusFileRecordReadResult[]>.Failure(Contextualize(gateError,
                DriverOperation.ReadBlock));
        try
        {
            var pduLength = 2 + requestByteCount;
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.ReadFileRecord;
            pdu[1] = checked((byte)requestByteCount);
            var offset = 2;
            foreach (var request in requests.Span)
            {
                pdu[offset++] = 6;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], request.FileNumber);
                offset += 2;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], request.RecordNumber);
                offset += 2;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], request.RecordLength);
                offset += 2;
            }

            var result = await ExecutePreparedPduCoreAsync(pduLength, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<ModbusFileRecordReadResult[]>.Failure(Contextualize(result.Error,
                    DriverOperation.ReadBlock));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.ReadFileRecord);
            if (error is { } e)
                return DriverResult<ModbusFileRecordReadResult[]>.Failure(Contextualize(ObserveResponseError(e),
                    DriverOperation.ReadBlock));
            if (response.Length < 4 || response[1] != response.Length - 2)
                return DriverResult<ModbusFileRecordReadResult[]>.Failure(ServiceProtocolError("MODBUS_FC14_LENGTH",
                    "FC14 combined response data length does not match the PDU.", DriverOperation.ReadBlock));

            var outputs = new ModbusFileRecordReadResult[requests.Length];
            offset = 2;
            for (var r = 0; r < requests.Length; r++)
            {
                var request = requests.Span[r];
                var expectedSubLength = 1 + request.RecordLength * 2;
                if (offset + 2 > response.Length || response[offset++] != expectedSubLength ||
                    response[offset++] != 6 || offset + request.RecordLength * 2 > response.Length)
                    return DriverResult<ModbusFileRecordReadResult[]>.Failure(
                        ServiceProtocolError("MODBUS_FC14_SUBRESPONSE",
                            $"FC14 sub-response {r} length/reference type is invalid.", DriverOperation.ReadBlock));
                var values = new ushort[request.RecordLength];
                for (var i = 0; i < values.Length; i++)
                    values[i] = BinaryPrimitives.ReadUInt16BigEndian(response[(offset + i * 2)..]);
                offset += values.Length * 2;
                outputs[r] = new ModbusFileRecordReadResult(request.FileNumber, request.RecordNumber, values);
            }

            if (offset != response.Length)
                return DriverResult<ModbusFileRecordReadResult[]>.Failure(ServiceProtocolError("MODBUS_FC14_TRAILING",
                    "FC14 response contains unexpected trailing bytes.", DriverOperation.ReadBlock));
            return DriverResult<ModbusFileRecordReadResult[]>.Success(outputs);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult> WriteFileRecordsAsync(
        ReadOnlyMemory<ModbusFileRecordWriteRequest> requests,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (requests.IsEmpty)
            return DriverResult.Failure(ConfigurationError("MODBUS_FILE_REQUESTS",
                "At least one file-record write sub-request is required."));
        var requestByteCount = 0;
        foreach (var request in requests.Span)
        {
            if (request.FileNumber == 0 || request.Values.IsEmpty || request.RecordNumber > 9999 ||
                (uint)request.RecordNumber + request.Values.Length > 10000)
                return DriverResult.Failure(ConfigurationError("MODBUS_FILE_RANGE",
                    "Each file-record write sub-request requires FileNumber 1..65535 and a non-empty sequential range within RecordNumber 0..9999."));
            requestByteCount = checked(requestByteCount + 7 + request.Values.Length * 2);
        }

        if (requestByteCount > 0xFB)
            return DriverResult.Failure(ConfigurationError("MODBUS_FILE_PDU",
                "Combined FC15 sub-requests exceed the MODBUS 253-byte PDU / 0xFB request-data limits. Split the request into multiple calls."));

        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError) return DriverResult.Failure(Contextualize(gateError, DriverOperation.WriteBlock));
        try
        {
            var pduLength = 2 + requestByteCount;
            var pdu = _requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength, pduLength);
            pdu[0] = ModbusProtocol.WriteFileRecord;
            pdu[1] = checked((byte)requestByteCount);
            var offset = 2;
            foreach (var request in requests.Span)
            {
                pdu[offset++] = 6;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], request.FileNumber);
                offset += 2;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], request.RecordNumber);
                offset += 2;
                BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], checked((ushort)request.Values.Length));
                offset += 2;
                foreach (var value in request.Values.Span)
                {
                    BinaryPrimitives.WriteUInt16BigEndian(pdu[offset..], value);
                    offset += 2;
                }
            }

            var expected = pdu.ToArray();
            var result = await ExecutePreparedPduCoreAsync(pduLength, unitId ?? _options.UnitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess) return DriverResult.Failure(Contextualize(result.Error, DriverOperation.WriteBlock));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, ModbusProtocol.WriteFileRecord);
            if (error is { } e)
                return DriverResult.Failure(Contextualize(ObserveResponseError(e), DriverOperation.WriteBlock));
            if (!response.SequenceEqual(expected))
                return DriverResult.Failure(ServiceProtocolError("MODBUS_FC15_ECHO",
                    "FC15 response did not echo the complete multi-record write request.", DriverOperation.WriteBlock));
            return DriverResult.Success();
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public async ValueTask<DriverResult<int>> ExecuteRawPduAsync(
        ReadOnlyMemory<byte> requestPdu,
        Memory<byte> responsePdu,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestPdu.IsEmpty || requestPdu.Length > ModbusProtocol.MaximumPduLength || requestPdu.Span[0] == 0 ||
            responsePdu.IsEmpty)
            return DriverResult<int>.Failure(ConfigurationError("MODBUS_RAW_PDU",
                "Raw PDU requires a nonzero function code, 1..253 request bytes, and a non-empty response destination."));
        var gate = await EnterRequestGateAsync(cancellationToken).ConfigureAwait(false);
        if (gate is { } gateError) return DriverResult<int>.Failure(Contextualize(gateError, DriverOperation.Execute));
        try
        {
            requestPdu.Span.CopyTo(_requestBuffer.AsSpan(ModbusProtocol.MbapHeaderLength));
            var result =
                await ExecutePreparedPduCoreAsync(requestPdu.Length, unitId ?? _options.UnitId, cancellationToken)
                    .ConfigureAwait(false);
            if (!result.IsSuccess)
                return DriverResult<int>.Failure(Contextualize(result.Error, DriverOperation.Execute));
            var response = _responsePdu.AsSpan(0, result.Value);
            var error = ModbusProtocol.ValidateFunctionResponse(response, requestPdu.Span[0]);
            if (error is { } e)
                return DriverResult<int>.Failure(Contextualize(ObserveResponseError(e), DriverOperation.Execute));
            if (responsePdu.Length < response.Length)
                return DriverResult<int>.Failure(ConfigurationError("MODBUS_RAW_DESTINATION",
                    $"Raw response contains {response.Length} bytes; destination has {responsePdu.Length}."));
            response.CopyTo(responsePdu.Span);
            return DriverResult<int>.Success(response.Length);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    public ValueTask<DriverResult<int>> ExecuteEncapsulatedInterfaceAsync(
        ModbusMeiType meiType,
        ReadOnlyMemory<byte> requestData,
        Memory<byte> responseData,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        return ExecuteEncapsulatedInterfaceAsync((byte)meiType, requestData, responseData, unitId, cancellationToken);
    }

    /// <summary>Executes an arbitrary MEI type for vendor/private extensions.</summary>
    public async ValueTask<DriverResult<int>> ExecuteEncapsulatedInterfaceAsync(
        byte meiType,
        ReadOnlyMemory<byte> requestData,
        Memory<byte> responseData,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if (requestData.Length > ModbusProtocol.MaximumPduLength - 2)
            return DriverResult<int>.Failure(ConfigurationError("MODBUS_MEI_LENGTH",
                "MEI type-specific request data exceeds the MODBUS PDU limit."));
        var request = new byte[2 + requestData.Length];
        request[0] = ModbusProtocol.EncapsulatedInterfaceTransport;
        request[1] = meiType;
        requestData.Span.CopyTo(request.AsSpan(2));
        var rawResponse = new byte[Math.Min(ModbusProtocol.MaximumPduLength, responseData.Length + 2)];
        var raw = await ExecuteRawPduAsync(request, rawResponse, unitId, cancellationToken).ConfigureAwait(false);
        if (!raw.IsSuccess) return raw;
        if (raw.Value < 2 || rawResponse[0] != ModbusProtocol.EncapsulatedInterfaceTransport ||
            rawResponse[1] != meiType)
            return DriverResult<int>.Failure(ServiceProtocolError("MODBUS_MEI_ECHO",
                "Encapsulated Interface response did not echo the requested MEI type.", DriverOperation.Execute));
        var length = raw.Value - 2;
        if (responseData.Length < length)
            return DriverResult<int>.Failure(ConfigurationError("MODBUS_MEI_DESTINATION",
                $"MEI response contains {length} type-specific bytes; destination has {responseData.Length}."));
        rawResponse.AsSpan(2, length).CopyTo(responseData.Span);
        return DriverResult<int>.Success(length);
    }

    public async ValueTask<DriverResult<ModbusDeviceIdentification>> ReadAllDeviceIdentificationAsync(
        ModbusDeviceIdReadCode readDeviceIdCode = ModbusDeviceIdReadCode.Extended,
        byte? unitId = null,
        CancellationToken cancellationToken = default)
    {
        if ((byte)readDeviceIdCode is < (byte)ModbusDeviceIdReadCode.Basic or > (byte)ModbusDeviceIdReadCode.Extended)
            return DriverResult<ModbusDeviceIdentification>.Failure(ConfigurationError("MODBUS_DEVICE_ID_CODE",
                "Stream Device Identification code must be Basic, Regular, or Extended."));
        var objects = new List<ModbusDeviceIdObject>();
        byte objectId = 0;
        for (var page = 0; page < 256; page++)
        {
            var result = await ReadDeviceIdentificationAsync(readDeviceIdCode, objectId, unitId, cancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess) return result;
            objects.AddRange(result.Value.Objects);
            if (!result.Value.MoreFollows)
                return DriverResult<ModbusDeviceIdentification>.Success(
                    new ModbusDeviceIdentification(readDeviceIdCode, result.Value.ConformityLevel, false, 0,
                        [.. objects]));
            if (result.Value.NextObjectId == objectId)
                return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_LOOP",
                    "Device Identification pagination did not advance Next Object ID.", DriverOperation.Identify));
            objectId = result.Value.NextObjectId;
        }

        return DriverResult<ModbusDeviceIdentification>.Failure(ServiceProtocolError("MODBUS_MEI_PAGES",
            "Device Identification exceeded 256 pages.", DriverOperation.Identify));
    }

    private DriverError ServiceProtocolError(string code, string message,
        DriverOperation operation = DriverOperation.Diagnose)
    {
        return Contextualize(ModbusProtocol.ProtocolError(code, message), operation);
    }
}
