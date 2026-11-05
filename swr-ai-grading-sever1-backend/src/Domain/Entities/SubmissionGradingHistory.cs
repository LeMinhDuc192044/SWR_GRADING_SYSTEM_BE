// SubmissionEvaluation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("submission_grading_histories")]
public class SubmissionGradingHistory
{
    [Key]
    [Column("history_id")]
    public Guid HistoryId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("evaluation_type")]
    public EvaluationType EvaluationType { get; set; }   // e.g. AI, Lecturer

    [Required]
    [Column("grading_round")]
    public int GradingRound { get; set; }

    [Column("total_score", TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    [Column("general_feedback")]
    public string? GeneralFeedback { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(Submission))]
    public Guid SubmissionId { get; set; }
    public StudentSubmission Submission { get; set; } = null!;
    public ICollection<SubmissionItemDetail> Details { get; set; } = [];
}