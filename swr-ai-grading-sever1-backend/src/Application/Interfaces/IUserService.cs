using Application.Common;
using Application.DTOs.Users;

namespace Application.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Lấy danh sách tài khoản cán bộ (Examiner, Monitor, Manager, Admin) có phân trang và bộ lọc theo vai trò.
    /// </summary>
    Task<Result<PagedResult<UserStaffResponseDto>>> GetPagedStaffAsync(StaffFilterRequest request, CancellationToken ct = default);
}
