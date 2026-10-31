using System.ComponentModel.DataAnnotations;
using Domain.Enums;

namespace Application.DTOs.Regrades;

public record CreateRegradeApplicationRequest(
    [property: Required] Guid SubmissionId,
    [property: Required, MaxLength(1000)] string Reason
);

public record ReviewRegradeApplicationRequest(
    [property: Required] bool IsApproved,
    Guid? AssignedLecturerId,
    string? AdminFeedback
);

public record GradeRegradeApplicationRequest(
    [property: Required] decimal RegradedScore,
    string? LecturerFeedback
);

public sealed class RegradeApplicationDto
{
    public Guid ApplicationId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string StudentEmail { get; set; } = string.Empty;

    public string ExaminationCode { get; set; } = string.Empty;
    public string DiaryName { get; set; } = string.Empty;
    public string FileSubmissions { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
    public RegradeApplicationStatus Status { get; set; }
    public string StatusText => Status switch
    {
        RegradeApplicationStatus.Pending => "Chờ duyệt",
        RegradeApplicationStatus.Approved => "Đã duyệt (Đang chấm lại)",
        RegradeApplicationStatus.Rejected => "Từ chối",
        RegradeApplicationStatus.Completed => "Đã chấm lại",
        _ => Status.ToString()
    };

    public Guid? AssignedLecturerId { get; set; }
    public string? AssignedLecturerName { get; set; }
    public string? AssignedLecturerCode { get; set; }

    public Guid? AdminId { get; set; }
    public string? AdminName { get; set; }
    public string? AdminFeedback { get; set; }

    public decimal? OriginalScore { get; set; }
    public decimal? RegradedScore { get; set; }
    public string? LecturerFeedback { get; set; }

    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class GetRegradeApplicationsRequest
{
    public RegradeApplicationStatus? Status { get; set; }
    public Guid? SubmissionId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

