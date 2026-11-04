using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("question_criterias")]
public class QuestionCriteria
{
    [Key]
    [Column("rubric_id")]
    public Guid RubricId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("criteria")]
    public string Criteria { get; set; } = string.Empty;

    [Required]
    [Column("point", TypeName = "decimal(5,2)")]
    public decimal Point { get; set; }

    [Required]
    [Column("question_id")]
    [ForeignKey(nameof(Question))]
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
}