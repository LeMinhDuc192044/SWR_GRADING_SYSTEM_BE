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
    [Column("exam_material_id")]
    [ForeignKey(nameof(ExamMaterial))]
    public Guid ExamMaterialId { get; set; }

    public ExamMaterial ExamMaterial { get; set; } = null!;
}