using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("students")]
public class Student : User
{
    [Required]
    [MaxLength(20)]
    [Column("student_code")]
    public string StudentCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("major")]
    public string Major { get; set; } = string.Empty;
    public ICollection<StudentSemester> StudentSemesters { get; set; } = [];
    public ICollection<Application> Applications { get; set; } = [];
    public ICollection<StudentExaminationSession> StudentExaminationSessions { get; set; } = [];
}
