using Domain.Enums;

namespace Application.DTOs.Submissions;

/// <summary>
/// DTO thể hiện dòng thời gian (Audit log) của một bài thi.
/// Cho biết lịch sử: nộp bài -> AI chấm -> GV review -> chốt Final.
/// </summary>
public sealed class SubmissionHistoryDto
{
    public Guid SubmissionId { get; set; }
    public string SubmissionFile { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string DiaryName { get; set; } = string.Empty;

    public SubmissionStatus CurrentStatus { get; set; }
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public string? LecturerComment { get; set; }

    public IReadOnlyList<SubmissionHistoryTimelineEventDto> Timeline { get; set; } = [];
}

public sealed class SubmissionHistoryTimelineEventDto
{
    public string Stage { get; set; } = string.Empty; // "Submitted", "AI_Graded", "Lecturer_Reviewed", "Final"
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? Timestamp { get; set; }
    public bool IsCompleted { get; set; }
}
