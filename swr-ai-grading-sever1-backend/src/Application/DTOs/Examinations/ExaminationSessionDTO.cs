using Domain.Enums;

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
    public IReadOnlyList<Guid> StudentIds { get; set; } = [];
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
