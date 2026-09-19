using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("student_submission")]
public class Submission
{
    [Key]
    [Column("submission_id")]
    public Guid SubmissionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("submission_name")]
    public string SubmissionName { get; set; } = string.Empty;

    [Column("ai_score", TypeName = "decimal(5,2)")]
    public decimal? AiScore { get; set; }

    [Column("lecturer_score", TypeName = "decimal(5,2)")]
    public decimal? LecturerScore { get; set; }

    [Column("ai_logs")]
    public string? AiLogs { get; set; }

    [Required]
    [Column("status")]
    public SubmissionStatus Status { get; set; }

    [MaxLength(1000)]
    [Column("comment")]
    public string Comment { get; set; } = string.Empty;

    [Required]
    [Column("created_date")]
    public DateTime CreatedDate { get; set; }

    [Required]
    [Column("updated_date")]
    public DateTime UpdatedDate { get; set; }

    // Grading diary
    [Required]
    [Column("diary_id")]
    [ForeignKey(nameof(GradingDiary))]
    public Guid DiaryId { get; set; }

    public GradingDiary GradingDiary { get; set; } = null!;

    // Student examination
    [Required]
    [Column("student_examination_id")]
    [ForeignKey(nameof(StudentExamination))]
    public Guid StudentExaminationId { get; set; }

    public StudentExamination StudentExamination { get; set; } = null!;
}
