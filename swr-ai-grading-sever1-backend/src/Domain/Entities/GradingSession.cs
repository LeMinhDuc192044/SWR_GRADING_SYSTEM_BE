using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("grading_sessions")]
public class GradingSession
{
    [Key]
    [Column("grading_session_id")]
    public Guid GradingSessionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("created_date")]
    public DateOnly CreatedDate { get; set; }

    [Required]
    [Column("updated_date")]
    public DateOnly UpdatedDate { get; set; }

    [Column("deadline")]
    public DateOnly? Deadline { get; set; }

    [Required]
    [Column("number_submissions")]
    public int NumberSubmissions { get; set; }

    public ICollection<Assignment> Assignments { get; set; } = [];

    [Required]
    [Column("examination_session_id")]
    public Guid ExaminationSessionId { get; set; }

    [ForeignKey(nameof(ExaminationSessionId))]
    public ExaminationSession ExaminationSession { get; set; } = null!;
}
