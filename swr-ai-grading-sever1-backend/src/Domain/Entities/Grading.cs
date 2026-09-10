using Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("gradings")]
public class Grading
{
    [Key]
    [Column("grading_id")]
    public Guid GradingId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    [Column("grading_code")]
    public string GradingCode { get; set; } = string.Empty;

    [Column("ai_score", TypeName = "decimal(5,2)")]
    public decimal? AiScore { get; set; }

    [Column("lecturer_score", TypeName = "decimal(5,2)")]
    public decimal? LecturerScore { get; set; }

    [Column("ai_logs")]
    public string? AiLogs { get; set; }

    [Required]
    [Column("create_date")]
    public DateTime CreateDate { get; set; }

    [Required]
    [Column("update_date")]
    public DateTime UpdateDate { get; set; }

    [MaxLength(1000)]
    [Column("comment")]
    public string? Comment { get; set; }

    [Column("final_score", TypeName = "decimal(5,2)")]
    public decimal? FinalScore { get; set; }

    [Required]
    [Column("status")]
    public GradingStatus Status { get; set; }

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(Submission))]
    public Guid SubmissionId { get; set; }

    public Submission Submission { get; set; } = null!;
}