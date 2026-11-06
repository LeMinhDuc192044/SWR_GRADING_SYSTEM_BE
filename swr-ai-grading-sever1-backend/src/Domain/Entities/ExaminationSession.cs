using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;


namespace Domain.Entities;

[Table("examination_sessions")]
public class ExaminationSession
{
    [Key]
    [Column("examination_sessions_id")]
    public Guid ExaminationSessionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("examination_code")]
    public string ExaminationCode { get; set; } = string.Empty;

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column("start_time", TypeName = "time")]
    public TimeOnly StartTime { get; set; }

    [Required]
    [Column("created_day")]
    public DateTime CreatedDay { get; set; }

    [Required]
    [Column("updated_day")]
    public DateTime UpdatedDay { get; set; }

    [Required]
    [Column("status")]
    public ExaminationSessionStatus Status { get; set; }

    [Required]
    [Column("semester_id")]
    [ForeignKey(nameof(Semester))]
    public Guid SemesterId { get; set; }

    [Required]
    [Column("examination_id")]
    [ForeignKey(nameof(Examination))]
    public Guid ExaminationId { get; set; }
    public Examination Examination { get; set; } = null!;

    [Column("paper_set_id")]
    public Guid? PaperSetId { get; set; }

    [ForeignKey(nameof(PaperSetId))]
    public PaperSet? PaperSet { get; set; }
    public ICollection<GradingSession> GradingSessions { get; set; } = [];
    public ICollection<StudentSubmission> StudentSubmissions { get; set; } = [];
    public ICollection<StudentExaminationSession> StudentExaminationSessions { get; set; } = [];
}