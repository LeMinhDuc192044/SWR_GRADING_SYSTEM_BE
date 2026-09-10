using AI_Assisted_SWR_Grading_System.Domain.Enums;
using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Application.DTOs.AuthDTOs;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _tokens;
    private readonly JwtSettings _settings;

    public AuthController(
        IUserRepository users,
        IUnitOfWork uow,
        IJwtTokenService tokens,
        IOptions<JwtSettings> options)
    {
        _users = users;
        _uow = uow;
        _tokens = tokens;
        _settings = options.Value;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email?.Trim().ToLowerInvariant();
        var user = string.IsNullOrWhiteSpace(email)
            ? null
            : await _users.GetByEmailAsync(email, ct);
        if (user is null || user.IsDeleted || !user.IsActive ||
            string.IsNullOrWhiteSpace(request.Password) ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid credentials." });
        }

        var token = _tokens.GenerateToken(
            user.Id,
            user.FullName,
            (int)user.Role,
            user.Role.ToString());

        return Ok(new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresInMinutes = _settings.ExpirationMinutes,
            user = new { user.Id, user.FullName, role = user.Role.ToString() }
        });
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var fullName = request.FullName?.Trim();
        var email = request.Email?.Trim().ToLowerInvariant();
        var cccd = request.CCCD?.Trim();
        var studentCode = request.StudentCode?.Trim();
        var major = request.Major?.Trim();
        var lecturerCode = request.LecturerCode?.Trim();
        var subject = request.Subject?.Trim();

        if (string.IsNullOrWhiteSpace(fullName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(cccd) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            request.Birthday == default ||
            !Enum.IsDefined(typeof(UserRole), request.Role))
        {
            return BadRequest(new
            {
                message = "FullName, Email, CCCD, Password, Birthday, and a valid Role are required."
            });
        }

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        if (request.Role == (int)UserRole.Student &&
            (string.IsNullOrWhiteSpace(studentCode) || string.IsNullOrWhiteSpace(major)))
        {
            return BadRequest(new
            {
                message = "StudentCode and Major are required for Student accounts."
            });
        }

        if (request.Role == (int)UserRole.Lecturer &&
            (string.IsNullOrWhiteSpace(lecturerCode) || string.IsNullOrWhiteSpace(subject)))
        {
            return BadRequest(new
            {
                message = "LecturerCode and Subject are required for Lecturer accounts."
            });
        }

        if (await _users.IsEmailExistsAsync(email, ct))
            return Conflict(new { message = "An account with this email already exists." });

        if (!string.IsNullOrWhiteSpace(studentCode) &&
            await _users.IsStudentCodeExistsAsync(studentCode, ct))
        {
            return Conflict(new { message = "An account with this student code already exists." });
        }

        if (!string.IsNullOrWhiteSpace(lecturerCode) &&
            await _users.IsLecturerCodeExistsAsync(lecturerCode, ct))
        {
            return Conflict(new { message = "An account with this lecturer code already exists." });
        }

        var user = new Domain.Entities.User
        {
            FullName = fullName,
            Email = email,
            Birthday = request.Birthday,
            Cccd = cccd,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = (UserRole)request.Role,
            IsActive = true,
            IsDeleted = false
        };

        user = request.Role switch
        {
            (int)UserRole.Student => new Domain.Entities.Student
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Birthday = user.Birthday,
                Cccd = user.Cccd,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                StundentCode = studentCode!,
                Major = major!
            },
            (int)UserRole.Lecturer => new Domain.Entities.Lecturer
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Birthday = user.Birthday,
                Cccd = user.Cccd,
                PasswordHash = user.PasswordHash,
                Role = user.Role,
                IsActive = user.IsActive,
                IsDeleted = user.IsDeleted,
                LecturerCode = lecturerCode!,
                Subject = subject!
            },
            _ => user
        };

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        var token = _tokens.GenerateToken(
            user.Id,
            user.FullName,
            (int)user.Role,
            user.Role.ToString());

        return Created("api/Auth/login", new
        {
            accessToken = token,
            tokenType = "Bearer",
            expiresInMinutes = _settings.ExpirationMinutes,
            user = new { user.Id, user.FullName, role = user.Role.ToString() }
        });
    }
}

