using Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Common;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        int successStatusCode = StatusCodes.Status200OK,
        string successMessage = "Thành công")
    {
        if (result.IsSuccess)
        {
            return successStatusCode switch
            {
                StatusCodes.Status201Created => controller.StatusCode(
                    StatusCodes.Status201Created,
                    ApiResponse.Success(result.Data, StatusCodes.Status201Created, successMessage)),
                _ => controller.Ok(ApiResponse.Success(result.Data, StatusCodes.Status200OK, successMessage))
            };
        }

        var errorCode = result.ErrorCode ?? "BAD_REQUEST";
        var message = result.Error ?? "Đã xảy ra lỗi.";
        var traceId = controller.HttpContext.TraceIdentifier;

        if (errorCode.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return controller.NotFound(ApiResponse.Failure(StatusCodes.Status404NotFound, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("REQUIRED", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("DENIED", StringComparison.OrdinalIgnoreCase))
        {
            return controller.StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(StatusCodes.Status403Forbidden, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("EXISTS", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("ALREADY", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("IN_USE", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Conflict(ApiResponse.Failure(StatusCodes.Status409Conflict, message, errorCode, traceId: traceId));
        }

        return controller.BadRequest(ApiResponse.Failure(StatusCodes.Status400BadRequest, message, errorCode, traceId: traceId));
    }

    public static IActionResult ToActionResult(
        this Result result,
        ControllerBase controller,
        string successMessage = "Thành công")
    {
        if (result.IsSuccess)
        {
            return controller.Ok(ApiResponse.Success(null, StatusCodes.Status200OK, successMessage));
        }

        var errorCode = result.ErrorCode ?? "BAD_REQUEST";
        var message = result.Error ?? "Đã xảy ra lỗi.";
        var traceId = controller.HttpContext.TraceIdentifier;

        if (errorCode.Contains("NOT_FOUND", StringComparison.OrdinalIgnoreCase))
        {
            return controller.NotFound(ApiResponse.Failure(StatusCodes.Status404NotFound, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("FORBIDDEN", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("REQUIRED", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("DENIED", StringComparison.OrdinalIgnoreCase))
        {
            return controller.StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(StatusCodes.Status403Forbidden, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("UNAUTHORIZED", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, message, errorCode, traceId: traceId));
        }

        if (errorCode.Contains("EXISTS", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("ALREADY", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
            errorCode.Contains("IN_USE", StringComparison.OrdinalIgnoreCase))
        {
            return controller.Conflict(ApiResponse.Failure(StatusCodes.Status409Conflict, message, errorCode, traceId: traceId));
        }

        return controller.BadRequest(ApiResponse.Failure(StatusCodes.Status400BadRequest, message, errorCode, traceId: traceId));
    }
}
