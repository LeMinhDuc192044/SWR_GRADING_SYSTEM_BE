using Application.Common;

namespace Application.DTOs.Students;

/// <summary>
/// DTO chứa đầy đủ thông tin tài khoản người dùng của Sinh viên (bảo mật: không chứa PasswordHash).
/// </summary>
public sealed class StudentResponseDto
{
    public Guid Id { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly Birthday { get; set; }
    public string Major { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
}

/// <summary>
/// Yêu cầu lấy danh sách sinh viên kèm bộ lọc theo học kỳ, kỳ thi, ca thi và tìm kiếm.
/// Kế thừa PagedRequest chuẩn (Page, PageSize).
/// </summary>
public sealed class StudentFilterRequest : PagedRequest
{
    public Guid? SemesterId { get; set; }
    public Guid? ExaminationId { get; set; }
    public Guid? ExaminationSessionId { get; set; }
    public string? Search { get; set; }
}
