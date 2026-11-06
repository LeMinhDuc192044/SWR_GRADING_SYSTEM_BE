using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("student_examination_sessions")]
public class StudentExaminationSession
{
    [Required]
    [Column("student_id")]
    public Guid StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Required]
    [Column("examination_session_id")]
    public Guid ExaminationSessionId { get; set; }

    [ForeignKey(nameof(ExaminationSessionId))]
    public ExaminationSession ExaminationSession { get; set; } = null!;
}