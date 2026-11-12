using Application.Common;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/examinations/{examinationId:guid}/sessions")]
public sealed class ExaminationSessionsController : ControllerBase
{
    private readonly IExaminationSessionService _service;

    public ExaminationSessionsController(IExaminationSessionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid examinationId, CancellationToken ct)
    {
        var result = await _service.GetByExaminationAsync(examinationId, ct);
        return ResultResponse(result);
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetById(Guid examinationId, Guid sessionId, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(examinationId, sessionId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Examiner")]
    public async Task<IActionResult> Create(Guid examinationId, [FromBody] CreateExaminationSessionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(examinationId, request, ct);
        return ResultResponse(result);
    }

    [HttpGet("~/api/examination-sessions")]
    [Authorize]
    public async Task<IActionResult> GetAllSessions([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(request, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("~/api/students/{studentId:guid}/examination-sessions")]
    [Authorize]
    public async Task<IActionResult> GetStudentSessions(Guid studentId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _service.GetPagedByStudentAsync(studentId, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    [HttpGet("~/api/examination-sessions/{examinationSessionId:guid}/results")]
    [Authorize]
    public async Task<IActionResult> GetResults(Guid examinationSessionId, CancellationToken ct)
    {
        var result = await _service.GetResultsAsync(examinationSessionId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    [HttpPut("{sessionId:guid}")]
    [Authorize(Roles = "Admin,Examiner")]
    public async Task<IActionResult> Update(Guid examinationId, Guid sessionId, [FromBody] UpdateExaminationSessionRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(examinationId, sessionId, request, ct);
        return ResultResponse(result);
    }

    [HttpDelete("{sessionId:guid}")]
    [Authorize(Roles = "Admin,Examiner")]
    public async Task<IActionResult> Delete(Guid examinationId, Guid sessionId, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(examinationId, sessionId, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(null))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateFromExcel(
        Guid examinationId, [FromForm] CreateExaminationSessionFromExcelForm form, CancellationToken ct)
    {
        if (!Path.GetExtension(form.File.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(ApiResponse.Failure(400, "Only .xlsx files are supported.", "INVALID_FILE_FORMAT"));
        if (form.File.Length == 0 || form.File.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse.Failure(400, "The Excel file must be between 1 byte and 5 MB.", "FILE_SIZE_LIMIT"));

        var request = new CreateExaminationSessionRequest
        {
            ExaminationCode = form.ExaminationCode,
            StartDate = form.StartDate,
            StartTime = form.StartTime,
            Status = form.Status,
            PaperSetId = form.PaperSetId
        };

        using var buffer = new MemoryStream();
        await form.File.CopyToAsync(buffer, ct);

        return ResultResponse(await _service.CreateWithStudentExcelAsync(examinationId, request, buffer, ct));
    }

    private IActionResult ResultResponse<T>(Application.Common.Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode is "EXAMINATION_NOT_FOUND" or "EXAMINATION_SESSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }
}
