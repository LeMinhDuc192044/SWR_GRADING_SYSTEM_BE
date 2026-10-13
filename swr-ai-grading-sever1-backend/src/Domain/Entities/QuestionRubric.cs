using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("question_rubrics")]
public class QuestionRubric
{
    [Key]
    [Column("rubric_id")]
    public Guid RubricId { get; set; } = Guid.NewGuid();

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

    public ICollection<Question> Questions { get; set; } = [];
}