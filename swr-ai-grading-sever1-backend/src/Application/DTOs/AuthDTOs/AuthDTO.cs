using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.AuthDTOs;
public record LoginRequest(
    [property: Required, EmailAddress] string? Email,
    [property: Required] string? Password);

public record RegisterRequest(
    string? FullName,
    [property: Required, EmailAddress] string? Email,
    string? CCCD,
    string? Password,
    DateOnly Birthday,
    int Role,
    string? StudentCode = null,
    string? Major = null,
    string? LecturerCode = null,
    string? Subject = null);
