using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace Domain.Entities;

[Table("staff")]
public class Staff : User
{
    [Required]
    [Column("role")]
    public UserRole Role { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = [];
    public ICollection<StudentSubmission> UploadedSubmissions { get; set; } = [];
    public ICollection<PaperSet> UploadedPaperSets { get; set; } = [];
}
