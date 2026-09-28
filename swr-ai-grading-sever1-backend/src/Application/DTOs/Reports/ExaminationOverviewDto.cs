namespace Application.DTOs.Reports;

/// <summary>
/// DTO chứa các chỉ số tổng quan của một kỳ thi thực hành (PE).
/// Giúp Admin/Khảo thí nắm bắt nhanh số lượng bài nộp, tiến độ chấm và mặt bằng điểm số.
/// </summary>
public sealed class ExaminationOverviewDto
{
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public string ExaminationName { get; set; } = string.Empty;

    // Số lượng bài thi
    public int TotalSubmissions { get; set; }
    public int AiGradedCount { get; set; }
    public int LecturerReviewedCount { get; set; }
    public int FinalizedCount { get; set; }

    // Thống kê điểm số thực hành
    public decimal? AverageScore { get; set; }
    public decimal? HighestScore { get; set; }
    public decimal? LowestScore { get; set; }

    // Tỷ lệ hoàn thành chấm thi (%)
    public double CompletionRate { get; set; }
}
