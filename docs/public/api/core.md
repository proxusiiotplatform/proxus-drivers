---
id: public-api-core
title: Proxus.Drivers.Core
audience: developer
status: generated
since: 0.1.0
last_verified: 2026-08-13
---

# Proxus.Drivers.Core

Public API for `Proxus.Drivers.Core`. This page is generated from the compiled assembly.

Use [Common SDK patterns](../examples/common-patterns.md) for lifecycle, result handling, cancellation, and ownership guidance.

## SocketIo

```csharp
public static class SocketIo
```

*Undocumented: no XML summary; this text is generated from the name.* Represents socket Io in the public driver contract.

### Members

#### `ReceiveExactlyAsync`

```csharp
public static ValueTask ReceiveExactlyAsync(Socket socket, Memory<byte> buffer, CancellationToken cancellationToken)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs receive Exactly Async.

#### `SendAllAsync`

```csharp
public static ValueTask SendAllAsync(Socket socket, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
```

*Undocumented: no XML summary; this text is generated from the name.* Performs send All Async.

## TimeoutTokenScope

```csharp
public struct TimeoutTokenScope
```

Creates a linked timeout token only when a finite timeout is configured. An infinite timeout keeps the connected steady-state path free of timeout-CTS allocations and lets the caller own the read-cycle budget.

### Members

#### `MaximumSupportedTimeout`

```csharp
public static readonly TimeSpan MaximumSupportedTimeout
```

*Undocumented: no XML summary; this text is generated from the name.* Stores maximum Supported Timeout.

#### `Create`

```csharp
public static TimeoutTokenScope Create(CancellationToken callerToken, TimeSpan timeout)
```

*Undocumented: no XML summary; this text is generated from the name.* Creates the requested value.

#### `Dispose`

```csharp
public void Dispose()
```

*Undocumented: no XML summary; this text is generated from the name.* Performs dispose.

#### `Token`

```csharp
public CancellationToken Token { get;  }
```

*Undocumented: no XML summary; this text is generated from the name.* Gets token.

