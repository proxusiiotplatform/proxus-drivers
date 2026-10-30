# Proxus.Drivers (open drivers)

Industrial protocol drivers for .NET 10, written for Native AOT: no reflection, no runtime code generation, real
async socket I/O and structured errors that say what failed, why, and what to try next.

| Package | What it does |
|---|---|
| `Proxus.Drivers.Abstractions` | Protocol-neutral values, quality, timestamps, errors and the common driver contract |
| `Proxus.Drivers.Core` | Socket and timeout plumbing shared by the drivers |
| `Proxus.Drivers.Modbus` | Modbus master over TCP, with RTU and ASCII framing through a gateway or a stream you open |
| `Proxus.Drivers.OpcUa` | OPC UA Binary client: SecureChannel (None, Basic256Sha256, Aes256_Sha256_RsaPss), Session, Read, Write, Browse, subscriptions, history, methods and events |

None of the packages depends on another library.

## Quick start

```bash
dotnet run --project samples/Proxus.Drivers.Modbus.Read -- 192.168.1.10 502 hr:0
dotnet run --project samples/Proxus.Drivers.OpcUa.Read -- opc.tcp://192.168.1.20:4840
```

Protocol guides: [Modbus](docs/protocols/modbus/index.md), [OPC UA](docs/protocols/opc-ua/index.md).
API reference: [Abstractions](docs/public/api/abstractions.md), [Core](docs/public/api/core.md),
[Modbus](docs/public/api/modbus.md), [OPC UA](docs/public/api/opcua.md).

## How much is proven

Every capability carries an evidence level ([definitions](docs/reference/evidence-levels.md)), from deterministic
tests to independent interoperability against libmodbus, PyModbus, open62541, asyncua and the OPC Foundation reference
server. The protocol guides say which level each feature has reached. A simulator is not a physical device, and the
guides do not pretend otherwise.

## Build and test

```bash
dotnet build Proxus.Drivers.slnx -c Release
dotnet run --project tests/Proxus.Drivers.Modbus.SelfTests -c Release --no-build
dotnet run --project tests/Proxus.Drivers.OpcUa.SelfTests -c Release --no-build
```

`lab/servers` holds the container recipes for the PyModbus and open62541 servers used in interoperability runs.

## More protocols

These drivers are the open part of the driver set inside [Proxus](https://proxus.io/), an on-premise industrial
data platform. Siemens S7, EtherNet/IP, CNC controls and the other protocols ship with the platform.

## Licence

Apache-2.0, see [LICENSE](LICENSE). The packet captures under `tests/corpus/fixtures` keep their own licences, listed
in [tests/corpus/THIRD_PARTY_NOTICES.md](tests/corpus/THIRD_PARTY_NOTICES.md).
