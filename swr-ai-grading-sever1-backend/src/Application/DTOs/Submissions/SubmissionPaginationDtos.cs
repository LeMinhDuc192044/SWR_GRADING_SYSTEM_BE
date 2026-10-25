using System.ComponentModel.DataAnnotations;
using Application.Common;
using Domain.Enums;

namespace Application.DTOs.Submissions;

public class GetSubmissionsRequest : PagedRequest
{
    public string? Search { get; set; }
    public SubmissionStatus? Status { get; set; }
    public Guid? SemesterId { get; set; }
    public Guid? ExaminationId { get; set; }
    public Guid? DiaryId { get; set; }
}

public class AdminSubmissionItemDto
{
    public Guid SubmissionId { get; set; }
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string FileSubmissions { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status switch
    {
        SubmissionStatus.Submitted => "Đã nộp (chưa chấm)",
        SubmissionStatus.AI_Graded => "AI đã chấm",
        SubmissionStatus.Lecturer_Reviewed => "GV đã duyệt",
        SubmissionStatus.Final => "Hoàn tất",
        SubmissionStatus.ReGrading => "Đang phúc khảo",
        SubmissionStatus.ReGraded => "Đã phúc khảo",
        _ => Status.ToString()
    };
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid DiaryId { get; set; }
    public string DiaryName { get; set; } = string.Empty;
    public string ExaminationCode { get; set; } = string.Empty;
    public string PaperSetCode { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public decimal? FinalScore { get; set; }
}

public class UpdateSubmissionStatusRequest
{
    [Required]
    public SubmissionStatus Status { get; set; }
    public string? Reason { get; set; }
}
