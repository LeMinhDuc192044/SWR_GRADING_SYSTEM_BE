using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("diagram_elements")]
public class DiagramElement
{
    [Key]
    [Column("element_id")]
    public Guid ElementId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("diagram_id")]
    [ForeignKey(nameof(Diagram))]
    public Guid DiagramId { get; set; }
    public Diagram Diagram { get; set; } = null!;

    [Required, MaxLength(50)]
    [Column("element_code")]
    public string ElementCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    [Column("shape_type")]
    public string? ShapeType { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("is_requirement")]
    public bool IsRequirement { get; set; }

    [Column("file_template_url")]
    public string? FileTemplateUrl { get; set; }

    // Self-reference (the loop on the left of Diagrams_elements)
    [Column("element_reference_id")]
    [ForeignKey(nameof(ReferenceElement))]
    public Guid? ElementReferenceId { get; set; }
    public DiagramElement? ReferenceElement { get; set; }
    public ICollection<DiagramElement> ReferencedBy { get; set; } = [];

    [MaxLength(100)]
    [Column("element_reference_type")]
    public string? ElementReferenceType { get; set; }

    [MaxLength(100)]
    [Column("software_type")]
    public string? SoftwareType { get; set; }

    public SubmissionParsedAnswer? ParsedAnswer { get; set; }
}