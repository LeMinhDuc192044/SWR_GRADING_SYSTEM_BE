namespace Application.DTOs.Reports;

/// <summary>
/// Một bài nộp đã được chốt điểm chính thức (SubmissionGradingHistoryStatus.OFFICIAL)
/// kèm thông tin sinh viên, kỳ thi, học kỳ, bộ đề và chi tiết điểm từng câu.
/// </summary>
public sealed class OfficialSubmissionListItemDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string ExaminationName { get; set; } = string.Empty;
    public string ExaminationCode { get; set; } = string.Empty;
    public string PaperSetCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public string SemesterCode { get; set; } = string.Empty;
    public decimal? OfficialScore { get; set; }
    public string GradedBy { get; set; } = string.Empty;
    public Guid? GradedById { get; set; }

    /// <summary>Lịch sử chấm điểm OFFICIAL được dùng làm điểm chính thức.</summary>
    public Guid HistoryId { get; set; }
    public DateTime GradedAt { get; set; }

    /// <summary>Chi tiết điểm chỉ của lịch sử chấm OFFICIAL.</summary>
    public IReadOnlyList<OfficialSubmissionDetailDto> Details { get; set; } = [];
}

public sealed class OfficialSubmissionDetailDto
{
    public Guid DetailId { get; set; }
    public Guid ParsedAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}
