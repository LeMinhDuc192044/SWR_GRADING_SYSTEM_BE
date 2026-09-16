using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("grading_diary")]
public class GradingDiary
{
    [Key]
    [Column("grading_diary_id")]
    public Guid GradingDiaryId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(21)]
    [Column("examination_code")]
    public string Content { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("create_by_id")]
    [ForeignKey(nameof(CreatedBy))]
    public Guid CreateById { get; set; }

    public Lecturer CreatedBy { get; set; } = null!;

    public ICollection<Submission> Submissions { get; set; }
        = new List<Submission>();
}