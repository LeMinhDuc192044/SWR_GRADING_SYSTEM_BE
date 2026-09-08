using AI_Assisted_SWR_Grading_System.Domain.Enums;


namespace AI_Assisted_SWR_Grading_System.Domain.Entities;
public class ExamMaterial
{
    public Guid ExamMaterialId { get; set; } = Guid.NewGuid();
    public string ExamMaterialCode { get; set; } = string.Empty;
    public string? FileDocs { get; set; }
    public string? FileRubric { get; set; }
    public string? FileAnswerTemplate { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public bool IsDeleted { get; set; }
    public ExamMaterialStatus Status { get; set; }

    public Guid CreateById { get; set; }
    public Lecturer CreateBy { get; set; } = null!;

    public Guid ExaminationId { get; set; }
    public Examination Examination { get; set; } = null!;
}