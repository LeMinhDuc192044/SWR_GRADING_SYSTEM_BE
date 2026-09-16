using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Sổ chấm bài — mỗi Lecturer tạo 1 sổ để nhóm các Submission chấm trong 1 kỳ/1 môn.
/// Submission FK tới GradingDiary (không FK trực tiếp tới Lecturer).
/// Lecturer lấy gián tiếp qua <see cref="CreateById"/>.
/// </summary>
[Table("grading_diary")]
public class GradingDiary
{
    [Key]
    [Column("grading_diary_id")]
    public Guid GradingDiaryId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(50)]
    [Column("examination_code")]
    public string ExaminationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("create_by_id")]
    [ForeignKey(nameof(CreateBy))]
    public Guid CreateById { get; set; }

    public Lecturer CreateBy { get; set; } = null!;

    public ICollection<Submission> Submissions { get; set; } = new List<Submission>();
}
