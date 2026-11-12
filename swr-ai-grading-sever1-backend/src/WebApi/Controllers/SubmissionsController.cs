using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/submissions")]
// [Authorize(Roles = "Examiner,Admin")]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>
    /// FLOW 1: Upload bài làm Word (.docx), RAR (.rar) hoặc ZIP (.zip) đơn lẻ hay gói cả phòng thi.
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = "Examiner")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(
        [FromForm] List<IFormFile> files,
        [FromQuery] Guid? examinationSessionId,
        CancellationToken ct)
    {
        
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        if (files is null || files.Count == 0)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn ít nhất một file .docx, .rar hoặc .zip bài làm của sinh viên.", "FILES_REQUIRED"));

        var docFiles = files.Select(f => (IDocumentFile)new FormFileDocumentAdapter(f)).ToList();
        var result = await _submissionService.UploadSubmissionsAsync(docFiles, userId, examinationSessionId, ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!, result.ErrorCode));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// Lấy danh sách toàn bộ bài nộp.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _submissionService.GetListAsync(userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!, result.ErrorCode));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bài nộp (bao gồm điểm AI, điểm giảng viên, tiêu chí chi tiết).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _submissionService.GetByIdAsync(id, userId, IsElevatedRole(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!, result.ErrorCode));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// FLOW 2: Kích hoạt AI chấm điểm tự động dựa trên câu trả lời bóc tách và rubric.
    /// </summary>
    [HttpPost("{id:guid}/ai-grade")]
    public async Task<IActionResult> AiGrade(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _submissionService.TriggerAiGradingAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!, result.ErrorCode));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// GET /api/submissions/{id}/history
    /// Xem chi tiết lịch sử và các mốc thời gian xử lý bài thi (Audit log: Nộp -> AI chấm -> GV review -> Final).
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _submissionService.GetHistoryAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!, result.ErrorCode));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
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
        return string.Equals(roleStr, nameof(UserRole.Admin), StringComparison.OrdinalIgnoreCase) || User.IsInRole("Admin");
    }
}
