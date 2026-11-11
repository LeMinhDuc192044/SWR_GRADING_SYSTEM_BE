using System.Security.Claims;
using Application.Common;
using Application.DTOs.Grading;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/submissions/{submissionId:guid}/grading")]
public class SubmissionGradingController : ControllerBase
{
    private readonly ISubmissionGradingService _grading;

    public SubmissionGradingController(ISubmissionGradingService grading) => _grading = grading;

    [HttpGet]
    public async Task<IActionResult> GetHistories(Guid submissionId, CancellationToken ct)
        => CurrentUserId is { } userId
            ? ToResponse(await _grading.GetHistoriesAsync(submissionId, userId, ct))
            : Unauthorized();

    [HttpPost("ai")]
    public async Task<IActionResult> GradeWithAi(Guid submissionId, CancellationToken ct)
        => CurrentUserId is { } userId
            ? ToResponse(await _grading.GradeWithAiAsync(submissionId, userId, ct))
            : Unauthorized();

    [HttpPost("lecturer")]
    public async Task<IActionResult> CreateLecturerGrading(Guid submissionId, [FromBody] CreateLecturerGradingRequest request, CancellationToken ct)
        => CurrentUserId is { } userId
            ? ToResponse(await _grading.CreateLecturerGradingAsync(submissionId, request, userId, ct))
            : Unauthorized();

    [HttpPost("review")]
    public async Task<IActionResult> Review(Guid submissionId, [FromBody] ReviewGradingRequest request, CancellationToken ct)
        => CurrentUserId is { } userId
            ? ToResponse(await _grading.ReviewAsync(submissionId, request, userId, ct))
            : Unauthorized();

    [HttpPost("{historyId:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid submissionId, Guid historyId, CancellationToken ct)
        => CurrentUserId is { } userId
            ? ToResponse(await _grading.FinalizeAsync(submissionId, historyId, userId, ct))
            : Unauthorized();

    // ASSUMPTION: the user id is in the NameIdentifier or "sub" claim. Match how your other controllers read it.
    private Guid? CurrentUserId
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id) ? id : null;

    private IActionResult ToResponse<T>(Result<T> result)
        => result.IsSuccess ? Ok(ApiResponse.Success(result.Data!)) : Failure(result.Error, result.ErrorCode);

    private IActionResult Failure(string? error, string? code)
    {
        var status = code switch
        {
            "FORBIDDEN" => StatusCodes.Status403Forbidden,
            not null when code.EndsWith("_NOT_FOUND") => StatusCodes.Status404NotFound,
            "ALREADY_FINALIZED" => StatusCodes.Status409Conflict,
            "AI_GRADING_FAILED" => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse.Failure(status, error ?? "Request failed."));
    }
}