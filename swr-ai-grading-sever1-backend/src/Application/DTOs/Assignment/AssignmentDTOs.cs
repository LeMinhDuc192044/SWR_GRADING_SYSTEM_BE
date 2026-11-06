using Domain.Enums;

namespace Application.DTOs.Assignments;

public sealed class AssignmentDTO
{
    public Guid AssignmentId { get; set; }
    public Guid GradingSessionId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid StaffId { get; set; }
    public AssignmentStatus Status { get; set; }
    public DateTime AssignedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class CreateAssignmentRequest
{
    public Guid StaffId { get; set; }
    public int NumberSubmissions { get; set; }
}

public sealed class UpdateAssignmentRequest
{
    public Guid? StaffId { get; set; }
    public AssignmentStatus? Status { get; set; }
}
