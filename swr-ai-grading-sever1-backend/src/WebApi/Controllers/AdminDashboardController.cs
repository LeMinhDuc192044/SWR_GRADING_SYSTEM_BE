using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Dashboard thống kê học kỳ và hiệu suất giảng viên dành cho Ban Giám hiệu / Admin.
/// </summary>
[ApiController]
[Route("api/admin/dashboard")]
[Authorize(Roles = "Admin")]
public sealed class AdminDashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public AdminDashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>
    /// GET /api/admin/dashboard/semesters-statistics
    /// Xem thống kê điểm, tỷ lệ đạt/trượt và phổ điểm của từng học kỳ.
    /// </summary>
    [HttpGet("semesters-statistics")]
    public async Task<IActionResult> GetSemestersStatistics(CancellationToken ct)
    {
        var result = await _dashboardService.GetAdminSemestersStatisticsAsync(ct);
        return Ok(ApiResponse.Success(result.Data));
    }

    /// <summary>
    /// GET /api/admin/dashboard/lecturers-performance?semesterId=...
    /// Xem thống kê hiệu suất, khối lượng chấm bài và tiến độ hoàn thành của từng giảng viên.
    /// </summary>
    [HttpGet("lecturers-performance")]
    public async Task<IActionResult> GetLecturersPerformance([FromQuery] Guid? semesterId, CancellationToken ct)
    {
        var result = await _dashboardService.GetAdminLecturersPerformanceAsync(semesterId, ct);
        return Ok(ApiResponse.Success(result.Data));
    }
}
