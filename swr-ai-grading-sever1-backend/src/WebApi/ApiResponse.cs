namespace WebApi;

public sealed class ApiResponse<T>
{
    public string Status { get; init; } = string.Empty;
    public int Code { get; init; }
    public string? ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public object? Errors { get; init; }
    public string? TraceId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Success(T? data, int code = 200, string message = "Thành công") => new()
    {
        Status = "success",
        Code = code,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> Failure(
        int code,
        string message,
        string? errorCode = null,
        object? errors = null,
        string? traceId = null) => new()
    {
        Status = "fail",
        Code = code,
        ErrorCode = errorCode,
        Message = message,
        Errors = errors,
        TraceId = traceId,
        Data = default
    };
}

public static class ApiResponse
{
    public static ApiResponse<object?> Success(object? data, int code = 200, string message = "Thành công") =>
        ApiResponse<object?>.Success(data, code, message);

    public static ApiResponse<object?> Failure(
        int code,
        string message,
        string? errorCode = null,
        object? errors = null,
        string? traceId = null) =>
        ApiResponse<object?>.Failure(code, message, errorCode, errors, traceId);
}
