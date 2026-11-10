using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;


namespace Domain.Entities;

[Table("applications")]

public class Application 
{
    [Key]
    [Column("application_id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("reason")]
    public string reason { get; set; } = string.Empty;

    [Required]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.PENDING;

    [Required]
    [Column("created_day")]
    public DateTime CreatedDay { get; set; }

    [Required]
    [Column("updated_day")]
    public DateTime UpdatedDay { get; set; }

    [Required]
    [Column("student_id")]
    public Guid StudentId { get; set; }

    // Navigation property
    [ForeignKey(nameof(StudentId))]
    public Student Student { get; set; } = null!;

    [Required]
    [Column("submission_id")]
    public Guid SubmissionId { get; set; }

    [ForeignKey(nameof(SubmissionId))]
    public StudentSubmission StudentSubmission { get; set; } = null!;
}