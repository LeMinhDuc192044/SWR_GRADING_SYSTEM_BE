using Domain.Enums;

namespace Application.DTOs.Grading;

public sealed class GradingHistoryDTO
{
    public Guid HistoryId { get; set; }
    public Guid SubmissionId { get; set; }
    public EvaluationType EvaluationType { get; set; }
    public int GradingRound { get; set; }
    public decimal? TotalScore { get; set; }
    public string? GeneralFeedback { get; set; }
    public SubmissionGradingHistoryStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GradingItemDetailDTO> Details { get; set; } = [];
}

public sealed class GradingItemDetailDTO
{
    public Guid DetailId { get; set; }
    public Guid ParsedAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// Manual lecturer grading: one item per question, addressed by question number (1, 2, ... n).
/// </summary>
public sealed class CreateLecturerGradingRequest
{
    public string? GeneralFeedback { get; set; }
    public List<LecturerGradingItemRequest> Items { get; set; } = [];
}

public sealed class LecturerGradingItemRequest
{
    public int QuestionNumber { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}

public sealed class ReviewGradingRequest
{
    /// <summary>History to start from. Defaults to the latest one.</summary>
    public Guid? BaseHistoryId { get; set; }
    public string? GeneralFeedback { get; set; }
    /// <summary>Only the items the monitor changed.</summary>
    public List<ReviewItemRequest> Items { get; set; } = [];
}

public sealed class SubmissionGradingOverviewDTO
{
    public Guid SubmissionId { get; set; }
    public IReadOnlyList<ParsedAnswerDTO> ParsedAnswers { get; set; } = [];
    public IReadOnlyList<GradingHistoryDTO> Histories { get; set; } = [];
}

public sealed class ParsedAnswerDTO
{
    public Guid ParsedAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public IReadOnlyList<ParsedAnswerCriteriaDTO> Criteria { get; set; } = [];
    public Guid? ElementId { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractedXml { get; set; }
    public string? ExtractedImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ParsedAnswerCriteriaDTO
{
    public string Criteria { get; set; } = string.Empty;
    public decimal Point { get; set; }
}

public sealed class ReviewItemRequest
{
    public Guid ParsedAnswerId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}