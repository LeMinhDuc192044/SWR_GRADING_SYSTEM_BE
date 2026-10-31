using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("regrade_applications")]
public class RegradeApplication
{
    [Key]
    [Column("application_id")]
    public Guid ApplicationId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(Submission))]
    public Guid SubmissionId { get; set; }
    public StudentSubmission Submission { get; set; } = null!;

    [Required]
    [Column("student_id")]
    [ForeignKey(nameof(Student))]
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    [Required]
    [MaxLength(1000)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [Column("status")]
    public RegradeApplicationStatus Status { get; set; } = RegradeApplicationStatus.Pending;

    [Column("assigned_lecturer_id")]
    [ForeignKey(nameof(AssignedLecturer))]
    public Guid? AssignedLecturerId { get; set; }
    public Lecturer? AssignedLecturer { get; set; }

    [Column("admin_id")]
    [ForeignKey(nameof(Admin))]
    public Guid? AdminId { get; set; }
    public User? Admin { get; set; }

    [MaxLength(1000)]
    [Column("admin_feedback")]
    public string? AdminFeedback { get; set; }

    [Column("original_score", TypeName = "decimal(5,2)")]
    public decimal? OriginalScore { get; set; }

    [Column("regraded_score", TypeName = "decimal(5,2)")]
    public decimal? RegradedScore { get; set; }

    [MaxLength(1000)]
    [Column("lecturer_feedback")]
    public string? LecturerFeedback { get; set; }

    [Required]
    [Column("created_date")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_date")]
    public DateTime UpdatedDate { get; set; } = DateTime.UtcNow;
}

