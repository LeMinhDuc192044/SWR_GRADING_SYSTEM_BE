using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;
public class Examination
{
    public Guid ExaminationId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty; 
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public int DurationMinutes { get; set; }
    public int BeforeTimeMinutes { get; set; }
    public string? Note { get; set; }
    public ExaminationStatus Status { get; set; }

    public Guid SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;

    public ICollection<ExamMaterial> ExamMaterials { get; set; } = new List<ExamMaterial>();
}
