using AI_Assisted_SWR_Grading_System.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

[Table("examinations")]
public class Examination
{
    [Key]
    [Column("examination_id")]
    public Guid ExaminationId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    [Column("examination_code")]
    public string ExaminationCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("start_time")]
    public TimeSpan StartTime { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Duration must be greater than 0.")]
    [Column("duration_minutes")]
    public int DurationMinutes { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Before-time minutes cannot be negative.")]
    [Column("before_time_minutes")]
    public int BeforeTimeMinutes { get; set; } = 0;

    [MaxLength(1000)]
    [Column("note")]
    public string? Note { get; set; }

    [Required]
    [Column("status")]
    public ExaminationStatus Status { get; set; }

    [Required]
    [Column("semester_id")]
    [ForeignKey(nameof(Semester))]
    public Guid SemesterId { get; set; }

    public Semester Semester { get; set; } = null!;

    public ICollection<ExamMaterial> ExamMaterials { get; set; } = new List<ExamMaterial>();
}