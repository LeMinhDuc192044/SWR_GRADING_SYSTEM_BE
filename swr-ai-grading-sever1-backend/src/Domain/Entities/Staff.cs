using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Domain.Entities;

[Table("staff")]
[Index(nameof(StaffCode), IsUnique = true)]
public class Staff : User
{
    [Required]
    [Column("role")]
    public UserRole Role { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("staff_code")]
    public string StaffCode { get; set; } = string.Empty;

    public ICollection<Assignment> Assignments { get; set; } = [];
    public ICollection<StudentSubmission> UploadedSubmissions { get; set; } = [];
    public ICollection<PaperSet> UploadedPaperSets { get; set; } = [];
}
