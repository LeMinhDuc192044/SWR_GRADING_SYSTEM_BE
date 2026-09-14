namespace WebApi;

public sealed class ApiResponse<T>
{
    public string Status { get; init; } = string.Empty;
    public int Code { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }

    public static ApiResponse<T> Success(T? data, int code = 200, string message = "Thành công") => new()
    {
        Status = "success",
        Code = code,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> Failure(int code, string message) => new()
    {
        Status = "fail",
        Code = code,
        Message = message,
        Data = default
    };
}

public static class ApiResponse
{
    public static ApiResponse<object?> Success(object? data, int code = 200, string message = "Thành công") =>
        ApiResponse<object?>.Success(data, code, message);

    public static ApiResponse<object?> Failure(int code, string message) =>
        ApiResponse<object?>.Failure(code, message);
}
