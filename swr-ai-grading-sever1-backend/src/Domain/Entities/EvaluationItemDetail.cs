// EvaluationItemDetail.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("evaluation_item_details")]
public class EvaluationItemDetail
{
    [Key]
    [Column("detail_id")]
    public Guid DetailId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("evaluation_id")]
    [ForeignKey(nameof(Evaluation))]
    public Guid EvaluationId { get; set; }
    public SubmissionEvaluation Evaluation { get; set; } = null!;

    [Required]
    [Column("parsed_answer_id")]
    [ForeignKey(nameof(ParsedAnswer))]
    public Guid ParsedAnswerId { get; set; }
    public SubmissionParsedAnswer ParsedAnswer { get; set; } = null!;

    [Column("score", TypeName = "decimal(5,2)")]
    public decimal Score { get; set; }

    [Column("comment")]
    public string? Comment { get; set; }

    [Column("ai_extraction_id")]
    public Guid? AiExtractionId { get; set; }
}