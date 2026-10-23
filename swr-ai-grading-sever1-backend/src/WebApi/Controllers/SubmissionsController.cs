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
[Authorize(Roles = "Lecturer,Admin")]
public sealed class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _submissionService;

    public SubmissionsController(ISubmissionService submissionService)
    {
        _submissionService = submissionService;
    }

    /// <summary>
    /// FLOW 1: Upload bài làm Word (.docx) hoặc archive RAR chứa Word và sơ đồ.
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
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn ít nhất một file .docx hoặc .rar bài làm của sinh viên."));

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
    /// FLOW 2: Kích hoạt AI chấm điểm tự động dựa trên câu trả lời bóc tách và rubric.
    /// </summary>
    [HttpPost("{id:guid}/ai-grade")]
    public async Task<IActionResult> AiGrade(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.TriggerAiGradingAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// FLOW 3: Giảng viên chấm / đánh giá bài nộp lần đầu (nhập điểm hoặc chọn pick điểm AI).
    /// </summary>
    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewSubmissionRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.ReviewAsync(id, request, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode is "INVALID_SCORE" or "SCORE_REQUIRED" or "AI_EVALUATION_NOT_FOUND")
            return BadRequest(ApiResponse.Failure(400, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// Chỉnh sửa trực tiếp 1 chi tiết chấm điểm (item detail) theo detailId mà không tạo thêm evaluation mới.
    /// Tự động cập nhật lại tổng điểm của bài nộp.
    /// </summary>
    [HttpPut("item-details/{detailId:guid}")]
    public async Task<IActionResult> UpdateItemDetail(
        Guid detailId,
        [FromBody] UpdateEvaluationItemDetailRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.UpdateItemDetailAsync(detailId, request, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode is "DETAIL_NOT_FOUND" or "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        if (result.ErrorCode == "FORBIDDEN")
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Failure(403, result.Error!));

        if (result.ErrorCode == "INVALID_SCORE")
            return BadRequest(ApiResponse.Failure(400, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// FLOW 3: Chốt điểm cuối cùng cho bài nộp (trạng thái chuyển sang Final).
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

    /// <summary>
    /// GET /api/submissions/{id}/history
    /// Xem chi tiết lịch sử và các mốc thời gian xử lý bài thi (Audit log: Nộp -> AI chấm -> GV review -> Final).
    /// </summary>
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _submissionService.GetHistoryAsync(id, userId, IsElevatedRole(), ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
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
