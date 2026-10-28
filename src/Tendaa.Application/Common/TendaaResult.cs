namespace Tendaa.Application.Common;

/// <summary>Standard return shape for every Application service method. Controllers reflect
/// <see cref="StatusCode"/> back to the client via <c>StatusCode(result.StatusCode, result)</c>
/// rather than always returning 200.</summary>
public class TendaaResult
{
    public bool IsSuccessful { get; }
    public int StatusCode { get; }
    public string? Message { get; }

    protected TendaaResult(bool isSuccessful, int statusCode, string? message)
    {
        IsSuccessful = isSuccessful;
        StatusCode = statusCode;
        Message = message;
    }

    public static TendaaResult Success(string? message = null) => new(true, 200, message);

    public static TendaaResult BadRequest(string? message = null) => new(false, 400, message);

    public static TendaaResult Unauthorized(string? message = null) => new(false, 401, message);

    public static TendaaResult NotFound(string? message = null) => new(false, 404, message);

    public static TendaaResult Conflict(string? message = null) => new(false, 409, message);

    public static TendaaResult InternalServerError(string? message = null) => new(false, 500, message);
}

public sealed class TendaaResult<T> : TendaaResult
{
    public T? Data { get; }

    private TendaaResult(bool isSuccessful, int statusCode, string? message, T? data)
        : base(isSuccessful, statusCode, message)
    {
        Data = data;
    }

    public static TendaaResult<T> Success(T data, string? message = null) => new(true, 200, message, data);

    public static new TendaaResult<T> BadRequest(string? message = null) => new(false, 400, message, default);

    public static new TendaaResult<T> Unauthorized(string? message = null) => new(false, 401, message, default);

    public static new TendaaResult<T> NotFound(string? message = null) => new(false, 404, message, default);

    public static new TendaaResult<T> Conflict(string? message = null) => new(false, 409, message, default);

    public static new TendaaResult<T> InternalServerError(string? message = null) => new(false, 500, message, default);
}
