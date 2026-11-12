using Application.Common;
using Application.DTOs.Assignments;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[Authorize(Roles = "Admin,Manager,Examiner")]
[Route("api/grading-sessions/{gradingSessionId:guid}/assignments")]
public class AssignmentController : ControllerBase
{
    private readonly IAssignmentService _assignments;

    public AssignmentController(IAssignmentService assignments)
    {
        _assignments = assignments;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid gradingSessionId, CancellationToken ct)
        => ToResponse(await _assignments.GetByGradingSessionAsync(gradingSessionId, ct));

    [HttpGet("{assignmentId:guid}")]
    public async Task<IActionResult> GetById( Guid gradingSessionId, Guid assignmentId, CancellationToken ct)
        => ToResponse(await _assignments.GetByIdAsync(gradingSessionId, assignmentId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid gradingSessionId, [FromBody] CreateAssignmentRequest request, CancellationToken ct)
        => ToResponse(await _assignments.CreateAsync(gradingSessionId, request, ct));

    [HttpPut("{assignmentId:guid}")]
    public async Task<IActionResult> Update(Guid gradingSessionId, Guid assignmentId, [FromBody] UpdateAssignmentRequest request, CancellationToken ct)
        => ToResponse(await _assignments.UpdateAsync(gradingSessionId, assignmentId, request, ct));

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Delete(Guid gradingSessionId, Guid assignmentId, CancellationToken ct)
        => ToResponse(await _assignments.DeleteAsync(gradingSessionId, assignmentId, ct));

    private IActionResult ToResponse<T>(Result<T> result)
        => result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data!))
            : Failure(result.Error, result.ErrorCode);  

    private IActionResult ToResponse(Result result)
        => result.IsSuccess
            ? Ok(ApiResponse.Success(new { }))
            : Failure(result.Error, result.ErrorCode);

    private IActionResult Failure(string? error, string? code)
    {
        var status = code switch
        {
            not null when code.EndsWith("_NOT_FOUND") => StatusCodes.Status404NotFound,
            "ASSIGNMENT_LOCKED" or "MONITOR_DUPLICATE_SUBMISSION" or "SUBMISSION_ALREADY_ASSIGNED" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };
        return StatusCode(status, ApiResponse.Failure(status, error ?? "Request failed.", code));
    }
}