using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/grading-diaries")]
[Authorize(Roles = "Lecturer,Admin")]
public sealed class GradingDiariesController : ControllerBase
{
    private readonly IGradingDiaryService _service;

    public GradingDiariesController(IGradingDiaryService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateGradingDiaryRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.CreateAsync(request, userId, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.GradingDiaryId }, ApiResponse.Success(result.Data, 201, "Tạo sổ chấm thành công."));

        return result.ToActionResult(this);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        Guid? lecturerFilter = IsElevatedRole() ? null : userId;
        var pagedResult = await _service.GetPagedAsync(request, lecturerFilter, ct);
        return Ok(ApiResponse.Success(pagedResult));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.GetByIdAsync(id, userId, IsElevatedRole(), ct);
        return result.ToActionResult(this);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateGradingDiaryRequest request,
        CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.UpdateAsync(id, request, userId, IsElevatedRole(), ct);
        return result.ToActionResult(this, successMessage: "Cập nhật sổ chấm thành công.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.DeleteAsync(id, userId, IsElevatedRole(), ct);
        return result.ToActionResult(this, successMessage: "Xóa sổ chấm thành công.");
    }

    /// <summary>
    /// GET /api/grading-diaries/{id}/progress
    /// Thống kê tiến độ chấm bài của sổ chấm (tổng bài, số bài đã chốt, % hoàn thành).
    /// </summary>
    [HttpGet("{id:guid}/progress")]
    public async Task<IActionResult> GetProgress(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.GetProgressAsync(id, userId, IsElevatedRole(), ct);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// GET /api/grading-diaries/{id}/compare-with-ai
    /// Rà soát độ lệch điểm giữa Giảng viên và AI để phát hiện các bài thi chênh lệch điểm lớn.
    /// </summary>
    [HttpGet("{id:guid}/compare-with-ai")]
    public async Task<IActionResult> CompareWithAi(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng.", "UNAUTHORIZED"));

        var result = await _service.CompareWithAiAsync(id, userId, IsElevatedRole(), ct);
        return result.ToActionResult(this);
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
