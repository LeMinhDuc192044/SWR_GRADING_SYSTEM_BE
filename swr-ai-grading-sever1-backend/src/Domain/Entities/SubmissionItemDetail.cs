// SubmissionItemDetail.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("submission_item_details")]
public class SubmissionItemDetail
{
    [Key]
    [Column("detail_id")]
    public Guid DetailId { get; set; } = Guid.NewGuid();

    [Column("score", TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Required]
    [Column("evaluation_id")]
    [ForeignKey(nameof(SubmissionGradingHistory))]
    public Guid EvaluationId { get; set; }
    public SubmissionGradingHistory SubmissionGradingHistory { get; set; } = null!;

    [Required]
    [Column("parsed_answer_id")]
    [ForeignKey(nameof(ParsedAnswer))]
    public Guid ParsedAnswerId { get; set; }
    public SubmissionParsedAnswer ParsedAnswer { get; set; } = null!;
}