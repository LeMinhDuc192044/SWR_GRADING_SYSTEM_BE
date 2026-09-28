namespace Application.DTOs.Reports;

/// <summary>
/// DTO thể hiện phổ điểm bài thi thực hành theo từng khoảng điểm.
/// Dùng để vẽ biểu đồ phân bố điểm cho khảo thí đánh giá độ khó đề thi.
/// </summary>
public sealed class ScoreDistributionDto
{
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public int TotalGraded { get; set; }
    public IReadOnlyList<ScoreRangeDto> Ranges { get; set; } = [];
}

public sealed class ScoreRangeDto
{
    public string Label { get; set; } = string.Empty; // Ví dụ: "0 - 2đ", "2 - 4đ", ...
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
}
