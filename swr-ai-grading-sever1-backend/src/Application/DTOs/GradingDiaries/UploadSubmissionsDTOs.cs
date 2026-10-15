namespace Application.DTOs.GradingDiaries;

public sealed class UploadResultDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionFile { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UploadedSubmissionSummaryDTO
{
    public Guid SubmissionId { get; set; }
    public string FileSubmissions { get; set; } = string.Empty;
    public string SubmissionFile { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid DiaryId { get; set; }
    public Guid StudentExaminationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class UploadSubmissionsResultDTO
{
    public Guid GradingDiaryId { get; set; }
    public int TotalUploaded { get; set; }
    public int TotalSucceeded { get; set; }
    public IReadOnlyList<UploadedSubmissionSummaryDTO> Submissions { get; set; } = Array.Empty<UploadedSubmissionSummaryDTO>();
}
