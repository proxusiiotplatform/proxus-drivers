# Proxus.Drivers.Abstractions

The shared contracts of the Proxus industrial drivers: `DriverResult` and `DriverError` (results with structured,
actionable errors instead of exceptions), `DriverValue` and quality, industrial data types, record schemas and
packed layouts, batches and driver capabilities.

You rarely reference this package directly: `Proxus.Drivers.Modbus` and `Proxus.Drivers.OpcUa` bring it at exactly
their own version. Open source under the Apache License 2.0.
