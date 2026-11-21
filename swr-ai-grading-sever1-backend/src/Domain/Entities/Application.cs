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

    // Concurrency token: a status change saved by another request makes this save fail
    // instead of silently overwriting it (e.g. a resubmission racing an approval).
    [Required]
    [ConcurrencyCheck]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.PENDING;

    // Set once when the appeal is approved. The regrade cycle starts here, so later
    // status changes (IN_PROGRESS, COMPLETED) do not move the start of that cycle.
    [Column("approved_at")]
    public DateTime? ApprovedAt { get; set; }

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