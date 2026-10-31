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
    [Column("diary_id")]
    [ForeignKey(nameof(GradingDiary))]
    public Guid DiaryId { get; set; }
    public GradingDiary GradingDiary { get; set; } = null!;

    [Required]
    [Column("student_examination_id")]
    [ForeignKey(nameof(StudentExamination))]
    public Guid StudentExaminationId { get; set; }
    public StudentExamination StudentExamination { get; set; } = null!;

    public ICollection<SubmissionParsedAnswer> ParsedAnswers { get; set; } = [];
    public ICollection<SubmissionEvaluation> Evaluations { get; set; } = [];
    public ICollection<RegradeApplication> RegradeApplications { get; set; } = [];
}
