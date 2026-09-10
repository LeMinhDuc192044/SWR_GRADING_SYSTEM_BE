//using Application.Common;
//using Application.DTOs.Users;

//namespace Application.Interfaces;

///// <summary>
///// Service quản lý người dùng (sinh viên, giảng viên).
///// </summary>
//public interface IUserService
//{
//    Task<PagedResult<UserDto>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
//    Task<Result<UserDto>> GetByIdAsync(Guid id, CancellationToken ct = default);
//    Task<Result<UserDto>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
//    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default); // soft delete
//}
