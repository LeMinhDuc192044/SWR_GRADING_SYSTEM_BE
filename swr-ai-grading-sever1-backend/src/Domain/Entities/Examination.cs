using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Formats.Tar;
using Domain.Enums;


namespace Domain.Entities;

[Table("examinations")]
public class Examination
{
    [Key]
    [Column("examination_id")]
    public Guid ExaminationId { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("examination_type")]
    public ExaminationType ExaminationType { get; set; }

    [Required]
    [Column("start_date", TypeName = "date")]
    public DateOnly StartDate { get; set; }


    [Required]
    [Column("end_date", TypeName = "date")]
    public DateOnly EndDate { get; set; }

    [Required]
    [Column("created_day")]
    public DateTime CreatedDay { get; set; }

    [Required]
    [Column("updated_day")]
    public DateTime UpdatedDay { get; set; }

    [Required]
    [Column("status")]
    public ExaminationStatus Status { get; set; }

    [Required]
    [Column("semester_id")]
    [ForeignKey(nameof(Semester))]
    public Guid SemesterId { get; set; }

    public Semester Semester { get; set; } = null!;

    public ICollection<PaperSet> PaperSets { get; set; } = [];

    public ICollection<ExaminationSession> StudentExaminations { get; set; } = [];

}