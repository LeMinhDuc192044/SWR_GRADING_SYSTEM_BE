using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("student_semester")]
public class StudentSemester
{
    [Required]
    [Column("student_id")]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }

    public Student Student { get; set; } = null!;

    [Required]
    [Column("semester_id")]
    [ForeignKey(nameof(Semester))]
    public Guid SemesterId { get; set; }

    [Required]
    [Column("lecturer_name")]
    public string LecturerName { get; set; } = string.Empty;

    [Required]
    [Column("lecturer_code")]
    public string LecturerCode { get; set; } = string.Empty;

    [Required]
    [Column("class_code")]
    public string ClassCode { get; set; } = string.Empty;

    public Semester Semester { get; set; } = null!;
}
