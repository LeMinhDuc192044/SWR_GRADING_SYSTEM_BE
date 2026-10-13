using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("diagrams")]
public class Diagram
{
    [Key]
    [Column("diagram_id")]
    public Guid DiagramId { get; set; } = Guid.NewGuid();

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("file_template_url")]
    public string? FileTemplateUrl { get; set; }

    public ICollection<DiagramElement> Elements { get; set; } = [];
    public ICollection<QuestionRubric> Rubrics { get; set; } = [];
}