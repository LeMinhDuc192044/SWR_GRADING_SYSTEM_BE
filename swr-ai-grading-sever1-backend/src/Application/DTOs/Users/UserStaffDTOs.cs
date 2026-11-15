using Application.Common;
using Domain.Enums;

namespace Application.DTOs.Users;

/// <summary>
/// DTO chứa đầy đủ thông tin tài khoản người dùng của Cán bộ / Nhân viên (không chứa PasswordHash).
/// </summary>
public sealed class UserStaffResponseDto
{
    public Guid Id { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly Birthday { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
}

/// <summary>
/// Yêu cầu lấy danh sách cán bộ theo vai trò (Examiner, Monitor, Manager, Admin) và tìm kiếm.
/// Kế thừa PagedRequest chuẩn (Page, PageSize).
/// </summary>
public sealed class StaffFilterRequest : PagedRequest
{
    public UserRole? Role { get; set; }
    public string? Search { get; set; }
}
