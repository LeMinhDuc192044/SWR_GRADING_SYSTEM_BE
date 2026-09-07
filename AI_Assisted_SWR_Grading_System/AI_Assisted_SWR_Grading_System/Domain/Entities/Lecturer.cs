namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

public class Lecturer : User
{
    public string Subject { get; set; } = string.Empty;

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public ICollection<ExamMaterial> ExamMaterialsCreated { get; set; } = new List<ExamMaterial>();
}