using Domain.Enums;

namespace Application.DTOs.Examinations;

/// <summary>
/// One examination session of a student, with only that student's own information.
/// </summary>
public sealed class StudentExaminationSessionDTO
{
    public Guid ExaminationSessionId { get; set; }
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public ExaminationSessionStatus Status { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? PaperSetId { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
    public ExaminationSessionSemesterDTO? Semester { get; set; }
    public ExaminationSessionExaminationDTO? Examination { get; set; }
    public ExaminationSessionStudentDTO? Student { get; set; }
}
