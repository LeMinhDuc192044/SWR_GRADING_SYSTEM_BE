using Application.Common;
using Application.DTOs.Reports;

namespace Application.Interfaces;

/// <summary>
/// Service báo cáo & thống kê kỳ thi thực hành dành riêng cho Admin/Khảo thí
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Thống kê tổng quan kỳ thi: số bài nộp, tiến độ chấm, điểm trung bình/cao nhất/thấp nhất
    /// </summary>
    Task<Result<ExaminationOverviewDto>> GetExaminationOverviewAsync(Guid examinationId, CancellationToken ct = default);

    /// <summary>
    /// Phổ điểm thực hành (0-2, 2-4, 4-6, 6-8, 8-10) để đánh giá độ khó đề thi
    /// </summary>
    Task<Result<ScoreDistributionDto>> GetScoreDistributionAsync(Guid examinationId, CancellationToken ct = default);

    /// <summary>
    /// Xuất toàn bộ bảng điểm của kỳ thi ra file Excel (.xlsx) để nộp phòng Đào tạo
    /// </summary>
    Task<Result<ExportReportFileDto>> ExportExaminationScoresAsync(Guid examinationId, CancellationToken ct = default);

    /// <summary>
    /// Báo cáo phổ điểm so sánh song song giữa AI và Human trên các bài có trạng thái OFFICIAL (lọc theo Kỳ thi hoặc Học kỳ).
    /// </summary>
    Task<Result<ScoreDistributionComparisonDto>> GetScoreDistributionComparisonAsync(
        Guid? examinationId, Guid? semesterId, CancellationToken ct = default);

    /// <summary>
    /// Báo cáo thống kê chi tiết điểm số & phân tích đối chiếu sai số giữa AI và Human trên các bài OFFICIAL (lọc theo Kỳ thi hoặc Học kỳ).
    /// </summary>
    Task<Result<AiHumanScoreStatisticsDto>> GetAiHumanScoreStatisticsAsync(
        Guid? examinationId, Guid? semesterId, CancellationToken ct = default);

    /// <summary>
    /// Báo cáo hiệu suất cán bộ chấm thi (Monitor) theo lượng bài phân công và hoàn thành (có phân trang & lọc theo Kỳ thi / Học kỳ).
    /// </summary>
    Task<Result<MonitorPerformanceReportDto>> GetMonitorPerformanceAsync(
        Guid? examinationId, Guid? semesterId, string? search, PagedRequest request, CancellationToken ct = default);
}
