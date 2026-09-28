using Domain.Enums;

namespace Application.DTOs.GradingDiaries;

public sealed class CreateGradingDiaryRequest
{
    public Guid PaperSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public sealed class UpdateGradingDiaryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public sealed class GradingDiaryResponseDTO
{
    public Guid GradingDiaryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public Guid CreateById { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public int SubmissionsCount { get; set; }
}

public sealed class GradingDiaryDetailDTO
{
    public Guid GradingDiaryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public string? FileAnswerRubric { get; set; }
    public Guid CreateById { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public IReadOnlyList<SubmissionItemDTO> Submissions { get; set; } = Array.Empty<SubmissionItemDTO>();
}

public sealed class SubmissionItemDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// DTO theo dõi tiến độ chấm thi của một sổ chấm (Grading Diary).
/// </summary>
public sealed class GradingDiaryProgressDto
{
    public Guid DiaryId { get; set; }
    public string DiaryName { get; set; } = string.Empty;
    public string PaperSetCode { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int SubmittedCount { get; set; } // Số bài mới upload (chưa chấm)
    public int AiGradedCount { get; set; } // Đã qua AI chấm
    public int LecturerReviewedCount { get; set; } // GV đã chấm/review
    public int FinalizedCount { get; set; } // Đã chốt điểm Final
    public double ProgressPercent { get; set; } // Tỷ lệ đã chốt điểm trên tổng số bài
}

/// <summary>
/// DTO so sánh độ lệch điểm giữa Giảng viên và AI.
/// Giúp giảng viên phát hiện các bài thi có sự chênh lệch điểm lớn để rà soát lại.
/// </summary>
public sealed class AiComparisonItemDto
{
    public Guid SubmissionId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string SubmissionFile { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public decimal? ScoreDifference { get; set; } // |LecturerScore - AiScore|
    public SubmissionStatus Status { get; set; }
    public string Comment { get; set; } = string.Empty;
}
