using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Dashboard tổng quan chấm điểm dành cho Giảng viên (khớp 100% UI biểu đồ).
/// </summary>
[ApiController]
[Route("api/lecturer/dashboard")]
[Authorize(Roles = "Lecturer,Admin")]
public sealed class LecturerDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public LecturerDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// GET /api/lecturer/dashboard?semesterId=...
    /// Lấy toàn bộ dữ liệu KPI, Phổ điểm (10 cột), Tiến độ chấm (Donut chart), và So sánh các học kỳ.
    /// Nếu không truyền semesterId -> Thống kê cho tất cả học kỳ.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard([FromQuery] Guid? semesterId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized(ApiResponse.Failure(401, "Không xác thực được danh tính người dùng."));

        var result = await _dashboardService.GetLecturerDashboardAsync(userId, semesterId, ct);
        return Ok(ApiResponse.Success(result.Data));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(idStr, out userId);
    }
}
