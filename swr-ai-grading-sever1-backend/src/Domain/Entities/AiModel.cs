using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("ai_models")]
public class AiModel
{
    [Key]
    [Column("model_id")]
    public Guid ModelId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    // Uniqueness is enforced by the filtered unique index in AppDbContext, so rows
    // without a key (not configured yet) are allowed.
    [Required]
    [MaxLength(500)]
    [Column("api_key")]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    public bool Status { get; set; }

    [Column("price", TypeName = "decimal(18,4)")]
    public decimal Price { get; set; }

    [Column("is_selected")]
    public bool IsSelected { set; get; }
}
