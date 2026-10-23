using Domain.Enums;

namespace Application.DTOs.GradingDiaries;

public sealed class SubmitLecturerScoreRequest
{
    public decimal LecturerScore { get; set; }
    public string? Comment { get; set; }
}

public sealed class EvaluationItemReviewDto
{
    public Guid QuestionId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public string? NotRecognized { get; set; }
}

public sealed class ReviewSubmissionRequest
{
    public decimal? LecturerScore { get; set; }
    public string? Comment { get; set; }
    public bool PickAiScore { get; set; }
    public bool IsFinal { get; set; }
    public string? SelectionReason { get; set; }
    public List<EvaluationItemReviewDto>? ItemDetails { get; set; }
}

public sealed class EvaluationItemDetailDto
{
    public Guid DetailId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid ParsedAnswerId { get; set; }
    public Guid? QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public string? NotRecognized { get; set; }
    public Guid? AiExtractionId { get; set; }
}

public sealed class SubmissionDetailDTO
{
    public Guid SubmissionId { get; set; }
    public string FileSubmissions { get; set; } = string.Empty;
    public string SubmissionFile { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public Guid StudentId { get; set; }
    public Guid DiaryId { get; set; }
    public Guid StudentExaminationId { get; set; }
    public string DiaryName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public string? AiLogs { get; set; }
    public decimal? LecturerScore { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsFinalScore { get; set; }
    public string? SelectionReason { get; set; }
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public IReadOnlyList<CriterionScoreDto> CriteriaScores { get; set; } = Array.Empty<CriterionScoreDto>();
    public IReadOnlyList<EvaluationItemDetailDto> ItemDetails { get; set; } = Array.Empty<EvaluationItemDetailDto>();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
