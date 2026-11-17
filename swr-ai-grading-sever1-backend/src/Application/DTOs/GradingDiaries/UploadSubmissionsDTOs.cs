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
    public Guid StudentExaminationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
    public bool IsReplaced { get; set; }

    // Set for exam-session bundle uploads: the parsed student code and the folder it came from.
    public string? StudentCode { get; set; }
    public string? FolderName { get; set; }
}

public sealed class UploadSubmissionsResultDTO
{
    public int TotalUploaded { get; set; }
    public int TotalSucceeded { get; set; }
    public IReadOnlyList<UploadedSubmissionSummaryDTO> Submissions { get; set; } = Array.Empty<UploadedSubmissionSummaryDTO>();
}
