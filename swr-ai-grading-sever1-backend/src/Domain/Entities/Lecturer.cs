using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("lecturers")]
public class Lecturer : User
{
    [Required]
    [MaxLength(20)]
    [Column("lecturer_code")]
    public string LecturerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("subject")]
    public string Subject { get; set; } = string.Empty;

    public ICollection<ExamMaterial> ExamMaterialsCreated { get; set; } = new List<ExamMaterial>();
    public ICollection<GradingDiary> GradingDiariesCreated { get; set; } = new List<GradingDiary>();
}