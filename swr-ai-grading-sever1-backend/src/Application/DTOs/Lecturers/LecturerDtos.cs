namespace Application.DTOs.Lecturers;

/// <summary>
/// DTO tóm tắt thông tin Giảng viên trong hệ thống dành cho Admin.
/// </summary>
public sealed class LecturerSummaryDto
{
    public Guid Id { get; set; }
    public string LecturerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Thống kê liên quan
    public int PaperSetsCount { get; set; }
    public int GradingDiariesCount { get; set; }
}

/// <summary>
/// Request cập nhật thông tin giảng viên (Admin)
/// </summary>
public sealed class UpdateLecturerRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
}
