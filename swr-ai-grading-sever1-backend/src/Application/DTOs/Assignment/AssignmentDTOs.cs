using Domain.Enums;
using System.Text.Json.Serialization;

namespace Application.DTOs.Assignments;

public sealed class AssignmentDTO
{
    public Guid AssignmentId { get; set; }
    public Guid GradingSessionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid StaffId { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTime AssignedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public bool IsReassigned { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssignmentStaffDTO? Staff { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssignmentStudentDTO? Student { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssignmentPaperSetDTO? PaperSet { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AssignmentExaminationSessionDTO? ExaminationSession { get; set; }
}

public sealed class AssignmentStaffDTO
{
    public Guid StaffId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class AssignmentStudentDTO
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public sealed class AssignmentPaperSetDTO
{
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
}

public sealed class AssignmentExaminationSessionDTO
{
    public Guid ExaminationSessionId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public string ExaminationName { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public string SemesterCode { get; set; } = string.Empty;
}

public sealed class CreateAssignmentRequest
{
    public Guid StaffId { get; set; }
    public int NumberSubmissions { get; set; }
    public List<Guid>? SubmissionIds { get; set; }
}

public sealed class UpdateAssignmentRequest
{
    public Guid? StaffId { get; set; }
    public AssignmentStatus? Status { get; set; }
}
