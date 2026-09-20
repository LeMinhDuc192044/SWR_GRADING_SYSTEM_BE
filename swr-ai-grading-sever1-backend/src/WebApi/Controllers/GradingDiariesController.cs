using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/grading-diaries")]
[Authorize]
public sealed class GradingDiariesController : ControllerBase
{
    private readonly IGradingDiaryService _service;
    private readonly ISubmissionService _submissionService;

    public GradingDiariesController(IGradingDiaryService service, ISubmissionService submissionService)
    {
        _service = service;
        _submissionService = submissionService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGradingDiaryRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _service.CreateAsync(request, userId, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.GradingDiaryId }, ApiResponse.Success(result.Data));

        if (result.ErrorCode == "PAPER_SET_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "LECTURER_REQUIRED")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode == "PAPER_SET_ALREADY_HAS_DIARY")
            return Conflict(ApiResponse.Failure(409, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        Guid? lecturerFilter = IsElevatedRole() ? null : userId;
        var pagedResult = await _service.GetPagedAsync(request, lecturerFilter, ct);
        return Ok(ApiResponse.Success(pagedResult));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _service.GetByIdAsync(id, userId, IsElevatedRole(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateGradingDiaryRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _service.UpdateAsync(id, request, userId, IsElevatedRole(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _service.DeleteAsync(id, userId, IsElevatedRole(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success("Xóa sổ chấm thành công."));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode == "DIARY_HAS_SUBMISSIONS")
            return BadRequest(ApiResponse.Failure(400, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPost("{id:guid}/submissions/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> UploadSubmissions(
        Guid id,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        if (files is null || files.Count == 0)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn ít nhất một file .docx bài làm của sinh viên."));

        var docFiles = files.Select(f => (Application.Common.Interfaces.IDocumentFile)new WebApi.Common.FormFileDocumentAdapter(f)).ToList();
        var result = await _submissionService.UploadAndGradeBatchAsync(id, docFiles, userId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(idStr, out userId);
    }

    private bool IsElevatedRole()
    {
        var discriminator = User.FindFirstValue("discriminator");
        if (string.Equals(discriminator, "Admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var roleStr = User.FindFirstValue(ClaimTypes.Role);
        return roleStr == "2" || string.Equals(roleStr, "Admin", StringComparison.OrdinalIgnoreCase) || User.IsInRole("Admin");
    }
}
