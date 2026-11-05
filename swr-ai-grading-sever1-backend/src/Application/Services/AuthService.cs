using Application.Common;
using Application.DTOs.AuthDTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Options;

namespace Application.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _tokens;
    private readonly JwtSettings _settings;

    public AuthService(
        IUserRepository users,
        IUnitOfWork unitOfWork,
        IJwtTokenService tokens,
        IOptions<JwtSettings> options)
    {
        _users = users;
        _unitOfWork = unitOfWork;
        _tokens = tokens;
        _settings = options.Value;
    }

    public async Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
            return Result<AuthResponseDto>.Failure("Invalid credentials.", "UNAUTHORIZED");

        var student = await _users.GetStudentByEmailAsync(email, ct);
        var staff = student is null ? await _users.GetStaffByEmailAsync(email, ct) : null;
        var user = (User?)student ?? staff;

        if (user is null || user.IsDeleted || !user.IsActive ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Result<AuthResponseDto>.Failure("Invalid credentials.", "UNAUTHORIZED");
        }

        var role = student is not null ? UserRole.Student : staff!.Role;
        return Result<AuthResponseDto>.Success(CreateResponse(user, role, student));
    }

    public async Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var studentCode = request.StudentCode?.Trim();
        var major = request.Major?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(request.Password) || request.Birthday == default)
        {
            return Result<AuthResponseDto>.Failure(
                "Registration requires full name, email, password, and birthday.",
                "BAD_REQUEST");
        }

        if (!Enum.IsDefined(typeof(UserRole), request.Role))
            return Result<AuthResponseDto>.Failure("Invalid user role.", "BAD_REQUEST");

        if (request.Password.Length < 8)
            return Result<AuthResponseDto>.Failure("Password must be at least 8 characters.", "BAD_REQUEST");

        if (await _users.IsEmailExistsAsync(email, ct))
            return Result<AuthResponseDto>.Failure("An account with this email already exists.", "EMAIL_ALREADY_EXISTS");

        var role = (UserRole)request.Role;
        User user;
        Student? registeredStudent = null;

        if (role == UserRole.Student)
        {
            if (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(major))
                return Result<AuthResponseDto>.Failure("StudentCode and Major are required for Student registration.", "BAD_REQUEST");

            if (await _users.IsStudentCodeExistsAsync(studentCode, ct))
                return Result<AuthResponseDto>.Failure("An account with this student code already exists.", "STUDENT_CODE_ALREADY_EXISTS");

            registeredStudent = new Student
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Birthday = request.Birthday,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                StudentCode = studentCode,
                Major = major,
                IsActive = true,
                IsDeleted = false,
                CreatedDay = DateTime.UtcNow,
                UpdatedDay = DateTime.UtcNow
            };
            user = registeredStudent;
        }
        else
        {
            user = new Staff
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Email = email,
                Birthday = request.Birthday,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role,
                IsActive = true,
                IsDeleted = false,
                CreatedDay = DateTime.UtcNow,
                UpdatedDay = DateTime.UtcNow
            };
        }

        try
        {
            await _users.AddAsync(user, ct);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            return Result<AuthResponseDto>.Failure(
                "Không thể lưu tài khoản vì dữ liệu vượt quá độ dài hoặc vi phạm ràng buộc cơ sở dữ liệu: " + ex.Message,
                "DATA_SAVE_FAILED");
        }

        return Result<AuthResponseDto>.Success(CreateResponse(user, role, registeredStudent));
    }

    private AuthResponseDto CreateResponse(User user, UserRole role, Student? student)
    {
        var token = _tokens.GenerateToken(user.Id, user.FullName, (int)role, role.ToString());
        return new AuthResponseDto(
            token,
            "Bearer",
            _settings.ExpirationMinutes,
            new AuthUserDto(
                user.Id,
                user.FullName,
                user.Email,
                role.ToString(),
                student is not null ? "Student" : "Staff",
                student?.StudentCode,
                student?.Major));
    }
}
