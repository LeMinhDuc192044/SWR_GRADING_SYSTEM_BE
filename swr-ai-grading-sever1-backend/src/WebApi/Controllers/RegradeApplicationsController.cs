using Application.Common;
using Application.DTOs.Regrades;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

/// <summary>
/// Controller xử lý đơn phúc khảo (Application) & quy trình chấm lại (Regrade).
/// </summary>
[ApiController]
[Route("api/regrade-applications")]
[Authorize]
public sealed class RegradeApplicationsController : ControllerBase
{
    private readonly IRegradeService _regradeService;

    public RegradeApplicationsController(IRegradeService regradeService)
    {
        _regradeService = regradeService;
    }

    /// <summary>
    /// POST /api/regrade-applications
    /// Sinh viên tạo đơn phúc khảo cho bài nộp của mình.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> CreateApplication([FromBody] CreateRegradeApplicationRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _regradeService.CreateApplicationAsync(request, userId, ct);
        return result.ToActionResult(this, 201, "Tạo đơn phúc khảo thành công.");
    }

    /// <summary>
    /// PATCH /api/regrade-applications/{id}/review
    /// Admin xét duyệt đơn phúc khảo: Từ chối hoặc Chấp thuận (nếu chấp thuận thì chọn 1 giảng viên chấm lại).
    /// Khi chấp thuận, trạng thái bài nộp tự động chuyển sang ReGrading.
    /// </summary>
    [HttpPatch("{id:guid}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReviewApplication(
        Guid id,
        [FromBody] ReviewRegradeApplicationRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _regradeService.ReviewApplicationAsync(id, request, userId, ct);
        var msg = request.IsApproved
            ? "Đã phê duyệt đơn phúc khảo và giao cho giảng viên chấm lại."
            : "Đã từ chối đơn phúc khảo.";
        return result.ToActionResult(this, 200, msg);
    }

    /// <summary>
    /// POST /api/regrade-applications/{id}/grade
    /// Giảng viên được phân công thực hiện chấm lại bài thi phúc khảo.
    /// Khi chấm xong, đơn chuyển sang Completed và trạng thái bài thi chuyển sang ReGraded.
    /// </summary>
    [HttpPost("{id:guid}/grade")]
    [Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> GradeApplication(
        Guid id,
        [FromBody] GradeRegradeApplicationRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _regradeService.GradeApplicationAsync(id, request, userId, ct);
        return result.ToActionResult(this, 200, "Đã hoàn thành chấm điểm phúc khảo.");
    }

    /// <summary>
    /// GET /api/regrade-applications/admin
    /// Admin xem danh sách tất cả các đơn phúc khảo (có lọc trạng thái, tìm kiếm, phân trang).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetApplicationsForAdmin(
        [FromQuery] GetRegradeApplicationsRequest request,
        CancellationToken ct)
    {
        var result = await _regradeService.GetApplicationsForAdminAsync(request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/regrade-applications/my-applications
    /// Sinh viên xem danh sách các đơn phúc khảo do chính mình tạo.
    /// </summary>
    [HttpGet("my-applications")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetApplicationsForStudent(
        [FromQuery] GetRegradeApplicationsRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _regradeService.GetApplicationsForStudentAsync(userId, request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/regrade-applications/assigned
    /// Giảng viên xem danh sách các đơn phúc khảo được giao cho mình chấm lại.
    /// </summary>
    [HttpGet("assigned")]
    [Authorize(Roles = "Lecturer")]
    public async Task<IActionResult> GetApplicationsForLecturer(
        [FromQuery] GetRegradeApplicationsRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _regradeService.GetApplicationsForLecturerAsync(userId, request, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/regrade-applications/{id}
    /// Xem chi tiết một đơn phúc khảo theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetApplicationById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
            ?? User.FindFirst("role")?.Value
            ?? string.Empty;

        var result = await _regradeService.GetApplicationByIdAsync(id, userId, role, ct);
        return result.ToActionResult(this);
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("nameid")?.Value
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out userId);
    }
}

