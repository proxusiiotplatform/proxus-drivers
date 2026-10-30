using System.Text;

namespace Proxus.Drivers;

public enum DriverErrorKind : byte
{
    None = 0,
    Configuration = 1,
    NotSupported = 2,
    Cancelled = 3,
    Timeout = 4,
    Transport = 5,
    Protocol = 6,
    Device = 7,
    Data = 8,
    Disposed = 9
}

public enum DriverOperation : byte
{
    Unknown = 0,
    Validate = 1,
    Connect = 2,
    Disconnect = 3,
    Handshake = 4,
    Negotiate = 5,
    Read = 6,
    ReadBlock = 7,
    ReadBatch = 8,
    Write = 9,
    WriteBlock = 10,
    WriteBatch = 11,
    Encode = 12,
    Decode = 13,
    Diagnose = 14,
    Identify = 15,
    Execute = 16,
    Browse = 17,
    Discover = 18,
    Subscribe = 19,
    Publish = 20,
    Republish = 21
}

public enum DriverRetryDisposition : byte
{
    Unknown = 0,
    DoNotRetry = 1,
    RetrySameConnection = 2,
    ReconnectAndRetry = 3,
    FixConfiguration = 4
}

public enum DriverConnectionDisposition : byte
{
    Unknown = 0,
    Healthy = 1,
    Unchanged = 2,

    // Value 3 intentionally unassigned; reconnect advice is represented by Retry.
    Broken = 4
}

/// <summary>
///     Structured, protocol-neutral diagnostics. Error construction is off the healthy hot path;
///     user-facing prose is materialized only when <see cref="ToUserMessage" /> is requested.
/// </summary>
public readonly record struct DriverError(
    DriverErrorKind Kind,
    string Code,
    string Message,
    int? NativeCode = null,
    DriverOperation Operation = DriverOperation.Unknown,
    string? Protocol = null,
    string? Endpoint = null,
    string? Address = null,
    IndustrialDataType? DataType = null,
    string? NativeMeaning = null,
    string? Cause = null,
    string? SuggestedAction = null,
    DriverRetryDisposition Retry = DriverRetryDisposition.Unknown,
    DriverConnectionDisposition Connection = DriverConnectionDisposition.Unknown)
{
    public static DriverError None => new(DriverErrorKind.None, string.Empty, string.Empty);
    public bool IsNone => Kind == DriverErrorKind.None;

    public DriverError WithContext(
        string protocol,
        DriverOperation operation,
        string? endpoint = null,
        string? address = null,
        IndustrialDataType? dataType = null,
        DriverRetryDisposition? retry = null,
        DriverConnectionDisposition? connection = null)
    {
        return this with
        {
            Protocol = Protocol ?? protocol,
            Operation = operation == DriverOperation.Unknown ? Operation : operation,
            Endpoint = Endpoint ?? endpoint,
            Address = Address ?? address,
            DataType = DataType ?? dataType,
            Retry = Retry == DriverRetryDisposition.Unknown && retry.HasValue ? retry.Value : Retry,
            Connection = Connection == DriverConnectionDisposition.Unknown && connection.HasValue
                ? connection.Value
                : Connection
        };
    }

    /// <summary>Creates concise field-engineer-facing guidance without changing the structured error.</summary>
    public string ToUserMessage()
    {
        if (IsNone)
            return string.Empty;

        var builder = new StringBuilder(320);
        builder.Append(Message);
        if (!string.IsNullOrWhiteSpace(Code))
            builder.Append(" Code: ").Append(Code).Append('.');
        if (!string.IsNullOrWhiteSpace(Protocol))
            builder.Append(" Protocol: ").Append(Protocol).Append('.');
        if (Operation != DriverOperation.Unknown)
            builder.Append(" Operation: ").Append(Operation).Append('.');

        if (!string.IsNullOrWhiteSpace(Address))
            builder.Append(" Address: ").Append(Address).Append('.');
        if (DataType is { } dataType)
            builder.Append(" Type: ").Append(dataType).Append('.');
        if (!string.IsNullOrWhiteSpace(Endpoint))
            builder.Append(" Endpoint: ").Append(Endpoint).Append('.');
        if (NativeCode is { } native)
        {
            builder.Append(" Native code: 0x").Append(native.ToString("X")).Append('.');
            if (!string.IsNullOrWhiteSpace(NativeMeaning))
                builder.Append(' ').Append(NativeMeaning);
        }

        if (!string.IsNullOrWhiteSpace(Cause))
            builder.Append(" Likely cause: ").Append(Cause);
        if (!string.IsNullOrWhiteSpace(SuggestedAction))
            builder.Append(" Action: ").Append(SuggestedAction);

        builder.Append(" Retry: ").Append(Retry).Append('.');
        builder.Append(" Connection: ").Append(Connection).Append('.');
        return builder.ToString();
    }

    public override string ToString()
    {
        return NativeCode is { } native
            ? $"{Kind}/{Code} ({native}): {Message}"
            : $"{Kind}/{Code}: {Message}";
    }
}
