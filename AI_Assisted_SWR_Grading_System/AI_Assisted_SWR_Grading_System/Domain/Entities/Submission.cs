using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

public class Submission
{
    public Guid SubmissionId { get; set; }

    public string Folder { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }

    public Guid LecturerId { get; set; }
    public Lecturer Lecturer { get; set; } = null!;

    public ICollection<Grading> Gradings { get; set; } = new List<Grading>();
}


