---
id: public-examples-common
title: Common SDK patterns
audience: developer
status: maintained
since: 0.1.0
last_verified: 2026-08-13
---

# Common SDK patterns

## Treat results as data

Driver operations return `DriverResult` or `DriverResult<T>`. A failed operation does not make `Value`
meaningful. Inspect the error before using it.

```csharp
var result = await client.ReadValueAsync(descriptor, cancellationToken);
if (!result.IsSuccess)
{
    var error = result.Error;
    logger.LogWarning(
        "Driver read failed Code={Code} Kind={Kind} Retry={Retry} Connection={Connection} Native={Native}",
        error.Code, error.Kind, error.Retry, error.Connection, error.NativeCode);
    return;
}

var engineeringValue = result.Value.AsFloat32();
```

Use `ToUserMessage()` for an operator-facing summary. Use structured fields for automation and
telemetry; never branch on the English message text.

## Own the client lifecycle

```csharp
await using var client = CreateClient();

var connected = await client.ConnectAsync(stoppingToken);
if (!connected.IsSuccess)
    throw new InvalidOperationException(connected.Error.ToUserMessage());

try
{
    await PollAsync(client, stoppingToken);
}
finally
{
    var disconnected = await client.DisconnectAsync(CancellationToken.None);
    if (!disconnected.IsSuccess)
        logger.LogWarning("Disconnect failed: {Code}", disconnected.Error.Code);
}
```

`DisposeAsync` is the final resource boundary, not a substitute for observing disconnect errors. Do not
share one client instance between unrelated endpoints. A protocol client may serialize requests on one
physical connection even when callers invoke it concurrently.

## Compile configuration once

Parse addresses, validate descriptors, discover schemas, and compile read/write plans when a device
profile is activated. Reuse the compiled objects during polling.

```csharp
var parsed = ModbusAddress.Parse("unit=2;hr:100");
if (!parsed.IsSuccess)
    throw new InvalidOperationException(parsed.Error.ToUserMessage());

var descriptor = new ModbusRegisterDescriptor(
    parsed.Value,
    IndustrialDataType.Float32,
    Layout: ModbusRegisterLayout.WordSwapped);

var validation = descriptor.ValidateScalar();
if (!validation.IsSuccess)
    throw new InvalidOperationException(validation.Error.ToUserMessage());
```

## Cancellation and timeouts

`ConnectTimeout` and `RequestTimeout` bound driver-owned network stages. A caller cancellation token
represents application shutdown or an abandoned operation. Keep these meanings separate so logs can
distinguish a slow device from a deliberate cancellation.

```csharp
using var pollDeadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
pollDeadline.CancelAfter(TimeSpan.FromSeconds(2));
var result = await client.ReadValueAsync(descriptor, pollDeadline.Token);
```

Do not wrap async driver calls in `Task.Run`, `.Wait()`, or `.Result`. Do not create an unbounded task per
tag. Prefer one compiled batch per endpoint and a bounded scheduler across endpoints.

## Retry reads, reconcile writes

Follow `error.Retry` and `error.Connection`; they classify the observed failure, not permission for an
infinite loop.

```csharp
if (!result.IsSuccess && result.Error.Connection == DriverConnectionDisposition.Broken)
{
    await client.DisconnectAsync(CancellationToken.None);
    // Reconnect under an application-owned attempt and backoff budget.
}
```

Reads are normally safe to retry within a deadline. A timed-out write may have reached the device even
when its acknowledgement did not reach the client. Do not automatically replay an ambiguous write;
read back or apply an application-specific idempotency rule.

## Buffer ownership

The caller owns destination arrays passed as `Memory<T>` and source arrays passed as `ReadOnlyMemory<T>`.
Do not mutate a source buffer until the awaited call completes. Reuse buffers only when a single poll owns
them, or protect ownership explicitly.

## Production checklist

- Log endpoint identity without credentials or process payloads.
- Bound reconnect attempts and add jittered backoff.
- Separate device configuration errors from transient transport failures.
- Measure request count and values per request, not only elapsed method time.
- Make writes opt-in and independently authorized.
- Keep the exact device, firmware, security mode, and tested operations in a qualification record.
