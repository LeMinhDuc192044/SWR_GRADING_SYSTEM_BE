using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("student_examination")]
public class StudentExamination
{
    [Key]
    [Column("student_examination_id")]
    public Guid StudentExaminationId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("student_id")]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Required]
    [Column("exam_id")]
    [ForeignKey(nameof(Examination))]
    public Guid ExamId { get; set; }

    public Examination Examination { get; set; } = null!;
}