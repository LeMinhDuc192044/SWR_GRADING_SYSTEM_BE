using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.AuthDTOs;
public record LoginRequest(
    [property: Required, EmailAddress] string? Email,
    [property: Required] string? Password);

public record RegisterRequest(
    string? FullName,
    [property: Required, EmailAddress] string? Email,
    string? Password,
    DateOnly Birthday,
    int Role,
    string? StudentCode = null,
    string? Major = null,
    string? StaffCode = null,
    string? Subject = null);

public sealed record AuthResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes,
    AuthUserDto User);

public sealed record AuthUserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    string Type,
    string? StudentCode = null,
    string? Major = null,
    string? StaffCode = null);


public sealed class MonitorDTO
{
    public Guid Id { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly Birthday { get; set; }
    public bool IsActive { get; set; }
}