using Domain.Enums;

namespace Application.DTOs.GradingDiaries;

public sealed class SubmitLecturerScoreRequest
{
    public decimal LecturerScore { get; set; }
}

public sealed class SubmissionDetailDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public Guid DiaryId { get; set; }
    public string DiaryName { get; set; } = string.Empty;
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string AiComment { get; set; } = string.Empty;
    public IReadOnlyList<CriterionScoreDto> CriteriaScores { get; set; } = Array.Empty<CriterionScoreDto>();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
