using Application.Common;
using Application.DTOs.Lecturers;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller quản lý tài khoản Giảng viên dành riêng cho Admin.
/// </summary>
[ApiController]
[Route("api/admin/lecturers")]
[Authorize(Roles = "Admin")]
public sealed class AdminLecturersController : ControllerBase
{
    private readonly ILecturerService _lecturerService;

    public AdminLecturersController(ILecturerService lecturerService)
    {
        _lecturerService = lecturerService;
    }

    /// <summary>
    /// GET /api/admin/lecturers
    /// Danh sách tất cả giảng viên kèm số đề thi và sổ chấm đang phụ trách.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await _lecturerService.GetLecturersAsync(ct);
        return Ok(ApiResponse.Success(result.Data));
    }

    /// <summary>
    /// PUT /api/admin/lecturers/{id}
    /// Cập nhật thông tin giảng viên (Họ tên, email, bộ môn giảng dạy).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLecturerRequest request, CancellationToken ct)
    {
        var result = await _lecturerService.UpdateLecturerAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "LECTURER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// PATCH /api/admin/lecturers/{id}/status
    /// Bật/Tắt trạng thái hoạt động (kích hoạt hoặc tạm khóa tài khoản giảng viên).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ToggleStatus(Guid id, CancellationToken ct)
    {
        var result = await _lecturerService.ToggleStatusAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(new { isActive = result.Data }, 200, "Cập nhật trạng thái giảng viên thành công."));

        if (result.ErrorCode == "LECTURER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }
}
