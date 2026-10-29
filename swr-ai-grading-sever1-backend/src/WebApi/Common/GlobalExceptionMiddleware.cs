using System.Text.Json;
using Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace WebApi.Common;

public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var traceId = context.TraceIdentifier;
        _logger.LogError(ex, "[{TraceId}] Ngoại lệ chưa xử lý: {Message}", traceId, ex.Message);

        int statusCode;
        string errorCode;
        string message;
        object? errors = null;

        switch (ex)
        {
            case NotFoundException notFoundEx:
                statusCode = StatusCodes.Status404NotFound;
                errorCode = "NOT_FOUND";
                message = notFoundEx.Message;
                break;

            case ValidationException valEx:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "VALIDATION_FAILED";
                message = valEx.Message;
                break;

            case FluentValidation.ValidationException fvEx:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "VALIDATION_FAILED";
                message = "Dữ liệu gửi lên không hợp lệ.";
                errors = fvEx.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
                break;

            case ForbiddenException forbiddenEx:
                statusCode = StatusCodes.Status403Forbidden;
                errorCode = "FORBIDDEN";
                message = forbiddenEx.Message;
                break;

            case UnauthorizedAccessException unauthEx:
                statusCode = StatusCodes.Status401Unauthorized;
                errorCode = "UNAUTHORIZED";
                message = unauthEx.Message;
                break;

            case DbUpdateException dbUpdateEx:
                if (dbUpdateEx.InnerException is PostgresException pgEx)
                {
                    switch (pgEx.SqlState)
                    {
                        case "22001": // string data right truncation (value too long)
                            statusCode = StatusCodes.Status400BadRequest;
                            errorCode = "DATA_TOO_LONG";
                            message = "Dữ liệu nhập vào vượt quá độ dài tối đa cho phép của cơ sở dữ liệu. Vui lòng kiểm tra lại độ dài các trường ký tự (ví dụ: mã kỳ thi, mã số, ghi chú).";
                            break;

                        case "23505": // unique_violation
                            statusCode = StatusCodes.Status409Conflict;
                            errorCode = "DUPLICATE_ENTRY";
                            message = "Dữ liệu đã tồn tại trong hệ thống (vi phạm ràng buộc trùng lặp dữ liệu duy nhất).";
                            break;

                        case "23503": // foreign_key_violation
                            statusCode = StatusCodes.Status400BadRequest;
                            errorCode = "FOREIGN_KEY_VIOLATION";
                            message = "Dữ liệu liên kết (khóa ngoại) không tồn tại trong hệ thống.";
                            break;

                        default:
                            statusCode = StatusCodes.Status400BadRequest;
                            errorCode = "DATABASE_ERROR";
                            message = $"Lỗi thao tác cơ sở dữ liệu ({pgEx.SqlState}): {pgEx.MessageText}";
                            break;
                    }
                }
                else
                {
                    statusCode = StatusCodes.Status400BadRequest;
                    errorCode = "DATABASE_UPDATE_ERROR";
                    message = "Không thể cập nhật cơ sở dữ liệu do vi phạm ràng buộc dữ liệu.";
                }
                break;

            case InvalidDataException dataEx:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "INVALID_DATA";
                message = dataEx.Message;
                break;

            case ArgumentException argEx:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "BAD_REQUEST";
                message = argEx.Message;
                break;

            case InvalidOperationException opEx:
                statusCode = StatusCodes.Status400BadRequest;
                errorCode = "INVALID_OPERATION";
                message = opEx.Message;
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                errorCode = "INTERNAL_SERVER_ERROR";
                message = "Đã xảy ra lỗi máy chủ nội bộ. Vui lòng cung cấp mã TraceId cho quản trị viên.";
                if (_env.IsDevelopment())
                {
                    errors = new
                    {
                        exception = ex.GetType().Name,
                        details = ex.Message,
                        stackTrace = ex.StackTrace
                    };
                }
                break;
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var response = ApiResponse.Failure(statusCode, message, errorCode, errors, traceId);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
