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

    [NotMapped]
    [Obsolete("Use StudentCode instead")]
    public string StundentCode { get => StudentCode; set => StudentCode = value; }

    [Required]
    [MaxLength(100)]
    [Column("major")]
    public string Major { get; set; } = string.Empty;
    public ICollection<StudentExamination> StudentExaminations { get; set; }
        = new List<StudentExamination>();
}