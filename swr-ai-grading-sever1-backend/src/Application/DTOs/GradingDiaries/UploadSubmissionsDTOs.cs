namespace Application.DTOs.GradingDiaries;

public sealed class UploadedSubmissionSummaryDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public IReadOnlyList<CriterionScoreDto> CriteriaScores { get; set; } = Array.Empty<CriterionScoreDto>();
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UploadSubmissionsResultDTO
{
    public Guid GradingDiaryId { get; set; }
    public int TotalUploaded { get; set; }
    public int TotalGradedSuccessfully { get; set; }
    public IReadOnlyList<UploadedSubmissionSummaryDTO> Submissions { get; set; } = Array.Empty<UploadedSubmissionSummaryDTO>();
}
