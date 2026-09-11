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
    public SemesterStatus Status { get; set; }
}

public sealed class SemesterDetailDTO : SemesterDTO
{
    public IReadOnlyList<ExaminationDTO> Examinations { get; set; } = [];
    public IReadOnlyList<SemesterExamMaterialDTO> ExamMaterials { get; set; } = [];
}

public sealed class SemesterExamMaterialDTO
{
    public Guid ExamMaterialId { get; set; }
    public string ExamMaterialCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public ExamMaterialStatus Status { get; set; }
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
