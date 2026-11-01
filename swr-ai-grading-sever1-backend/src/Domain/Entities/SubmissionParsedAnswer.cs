// SubmissionParsedAnswer.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("submission_parsed_answers")]
public class SubmissionParsedAnswer
{
    [Key]
    [Column("parsed_answer_id")]
    public Guid ParsedAnswerId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(Submission))]
    public Guid SubmissionId { get; set; }
    public StudentSubmission Submission { get; set; } = null!;

    [Required]
    [Column("question_id")]
    [ForeignKey(nameof(Question))]
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;

    [Column("element_id")]
    [ForeignKey(nameof(Element))]
    public Guid? ElementId { get; set; }
    public DiagramElement? Element { get; set; }

    [Column("extracted_text")]
    public string? ExtractedText { get; set; }

    [Column("extracted_xml")]
    public string? Extracted_Xml { get; set; }

    [Column("extracted_image_url")]
    public string? ExtractedImageUrl { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    public ICollection<EvaluationItemDetail> EvaluationDetails { get; set; } = [];

}