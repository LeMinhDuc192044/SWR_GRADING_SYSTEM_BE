using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller báo cáo & thống kê kỳ thi thực hành dành riêng cho Ban Khảo thí / Admin.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = "Admin")]
public sealed class AdminReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public AdminReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// GET /api/admin/reports/examinations/{examId}/overview
    /// Dashboard tổng quan kỳ thi: số bài nộp, số bài đã chốt điểm, điểm TB, cao nhất, thấp nhất.
    /// </summary>
    [HttpGet("examinations/{examId:guid}/overview")]
    public async Task<IActionResult> GetExaminationOverview(Guid examId, CancellationToken ct)
    {
        var result = await _reportService.GetExaminationOverviewAsync(examId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "EXAM_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// GET /api/admin/reports/examinations/{examId}/score-distribution
    /// Phổ điểm bài thi thực hành chia theo các thang điểm (0-2, 2-4, 4-6, 6-8, 8-10).
    /// </summary>
    [HttpGet("examinations/{examId:guid}/score-distribution")]
    public async Task<IActionResult> GetScoreDistribution(Guid examId, CancellationToken ct)
    {
        var result = await _reportService.GetScoreDistributionAsync(examId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        if (result.ErrorCode == "EXAM_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    /// <summary>
    /// GET /api/admin/reports/examinations/{examId}/export
    /// Xuất toàn bộ bảng điểm của kỳ thi ra file Excel (.xlsx) để nộp phòng Đào tạo.
    /// </summary>
    [HttpGet("examinations/{examId:guid}/export")]
    public async Task<IActionResult> ExportScores(Guid examId, CancellationToken ct)
    {
        var result = await _reportService.ExportExaminationScoresAsync(examId, ct);
        if (result.IsSuccess && result.Data is not null)
        {
            return File(result.Data.FileBytes, result.Data.ContentType, result.Data.FileName);
        }

        if (result.ErrorCode == "EXAM_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));

        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }
}
