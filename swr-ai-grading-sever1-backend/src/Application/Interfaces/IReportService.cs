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
}
