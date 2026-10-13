// SubmissionEvaluation.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("submission_evaluations")]
public class SubmissionEvaluation
{
    [Key]
    [Column("evaluation_id")]
    public Guid EvaluationId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(Submission))]
    public Guid SubmissionId { get; set; }
    public StudentSubmission Submission { get; set; } = null!;

    [Required]
    [Column("evaluation_type")]
    public EvaluationType EvaluationType { get; set; }   // e.g. AI, Lecturer

    [Required]
    [Column("evaluation_round")]
    public int EvaluationRound { get; set; }

    [Column("total_score", TypeName = "decimal(5,2)")]
    public decimal? TotalScore { get; set; }

    [Column("general_feedback")]
    public string? GeneralFeedback { get; set; }

    [Column("is_final_score")]
    public bool IsFinalScore { get; set; }

    [Column("admin_selected_id")]
    [ForeignKey(nameof(AdminSelected))]
    public Guid? AdminSelectedId { get; set; }
    public User? AdminSelected { get; set; }

    [Column("selection_reason")]
    public string? SelectionReason { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public ICollection<EvaluationItemDetail> Details { get; set; } = [];
}