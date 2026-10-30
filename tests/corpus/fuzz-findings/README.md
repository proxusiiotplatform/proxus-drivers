# Fuzz finding reproducers

Inputs saved by `tools/Proxus.Drivers.CorpusFuzz` during the 2026-10-19 bounded campaign (600 s per
target). All OPC UA seeds come from redistributable captures (ICSNPP, BSD-3-Clause; ITI, CC-BY-4.0), so
the mutated inputs may be committed. Classification is in
VALIDATION_RESULTS.md.

Re-run one input with full stack and allocation output:

```bash
dotnet run --project tools/Proxus.Drivers.CorpusFuzz -c Release -- --target opcua-client --repro tests/corpus/fuzz-findings/<file>
```

The OPC UA self-test `Golden corpus: fuzz reproducers stay structured and bounded` replays every file
here. After the 2026-10-19 fixes, every input completes with success or a structured error and stays
within the allocation bound. The `InvalidOperationException` and `NullReferenceException` rows are
GC-UA-6, a timing-dependent race that is still open.

The file name starts with the target name. Findings marked "not reproducible" in the results did not
recur on a single replay; they depend on timing.

| Target | Finding | Hits | Detail | Input |
|---|---|---|---|---|
| `opcua-client` | ALLOCATION browse:Protocol:UA_BROWSE_DECODE | 33 | 12288 MB for a 78-byte input | [`opcua-client-AD318C2B3371B804.bin`](opcua-client-AD318C2B3371B804.bin) |
| `opcua-client` | ALLOCATION endpoints:Protocol:UA_GET_ENDPOINTS_DECODE | 51 | 12983 MB for a 480-byte input | [`opcua-client-7F569742DBFD4C6B.bin`](opcua-client-7F569742DBFD4C6B.bin) |
| `opcua-client` | ALLOCATION session:Protocol:UA_CREATE_SESSION_DECODE | 29 | 14208 MB for a 611-byte input | [`opcua-client-85043C3B54A7B2FA.bin`](opcua-client-85043C3B54A7B2FA.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.OpcUaTcpClient.ReadResponseHeader(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.cs:line 394 | 37 | OPC UA Binary payload truncated at offset 28; need 4 bytes, have 1. | [`opcua-client-E865B9C400D9CDE2.bin`](opcua-client-E865B9C400D9CDE2.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.OpcUaTcpClient.ReadResponseHeader(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.cs:line 395 | 15 | OPC UA Binary payload truncated at offset 32; need 4 bytes, have 2. | [`opcua-client-335DF8A42FF12C8D.bin`](opcua-client-335DF8A42FF12C8D.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.OpcUaTcpClient.ReadResponseHeader(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.cs:line 396 | 4 | OPC UA Binary payload truncated at offset 36; need 1 bytes, have 0. | [`opcua-client-EDFBFDFD79824943.bin`](opcua-client-EDFBFDFD79824943.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.OpcUaTcpClient.ReadResponseHeader(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.cs:line 397 | 37 | OPC UA Binary payload truncated at offset 37; need 4 bytes, have 3. | [`opcua-client-983B02A7F0D186DE.bin`](opcua-client-983B02A7F0D186DE.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.OpcUaTcpClient.SkipDiagnosticInfo(UaBinaryReader& reader, Int32 depth) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.cs:line 407 | 4 | OPC UA Binary payload truncated at offset 55; need 1 bytes, have 0. | [`opcua-client-3287C19FE9E8CC41.bin`](opcua-client-3287C19FE9E8CC41.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadByteString() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 129 | 2 | OPC UA Binary payload truncated at offset 44; need 4 bytes, have 0. | [`opcua-client-6F7D21452D9E7F55.bin`](opcua-client-6F7D21452D9E7F55.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadDateTime() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 96 | 82 | OPC UA Binary payload truncated at offset 20; need 8 bytes, have 5. | [`opcua-client-88F205EFFE12862B.bin`](opcua-client-88F205EFFE12862B.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadExtensionObject() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 184 | 1 | OPC UA Binary payload truncated at offset 43; need 1 bytes, have 0. | [`opcua-client-4E54DF9F7F3536F6.bin`](opcua-client-4E54DF9F7F3536F6.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadInt32() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 54 | 4 | OPC UA Binary payload truncated at offset 37; need 4 bytes, have 0. | [`opcua-client-E27F2DFC8B07E6B2.bin`](opcua-client-E27F2DFC8B07E6B2.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadNodeId(Byte& encodingFlags) @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 140 | 5 | OPC UA Binary payload truncated at offset 41; need 1 bytes, have 0. | [`opcua-client-EDCB8B9A4466C8B4.bin`](opcua-client-EDCB8B9A4466C8B4.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadString() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 118 | 6 | OPC UA Binary payload truncated at offset 54; need 4 bytes, have 2. | [`opcua-client-F46E9D0FF6E9FE48.bin`](opcua-client-F46E9D0FF6E9FE48.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.ReadUInt32() @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 62 | 2 | OPC UA Binary payload truncated at offset 49; need 4 bytes, have 0. | [`opcua-client-89B63D0915CCC27B.bin`](opcua-client-89B63D0915CCC27B.bin) |
| `opcua-client` | CRASH EndOfStreamException at Proxus.Drivers.OpcUa.UaBinaryReader.Require(Int32 count) @ Proxus.Drivers.OpcUa/UaBinaryReader.cs:line 202 | 574 | OPC UA Binary payload truncated at offset 45; need 16777216 bytes, have 49. | [`opcua-client-CF75F0F92BDF6E31.bin`](opcua-client-CF75F0F92BDF6E31.bin) |
| `opcua-client` | CRASH InvalidOperationException at Proxus.Drivers.OpcUa.OpcUaTcpClient.BeginServiceRequest(UInt32 typeId, IndustrialNodeId authToken, UInt32 requestId, UInt32 requestHandle, Nullable`1 requestTimeout) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.Services.cs:line 653 | 1 | SecureChannel is not open. | [`opcua-client-C52A90653EA12004.bin`](opcua-client-C52A90653EA12004.bin) |
| `opcua-client` | CRASH NullReferenceException at Proxus.Drivers.Core.SocketIo.SendAllAsync(Socket socket, ReadOnlyMemory`1 buffer, CancellationToken cancellationToken) @ Proxus.Drivers.Core/SocketIo.cs:line 18 | 2 | Object reference not set to an instance of an object. | [`opcua-client-07B43724D65F2604.bin`](opcua-client-07B43724D65F2604.bin) |
| `opcua-client` | CRASH OutOfMemoryException at Proxus.Drivers.OpcUa.OpcUaTcpClient.ReadBrowseResult(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaTcpClient.Browse.cs:line 240 | 1 | Array dimensions exceeded supported range. | [`opcua-client-A14888BB5FF9646E.bin`](opcua-client-A14888BB5FF9646E.bin) |
| `opcua-client` | HANG | 3 | input did not complete within the watchdog | [`opcua-client-B1432642907BF9EC.bin`](opcua-client-B1432642907BF9EC.bin) |
| `opcua-codecs` | ALLOCATION throw:EndOfStreamException | 13751 | 20352 MB for a 135-byte input | [`opcua-codecs-40C86D400E75AE3B.bin`](opcua-codecs-40C86D400E75AE3B.bin) |
| `opcua-codecs` | ALLOCATION throw:InvalidDataException | 2449 | 38403 MB for a 140-byte input | [`opcua-codecs-EE85F482AC084117.bin`](opcua-codecs-EE85F482AC084117.bin) |
| `opcua-codecs` | ALLOCATION throw:NotSupportedException | 62 | 56689 MB for a 591-byte input | [`opcua-codecs-FF012E2F5E881E06.bin`](opcua-codecs-FF012E2F5E881E06.bin) |
| `opcua-codecs` | CRASH ArgumentException at Proxus.Drivers.DriverValue.FromVariant(DriverValue value) @ Proxus.Drivers.Abstractions/DriverValue.cs:line 241 | 1 | Nested Variant wrappers are not allowed. (Parameter 'value') | [`opcua-codecs-B2F57DC933721C4B.bin`](opcua-codecs-B2F57DC933721C4B.bin) |
| `opcua-codecs` | CRASH OutOfMemoryException at Proxus.Drivers.OpcUa.OpcUaBinaryCodec.ReadVariant(UaBinaryReader& reader) @ Proxus.Drivers.OpcUa/OpcUaBinaryCodec.cs:line 59 | 4 | Array dimensions exceeded supported range. | [`opcua-codecs-597AE1D1666AA2BB.bin`](opcua-codecs-597AE1D1666AA2BB.bin) |
