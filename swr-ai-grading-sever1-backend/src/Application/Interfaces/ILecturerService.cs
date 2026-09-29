using Application.Common;
using Application.DTOs.Lecturers;

namespace Application.Interfaces;

/// <summary>
/// Service quản lý tài khoản Giảng viên (Dành cho Admin).
/// </summary>
public interface ILecturerService
{
    /// <summary>
    /// Danh sách tất cả giảng viên kèm số đề thi và sổ chấm phụ trách.
    /// </summary>
    Task<Result<IReadOnlyList<LecturerSummaryDto>>> GetLecturersAsync(CancellationToken ct = default);

    /// <summary>
    /// Cập nhật thông tin giảng viên (Họ tên, email, bộ môn).
    /// </summary>
    Task<Result<LecturerSummaryDto>> UpdateLecturerAsync(Guid id, UpdateLecturerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Bật/Tắt trạng thái hoạt động (Active/Inactive) của giảng viên.
    /// </summary>
    Task<Result<bool>> ToggleStatusAsync(Guid id, CancellationToken ct = default);
}
