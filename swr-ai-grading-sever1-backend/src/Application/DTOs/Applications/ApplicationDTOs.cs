using Domain.Enums;

namespace Application.DTOs.Applications;

public sealed class CreateApplicationRequest
{
    public Guid SubmissionId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class UpdateApplicationStatusRequest
{
    public ApplicationStatus Status { get; set; }
}

public sealed class ApplicationDTO
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
    public string? ExaminationName { get; set; }
    public string? ExaminationSessionCode { get; set; }
    public decimal? OfficialScore { get; set; }
}
