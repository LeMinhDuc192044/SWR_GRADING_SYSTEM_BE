using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/submissions")]
[Authorize]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>
    /// FLOW 1: Upload danh sách bài làm sinh viên (.docx) vào sổ chấm.
    /// </summary>
    [HttpPost("~/api/grading-diaries/{diaryId:guid}/submissions/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> Upload(
        [FromRoute] Guid diaryId,
        [FromForm] List<IFormFile> files,
        CancellationToken ct)
    {
        if (diaryId == Guid.Empty)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng cung cấp diaryId hợp lệ trên URL route."));

        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        if (files is null || files.Count == 0)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn ít nhất một file .docx bài làm của sinh viên."));

        var docFiles = files.Select(f => (IDocumentFile)new FormFileDocumentAdapter(f)).ToList();
        var result = await _submissionService.UploadSubmissionsAsync(diaryId, docFiles, userId, ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// Lấy danh sách toàn bộ bài nộp trong sổ chấm (Grading Diary).
    /// </summary>
    [HttpGet("~/api/grading-diaries/{diaryId:guid}/submissions")]
    public async Task<IActionResult> GetList(
        [FromRoute] Guid diaryId,
        CancellationToken ct)
    {
        if (diaryId == Guid.Empty)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng cung cấp diaryId hợp lệ trên URL route."));

        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.GetListByDiaryIdAsync(diaryId, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "DIARY_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bài nộp (bao gồm điểm AI, điểm giảng viên, tiêu chí chi tiết).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.GetByIdAsync(id, userId, IsElevatedRole(), ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// FLOW 2: Kích hoạt Gemini AI tự động chấm bài theo Rubric.
    /// </summary>
    [HttpPost("{id:guid}/ai-grade")]
    public async Task<IActionResult> TriggerAiGrade(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.TriggerAiGradingAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND" || result.ErrorCode == "PAPER_SET_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// FLOW 3: Giảng viên xem xét bài nộp và nhập điểm/nhận xét (trạng thái chuyển sang Lecturer_Reviewed).
    /// </summary>
    [HttpPut("{id:guid}/review")]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewSubmissionRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.ReviewAsync(id, request.LecturerScore, request.Comment, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode == "INVALID_SCORE")
            return BadRequest(ApiResponse.Failure(400, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// FLOW 4: Chốt điểm cuối cùng cho bài nộp (trạng thái chuyển sang Final).
    /// </summary>
    [HttpPut("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.FinalizeAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode == "INVALID_STATUS")
            return BadRequest(ApiResponse.Failure(400, result.Error!));

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
