using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller Báo cáo & Thống kê dành riêng cho Trưởng bộ môn (Manager) và Ban Khảo thí / Admin.
/// Cung cấp các báo cáo đối chiếu AI vs Human và đánh giá hiệu suất Monitor.
/// </summary>
[ApiController]
[Authorize(Roles = "Manager,Admin,Examiner")]
[Route("api/manager/reports")]
[Route("api/admin/reports/manager")]
public sealed class ManagerReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ManagerReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// GET /api/manager/reports/score-distribution
    /// Phổ điểm so sánh song song giữa AI và Human trên các bài thi đã chốt điểm chính thức (OFFICIAL).
    /// Hỗ trợ lọc theo Kỳ thi (examinationId) hoặc Học kỳ (semesterId).
    /// </summary>
    [HttpGet("score-distribution")]
    public async Task<IActionResult> GetScoreDistribution(
        [FromQuery] Guid? examinationId,
        [FromQuery] Guid? semesterId,
        CancellationToken ct)
    {
        var result = await _reportService.GetScoreDistributionComparisonAsync(examinationId, semesterId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// GET /api/manager/reports/ai-human-statistics
    /// Thống kê chi tiết & phân tích đối chiếu sai số điểm giữa AI và Human (Status: OFFICIAL).
    /// Hỗ trợ lọc theo Kỳ thi (examinationId) hoặc Học kỳ (semesterId).
    /// </summary>
    [HttpGet("ai-human-statistics")]
    public async Task<IActionResult> GetAiHumanStatistics(
        [FromQuery] Guid? examinationId,
        [FromQuery] Guid? semesterId,
        CancellationToken ct)
    {
        var result = await _reportService.GetAiHumanScoreStatisticsAsync(examinationId, semesterId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>
    /// GET /api/manager/reports/monitor-performance
    /// Báo cáo hiệu suất cán bộ chấm thi (Monitor) theo lượng bài phân công và hoàn thành.
    /// Hỗ trợ phân trang chuẩn PagedRequest (page, pageSize) và tìm kiếm (search).
    /// Hỗ trợ lọc theo Kỳ thi (examinationId) hoặc Học kỳ (semesterId).
    /// </summary>
    [HttpGet("monitor-performance")]
    public async Task<IActionResult> GetMonitorPerformance(
        [FromQuery] PagedRequest request,
        [FromQuery] Guid? examinationId,
        [FromQuery] Guid? semesterId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _reportService.GetMonitorPerformanceAsync(examinationId, semesterId, search, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }
}
