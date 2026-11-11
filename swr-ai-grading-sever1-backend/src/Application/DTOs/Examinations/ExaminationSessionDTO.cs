using Domain.Enums;
using System.Text.Json.Serialization;

namespace Application.DTOs.Examinations;

public sealed class ExaminationSessionDTO
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
    public IReadOnlyList<Guid> StudentIds { get; set; } = [];
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionSemesterDTO? Semester { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionExaminationDTO? Examination { get; set; }
}

public sealed class ExaminationSessionListItemDTO
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
    public int TotalSubmission { get; set; }
    public IReadOnlyList<ExaminationSessionStudentDTO> Students { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionSemesterDTO? Semester { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionExaminationDTO? Examination { get; set; }
}

public sealed class ExaminationSessionStudentDTO
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public sealed class CreateExaminationSessionRequest
{
    public string ExaminationCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public ExaminationSessionStatus Status { get; set; } = ExaminationSessionStatus.SETUP;
    public Guid? PaperSetId { get; set; }
    public List<Guid> StudentIds { get; set; } = [];
}

public sealed class UpdateExaminationSessionRequest
{
    public string? ExaminationCode { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public ExaminationSessionStatus? Status { get; set; }
    public Guid? PaperSetId { get; set; }
    public List<Guid>? StudentIds { get; set; }
}


public sealed class ExaminationSessionSemesterDTO
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public sealed class ExaminationSessionExaminationDTO
{
    public Guid ExaminationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}