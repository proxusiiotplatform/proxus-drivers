# Proxus.Drivers.Abstractions

The shared contracts of the Proxus industrial drivers: `DriverResult` and `DriverError` (results with structured,
actionable errors instead of exceptions), `DriverValue` and quality, industrial data types, record schemas and
packed layouts, batches and driver capabilities.

You rarely reference this package directly: `Proxus.Drivers.Modbus` and `Proxus.Drivers.OpcUa` bring it at exactly
their own version. Open source under the Apache License 2.0.

## About Proxus

Built by [Proxus](https://proxus.io/?utm_source=nuget&utm_medium=readme&utm_campaign=proxus-drivers), the on-premise
industrial data platform: device connectivity, a Unified Namespace, historian, dashboards and governed AI access in one
product. Siemens S7, EtherNet/IP, CNC controls and the other protocols ship with the platform.
Source: [github.com/proxusiiotplatform/proxus-drivers](https://github.com/proxusiiotplatform/proxus-drivers).
