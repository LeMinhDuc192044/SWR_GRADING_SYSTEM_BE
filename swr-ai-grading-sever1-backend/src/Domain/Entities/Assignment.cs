using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("assignments")]
public class Assignment
{
    [Key]
    [Column("assignment_id")]
    public Guid AssignmentId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("assigned_date")]
    public DateTime AssignedDate { get; set; }

    [Required]
    [Column("updated_date")]
    public DateTime UpdatedDate { get; set; }

    [Required]
    [Column("staff_id")]
    [ForeignKey(nameof(Staff))]
    public Guid StaffId { get; set; }

    public Staff Staff { get; set; } = null!;

    [Required]
    [Column("assignment_status")]
    public AssignmentStatus Status { get; set; }

    [Required]
    [Column("grading_session_id")]
    [ForeignKey(nameof(GradingSession))]
    public Guid GradingSessionId { get; set; }

    public GradingSession GradingSession { get; set; } = null!;

    [Required]
    [Column("submission_id")]
    [ForeignKey(nameof(StudentSubmission))]
    public Guid SubmissionId { get; set; }

    public StudentSubmission Submission { get; set; } = null!;

}
