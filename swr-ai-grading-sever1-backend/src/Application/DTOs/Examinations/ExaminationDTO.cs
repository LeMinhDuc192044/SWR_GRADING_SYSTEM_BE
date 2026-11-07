using System.Text.Json.Serialization;
using Application.DTOs.Questions;
using Domain.Enums;

namespace Application.DTOs.Examinations;

public class ExaminationDTO
{
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ExaminationType ExaminationType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
    public ExaminationStatus Status { get; set; }
    public Guid SemesterId { get; set; }
    public IReadOnlyList<Guid> PaperSetIds { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ExaminationPaperSetDTO>? PaperSets { get; set; }
}


public class CreateExaminationRequest
{
    public string Name { get; set; } = string.Empty;
    public ExaminationType ExaminationType { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus Status { get; set; } = ExaminationStatus.Draft;
    public Guid SemesterId { get; set; }
    public List<Guid> PaperSetIds { get; set; } = [];
}

public sealed class AddPaperSetsToExaminationRequest
{
    public List<Guid> PaperSetIds { get; set; } = [];
}

public class UpdateExaminationRequest
{
    public string? Name { get; set; }
    public ExaminationType? ExaminationType { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeSpan? StartTime { get; set; }
    public int? DurationMinutes { get; set; }
    public int? BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus? Status { get; set; }
    public Guid? SemesterId { get; set; }
    public Guid? PaperSetId { get; set; }
    public List<Guid>? PaperSetIds { get; set; }
}

public sealed class ExaminationPaperSetDTO
{
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;     // ASSUMPTION: property names match your PaperSet entity
    public string Description { get; set; } = string.Empty;      // ASSUMPTION
    public int TotalQuestions { get; set; }                      // ASSUMPTION
    public PaperSetStatus Status { get; set; }
    public IReadOnlyList<QuestionDTO> Questions { get; set; } = [];   // each question already carries its Criteria list
}