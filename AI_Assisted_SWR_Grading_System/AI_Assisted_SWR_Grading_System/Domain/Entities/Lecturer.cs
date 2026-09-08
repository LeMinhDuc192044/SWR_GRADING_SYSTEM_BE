using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

public class Lecturer : User
{
    [Required]
    [MaxLength(20)]
    [Column("lecturer_code")]
    public string LecturerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public ICollection<ExamMaterial> ExamMaterialsCreated { get; set; } = new List<ExamMaterial>();
}