using Application.Common;
using Application.DTOs.Submissions;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller cho Submission & Grading.
///
/// Endpoint mapping (theo Work Plan):
///   GET    /api/submissions                       → danh sách (paged)
///   GET    /api/submissions/{id}                  → chi tiết
///   POST   /api/submissions                       → tạo mới + upload file
///   PUT    /api/submissions/{id}                  → cập nhật metadata
///   DELETE /api/submissions/{id}                  → xóa (khi chưa có Grading)
///   GET    /api/submissions/{id}/file             → download file
///   POST   /api/submissions/{id}/gradings         → tạo Grading (mở đầu chấm AI)
///
/// Error code mapping → HTTP status (đồng nhất ExaminationController):
///   NOT_FOUND  (404): SUBMISSION_NOT_FOUND, LECTURER_NOT_FOUND, FILE_NOT_FOUND
///   CONFLICT   (409): SUBMISSION_HAS_GRADINGS
///   BAD_REQUEST(400): INVALID_*, FILE_*, GRADING_CODE_*
/// </summary>
[ApiController]
[Route("api/submissions")]
public class SubmissionsController : ControllerBase
{
    private readonly ISubmissionService _service;

    public SubmissionsController(ISubmissionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
        => Ok(ApiResponse.Success(await _service.GetPagedAsync(request, ct)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpPost]
    [DisableRequestSizeLimit] // submission file có thể lớn — chỉnh max ở config nếu cần
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreateSubmissionForm form,
        CancellationToken ct)
    {
        if (form is null)
            return BadRequest(ApiResponse.Failure(400, "Request body is required."));

        var request = new CreateSubmissionRequest
        {
            SubmissionName = form.SubmissionName ?? string.Empty,
            Folder = form.Folder ?? string.Empty,
            LecturerId = form.LecturerId,
            File = form.File is null ? null : new SubmissionFileUpload
            {
                Content = form.File.OpenReadStream(),
                FileName = form.File.FileName,
                ContentType = form.File.ContentType,
                Length = form.File.Length
            }
        };

        var result = await _service.CreateAsync(request, ct);
        return MapToAction(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSubmissionRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return MapToAction(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));
        if (result.ErrorCode == "SUBMISSION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "SUBMISSION_HAS_GRADINGS")
            return Conflict(ApiResponse.Failure(409, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> DownloadFile(Guid id, CancellationToken ct)
    {
        var result = await _service.DownloadFileAsync(id, ct);
        if (!result.IsSuccess)
        {
            if (result.ErrorCode is "SUBMISSION_NOT_FOUND" or "FILE_NOT_FOUND")
                return NotFound(ApiResponse.Failure(404, result.Error!));
            return BadRequest(ApiResponse.Failure(400, result.Error!));
        }

        var file = result.Data!;
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("{id:guid}/gradings")]
    public async Task<IActionResult> CreateGrading(
        Guid id,
        [FromBody] CreateGradingRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateGradingAsync(id, request, ct);
        return MapToAction(result);
    }

    // ====================== HELPERS ======================

    /// <summary>
    /// Form riêng cho multipart (chỉ dùng ở WebApi layer).
    /// Tránh để IFormFile lọt vào Application layer (Clean Architecture).
    /// </summary>
    public class CreateSubmissionForm
    {
        public string? SubmissionName { get; set; }
        public string? Folder { get; set; }
        public Guid LecturerId { get; set; }
        public IFormFile? File { get; set; }
    }

    private IActionResult MapToAction<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        // 404 cho các lỗi "không tìm thấy"
        if (result.ErrorCode is "SUBMISSION_NOT_FOUND" or "LECTURER_NOT_FOUND" or "FILE_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        // 400 cho phần còn lại
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }
}
