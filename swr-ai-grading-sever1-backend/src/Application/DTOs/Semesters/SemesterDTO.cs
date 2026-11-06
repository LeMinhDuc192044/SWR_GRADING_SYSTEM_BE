using Domain.Enums;
using Application.DTOs.Examinations;

namespace Application.DTOs.Semesters;
public class SemesterDTO
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
    public SemesterStatus Status { get; set; }
}

public sealed class SemesterDetailDTO : SemesterDTO
{
    public IReadOnlyList<ExaminationDTO> Examinations { get; set; } = [];
    public IReadOnlyList<SemesterPaperSetDTO> PaperSets { get; set; } = [];
}

public sealed class SemesterPaperSetDTO
{
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public PaperSetStatus Status { get; set; }
    public Guid? ExaminationId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string? FileQuestionDocs { get; set; }
    public string? FileAnswerRubric { get; set; }
    public string? FileAnswerTemplate { get; set; }
}

public class CreateSemesterRequest
{
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SemesterStatus Status { get; set; }
}

public class UpdateSemesterRequest
{
    public string? SemesterCode { get; set; }
    public string? Name { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public SemesterStatus? Status { get; set; }
}

public class UpdateSemesterStatusRequest
{
    public SemesterStatus Status { get; set; }
}

public sealed class AddStudentToSemesterRequest
{
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;
    public DateOnly StudentBirthday { get; set; }
    public string Major { get; set; } = string.Empty;
    public string MonitorCode { get; set; } = string.Empty;
    public string MonitorName { get; set; } = string.Empty;
    public string MonitorEmail { get; set; } = string.Empty;
    public DateOnly MonitorBirthday { get; set; }
}

public sealed class StudentSemesterDTO
{
    public Guid StudentId { get; set; }
    public Guid SemesterId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string LecturerName { get; set; } = string.Empty;
    public string LecturerCode { get; set; } = string.Empty;
}

public sealed class StudentSemesterImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<StudentSemesterImportErrorDto> Errors { get; set; } = [];
    public List<StudentSemesterDTO> ImportedStudents { get; set; } = [];
}

public sealed class StudentSemesterImportErrorDto
{
    public int RowIndex { get; set; }
    public string? StudentCode { get; set; }
    public string Reason { get; set; } = string.Empty;
}
