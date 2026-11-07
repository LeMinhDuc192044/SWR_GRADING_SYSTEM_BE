using Application.Common;
using Application.DTOs.GradingSession;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/sessions/{examinationSessionId:guid}/grading-sessions")]
[Authorize(Roles = "Manager,Monitor")]
public sealed class GradingSessionsController : ControllerBase
{
    private readonly IGradingSessionService _service;

    public GradingSessionsController(IGradingSessionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid examinationSessionId, CancellationToken ct)
    {
        var result = await _service.GetByExaminationSessionAsync(examinationSessionId, ct);
        return ResultResponse(result);
    }

    [HttpGet("~/api/grading-sessions")]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        var result = await _service.GetAllAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("/api/monitors/{monitorId:guid}/grading-sessions")]
    public async Task<IActionResult> GetByMonitor(
        Guid monitorId,
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        var result = await _service.GetByMonitorAsync(
            monitorId,
            request,
            ct);

        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("{gradingSessionId:guid}")]
    public async Task<IActionResult> GetById(Guid examinationSessionId, Guid gradingSessionId, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(examinationSessionId, gradingSessionId, ct);
        return ResultResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid examinationSessionId, [FromBody] CreateGradingSessionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(examinationSessionId, request, ct);
        return ResultResponse(result);
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        Guid examinationSessionId,
        [FromBody] UpdateGradingSessionRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(
            examinationSessionId,
            request,
            ct);

        return ResultResponse(result);
    }

    [HttpDelete("{gradingSessionId:guid}")]
    public async Task<IActionResult> Delete(Guid examinationSessionId, Guid gradingSessionId, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(examinationSessionId, gradingSessionId, ct);
        return ResultResponse(result);
    }

    private IActionResult ResultResponse<T>(Application.Common.Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        return ErrorResponse(result.Error!, result.ErrorCode);
    }

    private IActionResult ResultResponse(Application.Common.Result result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));
        return ErrorResponse(result.Error!, result.ErrorCode);
    }

    private IActionResult ErrorResponse(string message, string? errorCode)
    {
        if (errorCode is "EXAMINATION_SESSION_NOT_FOUND" or "GRADING_SESSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, message, errorCode));
        return BadRequest(ApiResponse.Failure(400, message, errorCode));
    }
}
