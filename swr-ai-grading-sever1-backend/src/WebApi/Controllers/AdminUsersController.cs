using Application.Common;
using Application.DTOs.Users;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

/// <summary>
/// Quản lý danh mục tài khoản cán bộ và người dùng các vai trò (Examiner, Monitor, Manager, Admin).
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin/users")]
[Route("api/admin/staff")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly IUserService _userService;

    public AdminUsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// GET /api/admin/users (hoặc /api/admin/staff)
    /// Lấy danh sách cán bộ theo các vai trò (Examiner, Monitor, Manager, Admin) có phân trang và tìm kiếm.
    /// Dành cho role: Admin, Manager.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Manager")]
    public async Task<IActionResult> GetStaffUsers([FromQuery] StaffFilterRequest request, CancellationToken ct)
    {
        var result = await _userService.GetPagedStaffAsync(request, ct);
        return result.ToActionResult(this);
    }
}
