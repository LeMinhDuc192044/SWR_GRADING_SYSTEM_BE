using AI_Assisted_SWR_Grading_System.Domain.Enums;


namespace AI_Assisted_SWR_Grading_System.Domain.Entities;
public class Semester
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public SemesterStatus Status { get; set; }

    public ICollection<Examination> Examinations { get; set; } = new List<Examination>();
}
