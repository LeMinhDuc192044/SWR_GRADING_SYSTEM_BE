using Domain.Enums;

namespace Application.DTOs.Submissions;

/// <summary>
/// A submission of one student, with the semester and examination it belongs to.
/// </summary>
public sealed class StudentSubmissionDTO
{
    public Guid SubmissionId { get; set; }
    public string FileSubmissions { get; set; } = string.Empty;
    public string SubmissionFile => string.IsNullOrWhiteSpace(FileSubmissions)
        ? string.Empty
        : Path.GetFileName(FileSubmissions);
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public Guid? ExaminationSessionId { get; set; }
    public string? ExaminationCode { get; set; }
    public StudentSubmissionSemesterDTO? Semester { get; set; }
    public StudentSubmissionExaminationDTO? Examination { get; set; }

    /// <summary>Only OFFICIAL grading histories, each with its item details.</summary>
    public IReadOnlyList<StudentSubmissionGradingHistoryDTO> GradingHistories { get; set; } = [];

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class StudentSubmissionGradingHistoryDTO
{
    public Guid HistoryId { get; set; }
    public EvaluationType EvaluationType { get; set; }
    public string EvaluationTypeName => EvaluationType.ToString();
    public int GradingRound { get; set; }
    public decimal? TotalScore { get; set; }
    public string? GeneralFeedback { get; set; }
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<StudentSubmissionItemDetailDTO> Details { get; set; } = [];
}

public sealed class StudentSubmissionItemDetailDTO
{
    public Guid DetailId { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
    public Guid ParsedAnswerId { get; set; }
}

public sealed class StudentSubmissionSemesterDTO
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class StudentSubmissionExaminationDTO
{
    public Guid ExaminationId { get; set; }
    public string Name { get; set; } = string.Empty;
}
