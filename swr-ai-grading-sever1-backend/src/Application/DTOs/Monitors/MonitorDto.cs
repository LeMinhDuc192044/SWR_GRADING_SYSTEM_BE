using Application.Common;
using Domain.Enums;

namespace Application.DTOs.Monitors;

/// <summary>
/// DTO chứa đầy đủ thông tin tài khoản cán bộ chấm thi (Monitor).
/// </summary>
public sealed class MonitorResponseDto
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
    public bool IsBusy { get; set; }
    public string WorkingStatus { get; set; } = "AVAILABLE";
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int AssignedCount { get; set; }
}

/// <summary>
/// Yêu cầu lấy danh sách cán bộ chấm thi (Monitor) dành cho Manager / Admin có phân trang và bộ lọc tìm kiếm.
/// Kế thừa PagedRequest chuẩn (Page, PageSize).
/// </summary>
public sealed class MonitorFilterRequest : PagedRequest
{
    public string? Search { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsBusy { get; set; }
}
