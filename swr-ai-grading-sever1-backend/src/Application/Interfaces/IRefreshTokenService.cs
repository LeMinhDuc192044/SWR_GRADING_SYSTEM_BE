namespace Application.Interfaces;

public interface IRefreshTokenService
{
    string GenerateRefreshToken(Guid userId, string role);
    (bool IsValid, Guid UserId, string Role) ValidateRefreshToken(string token);
    void RevokeRefreshToken(string token);
    void RevokeAllUserTokens(Guid userId);
}
