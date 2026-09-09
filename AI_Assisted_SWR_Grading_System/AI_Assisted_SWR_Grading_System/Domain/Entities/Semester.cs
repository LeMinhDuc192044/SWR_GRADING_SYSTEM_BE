using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Domain.Entities;

[Table("semesters")]
public class Semester
{
    [Key]
    [Column("semester_id")]
    public Guid SemesterId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(20)]
    [Column("semester_code")]
    public string SemesterCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateTime StartDate { get; set; }

    [Required]
    [Column("end_date", TypeName = "date")]
    public DateTime EndDate { get; set; }

    [Required]
    [Column("status")]
    public SemesterStatus Status { get; set; }

    public ICollection<Examination> Examinations { get; set; } = new List<Examination>();
}