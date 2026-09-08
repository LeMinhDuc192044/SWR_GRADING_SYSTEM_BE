using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

[Table("submissions")]
public class Submission
{
    [Key]
    [Column("submission_id")]
    public Guid SubmissionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("submission_name")]
    public string SubmissionName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("folder")]
    public string Folder { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    public SubmissionStatus Status { get; set; }

    [Required]
    [Column("created_date")]
    public DateTime CreatedDate { get; set; }

    [Required]
    [Column("updated_date")]
    public DateTime UpdatedDate { get; set; }

    [Required]
    [Column("lecturer_id")]
    [ForeignKey(nameof(Lecturer))]
    public Guid LecturerId { get; set; }

    public Lecturer Lecturer { get; set; } = null!;

    public ICollection<Grading> Gradings { get; set; } = new List<Grading>();
}