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
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
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
