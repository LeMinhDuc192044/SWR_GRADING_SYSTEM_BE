using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Users;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class UserService : IUserService
{
    private readonly IApplicationDbContext _db;

    public UserService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<PagedResult<UserStaffResponseDto>>> GetPagedStaffAsync(
        StaffFilterRequest request, CancellationToken ct = default)
    {
        request ??= new StaffFilterRequest();

        var query = _db.Staff.AsNoTracking()
            .Where(s => !s.IsDeleted);

        if (request.Role.HasValue)
        {
            query = query.Where(s => s.Role == request.Role.Value);
        }
        else
        {
            // Mặc định lấy các vai trò cán bộ trong hệ thống
            query = query.Where(s => s.Role == UserRole.Examiner ||
                                     s.Role == UserRole.Monitor ||
                                     s.Role == UserRole.Manager ||
                                     s.Role == UserRole.Admin);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.FullName.ToLower().Contains(term) ||
                s.StaffCode.ToLower().Contains(term) ||
                s.Email.ToLower().Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.StaffCode)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new UserStaffResponseDto
            {
                Id = s.Id,
                StaffCode = s.StaffCode,
                FullName = s.FullName,
                Email = s.Email,
                Birthday = s.Birthday,
                Role = s.Role,
                IsActive = s.IsActive,
                CreatedDay = s.CreatedDay,
                UpdatedDay = s.UpdatedDay
            })
            .ToListAsync(ct);

        var pagedResult = new PagedResult<UserStaffResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<UserStaffResponseDto>>.Success(pagedResult);
    }
}
