using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("questions")]
public class Question
{

    [Key]
    [Column("question_id")]
    public Guid QuestionId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    [Column("Title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column("content")]
    public string content { get; set; } = string.Empty;

    [Required]
    [Column("point")]
    public decimal point { get; set; }

    [Required]
    [Column("paper_set_id")]
    [ForeignKey(nameof(PaperSet))]
    public Guid PaperSetId { get; set; }

    public PaperSet PaperSet { get; set; } = null!;
}