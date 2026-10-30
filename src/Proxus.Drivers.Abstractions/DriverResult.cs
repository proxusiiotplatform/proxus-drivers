namespace Proxus.Drivers;

public readonly struct DriverResult
{
    private DriverResult(bool isSuccess, DriverError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public DriverError Error { get; }

    public static DriverResult Success()
    {
        return new DriverResult(true, DriverError.None);
    }

    public static DriverResult Failure(DriverError error)
    {
        return new DriverResult(false, error);
    }
}

public readonly struct DriverResult<T>
{
    private readonly T? _value;

    private DriverResult(bool isSuccess, T? value, DriverError error)
    {
        IsSuccess = isSuccess;
        _value = value;
        Error = error;
    }

    public bool IsSuccess { get; }
    public DriverError Error { get; }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"A failed driver result has no value: {Error}");


    public static DriverResult<T> Success(T value)
    {
        return new DriverResult<T>(true, value, DriverError.None);
    }

    public static DriverResult<T> Failure(DriverError error)
    {
        return new DriverResult<T>(false, default, error);
    }
}
