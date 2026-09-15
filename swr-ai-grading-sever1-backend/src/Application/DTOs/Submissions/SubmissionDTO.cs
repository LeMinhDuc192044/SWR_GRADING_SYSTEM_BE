using Domain.Enums;

namespace Application.DTOs.Submissions;

/// <summary>
/// DTO chi tiết của một Submission (trả về cho client).
/// Mapping 1:1 với entity Submission, thêm 2 field tiện ích:
///   - LecturerName: hiển thị cho UI không phải lookup thêm
///   - GradingCount:  số Grading hiện có của submission
/// </summary>
public class SubmissionDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public Guid LecturerId { get; set; }
    public string? LecturerName { get; set; }
    public int GradingCount { get; set; }
}

/// <summary>
/// DTO tóm tắt cho danh sách (không include Gradings).
/// </summary>
public class SubmissionSummaryDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public SubmissionStatus Status { get; set; }
    public DateTime CreatedDate { get; set; }
    public Guid LecturerId { get; set; }
    public int GradingCount { get; set; }
}

/// <summary>
/// Input từ WebApi layer — Controller sẽ map IFormFile → SubmissionFileUpload.
/// Tách ở Application layer để giữ Clean Architecture (Application không phụ thuộc AspNetCore).
/// </summary>
public sealed class SubmissionFileUpload
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public long Length { get; init; }
}

/// <summary>
/// Request body cho POST /api/submissions (upload file).
/// LecturerId lấy từ JWT claim hiện không có → client gửi kèm.
/// File ở dạng SubmissionFileUpload (đã map từ IFormFile ở Controller).
/// </summary>
public class CreateSubmissionRequest
{
    public string SubmissionName { get; set; } = string.Empty;
    public string Folder { get; set; } = string.Empty;
    public Guid LecturerId { get; set; }
    public SubmissionFileUpload? File { get; set; }
}

/// <summary>
/// Request body cho PUT /api/submissions/{id} (cập nhật metadata).
/// Status: nếu null giữ nguyên; LecturerId: chỉ Admin được đổi (sẽ check ở Service).
/// </summary>
public class UpdateSubmissionRequest
{
    public string? SubmissionName { get; set; }
    public string? Folder { get; set; }
    public SubmissionStatus? Status { get; set; }
    public Guid? LecturerId { get; set; }
}

/// <summary>
/// DTO trả về khi download file của submission.
/// </summary>
public sealed class SubmissionFileDownload
{
    public required Stream Content { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "download";
}
