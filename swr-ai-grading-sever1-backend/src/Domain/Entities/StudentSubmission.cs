using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("student_submission")]
public class StudentSubmission
{
    [Key]
    [Column("submission_id")]
    public Guid SubmissionId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(500)]
    [Column("file_submissions")]
    public string FileSubmissions { get; set; } = string.Empty;

    [Required]
    [Column("student_id")]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

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
    [Column("uploaded_by_id")]
    public Guid UploadedById { get; set; }

    [ForeignKey(nameof(UploadedById))]
    public Staff UploadedBy { get; set; } = null!;

    [Column("examination_session_id")]
    [ForeignKey(nameof(ExaminationSession))]
    public Guid? ExaminationSessionId { get; set; }
    public ExaminationSession? ExaminationSession { get; set; }
    public Application? Application { get; set; }

    public ICollection<SubmissionParsedAnswer> ParsedAnswers { get; set; } = [];
    public ICollection<SubmissionGradingHistory> SubmissionGradingHistories { get; set; } = [];
    public ICollection<Assignment> Assignments { get; set; } = [];
}
