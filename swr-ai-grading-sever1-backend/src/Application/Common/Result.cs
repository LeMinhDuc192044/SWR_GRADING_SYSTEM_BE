namespace Application.Common;

/// <summary>
/// Kết quả trả về dạng non-generic (không cần data).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string? code = null) => new(false, error, code);
}

/// <summary>
/// Kết quả trả về chuẩn cho tất cả Service methods.
/// Thay thế cho việc throw exception thông thường → dễ handle hơn.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu trả về khi thành công.</typeparam>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public string? Error { get; }
    public string? ErrorCode { get; }

    private Result(bool isSuccess, T? data, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Data = data;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result<T> Success(T data) => new(true, data, null, null);
    public static Result<T> Failure(string error, string? code = null) => new(false, default, error, code);

    // Implicit conversion để dễ dùng
    public static implicit operator Result<T>(T data) => Success(data);
}
