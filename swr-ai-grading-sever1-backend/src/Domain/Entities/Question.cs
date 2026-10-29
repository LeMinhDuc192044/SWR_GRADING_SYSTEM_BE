// Question.cs  (replace whole file)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("questions")]
public class Question
{
    [Key]
    [Column("question_id")]
    public Guid QuestionId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(50)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [Column("point", TypeName = "decimal(5,2)")]
    public decimal Point { get; set; }

    [Required]
    [Column("paper_set_id")]
    [ForeignKey(nameof(PaperSet))]
    public Guid PaperSetId { get; set; }
    public PaperSet PaperSet { get; set; } = null!;

    [Column("diagram_id")]
    [ForeignKey(nameof(Diagram))]
    public Guid? DiagramId { get; set; }
    public Diagram? Diagram { get; set; }

    // The Rubric table isn't visible in your screenshots; add the FK + navigation once it exists
    [Column("rubric_id")]
    [ForeignKey(nameof(Rubric))]
    public Guid? RubricId { get; set; }
    public QuestionRubric? Rubric { get; set; }

    [Required]
    [Column("created_day")]
    public DateTime CreatedDay { get; set; }

    [Required]
    [Column("updated_day")]
    public DateTime UpdatedDay { get; set; }

    public ICollection<SubmissionParsedAnswer> ParsedAnswers { get; set; } = [];
}