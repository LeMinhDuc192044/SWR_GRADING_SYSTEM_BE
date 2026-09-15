using Domain.Enums;

namespace Application.DTOs.Submissions;

/// <summary>
/// DTO trả về chi tiết 1 Grading.
/// </summary>
public class GradingDTO
{
    public Guid GradingId { get; set; }
    public string GradingCode { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public string? AiLogs { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime UpdateDate { get; set; }
    public string? Comment { get; set; }
    public decimal? FinalScore { get; set; }
    public GradingStatus Status { get; set; }
    public Guid SubmissionId { get; set; }
}

/// <summary>
/// Request body cho POST /api/submissions/{id}/gradings.
/// </summary>
public class CreateGradingRequest
{
    /// <summary>Mã do client cung cấp (vd: "G_{submissionId}_{seq}"). Nếu null → server sinh.</summary>
    public string? GradingCode { get; set; }

    /// <summary>Điểm AI (nếu đã chấm xong trước khi tạo bản ghi). Thường null khi mới tạo.</summary>
    public decimal? AiScore { get; set; }

    public string? AiLogs { get; set; }
    public string? Comment { get; set; }
}
