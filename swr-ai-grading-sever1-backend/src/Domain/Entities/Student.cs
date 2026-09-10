using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("students")]
public class Student : User
{
    [Required]
    [MaxLength(20)]
    [Column("student_code")]
    public string StundentCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("major")]
    public string Major { get; set; } = string.Empty;
}