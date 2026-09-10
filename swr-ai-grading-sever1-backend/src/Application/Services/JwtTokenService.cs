using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Common;
using Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Application.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _settings;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.Secret) ||
            Encoding.UTF8.GetByteCount(_settings.Secret) < 32)
        {
            throw new InvalidOperationException("Jwt:Secret must be at least 32 characters.");
        }
    }

    public string GenerateToken(Guid userId, string fullName, int role, string discriminator)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("discriminator", discriminator)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}