using System.Security.Claims;
using Application.Common;
using Application.DTOs.Applications;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/applications")]
public sealed class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// <summary>
    /// POST /api/applications
    /// Sinh viên nộp đơn phúc khảo cho bài thi đã có điểm chính thức.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateApplicationRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _applicationService.CreateAsync(request, userId, ct);
        return result.ToActionResult(this, StatusCodes.Status201Created, "Tạo đơn phúc khảo thành công.");
    }

    /// <summary>
    /// GET /api/applications/my
    /// Sinh viên xem danh sách các đơn phúc khảo của chính mình (hỗ trợ phân trang chuẩn PagedRequest).
    /// </summary>
    [HttpGet("my")]
    public async Task<IActionResult> GetMyApplications([FromQuery] PagedRequest request, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _applicationService.GetMyApplicationsAsync(request, userId, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/applications
    /// Ban Khảo thí / Trưởng bộ môn xem danh sách tất cả các đơn phúc khảo (hỗ trợ phân trang chuẩn PagedRequest).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager,Examiner")]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        [FromQuery] Guid? examinationSessionId,
        [FromQuery] ApplicationStatus? status,
        CancellationToken ct)
    {
        var result = await _applicationService.GetAllAsync(request, examinationSessionId, status, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/applications/{id}
    /// Xem chi tiết 1 đơn phúc khảo.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _applicationService.GetByIdAsync(id, userId, IsElevatedRole, ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// PUT /api/applications/{id}/status
    /// Ban Khảo thí / Trưởng bộ môn duyệt hoặc từ chối đơn phúc khảo.
    /// </summary>
    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateApplicationStatusRequest request,
        CancellationToken ct)
    {
        if (CurrentUserId is not { } userId)
            return Unauthorized(ApiResponse.Failure(StatusCodes.Status401Unauthorized, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _applicationService.UpdateStatusAsync(id, request, userId, ct);
        return result.ToActionResult(this, StatusCodes.Status200OK, "Cập nhật trạng thái đơn thành công.");
    }

    private Guid? CurrentUserId
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? User.FindFirstValue("nameid"), out var id) ? id : null;

    private bool IsElevatedRole
        => User.IsInRole("Admin") || User.IsInRole("Manager") || User.IsInRole("Examiner");
}
