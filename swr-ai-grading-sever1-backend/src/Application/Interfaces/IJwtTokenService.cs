namespace Application.Interfaces;

/// <summary>
/// Service sinh và validate JWT token.
/// </summary>
public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string fullName, int role, string discriminator);
}
