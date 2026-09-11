using Domain.Enums;

namespace Application.DTOs.Examinations;

public class ExaminationDTO
{
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExaminationType ExaminationType { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus Status { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? ExamMaterialId { get; set; }
}

public class CreateExaminationRequest
{
    public string Name { get; set; } = string.Empty;
    public ExaminationType ExaminationType { get; set; }
    public DateOnly StartDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus Status { get; set; } = ExaminationStatus.Draft;
    public Guid SemesterId { get; set; }
    public Guid ExamMaterialId { get; set; }
}

public class UpdateExaminationRequest
{
    public string? Name { get; set; }
    public ExaminationType? ExaminationType { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public int? DurationMinutes { get; set; }
    public int? BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus? Status { get; set; }
    public Guid? SemesterId { get; set; }
    public Guid? ExamMaterialId { get; set; }
}