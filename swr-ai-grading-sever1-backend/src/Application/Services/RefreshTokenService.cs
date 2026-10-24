using System.Security.Cryptography;
using Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Application.Services;

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromDays(7);
    private const string TokenPrefix = "rt_";
    private const string UserTokensPrefix = "user_rt_";

    public RefreshTokenService(IMemoryCache cache)
    {
        _cache = cache;
    }

    private sealed record RefreshTokenData(Guid UserId, string Role, DateTime ExpiresAt);

    public string GenerateRefreshToken(Guid userId, string role)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        var token = Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");

        var data = new RefreshTokenData(userId, role, DateTime.UtcNow.Add(DefaultExpiry));

        _cache.Set(TokenPrefix + token, data, DefaultExpiry);

        // Keep track of tokens per user for bulk revocation
        var userKey = UserTokensPrefix + userId;
        var userTokens = _cache.GetOrCreate(userKey, entry =>
        {
            entry.SlidingExpiration = DefaultExpiry;
            return new HashSet<string>();
        });

        lock (userTokens!)
        {
            userTokens.Add(token);
        }

        return token;
    }

    public (bool IsValid, Guid UserId, string Role) ValidateRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return (false, Guid.Empty, string.Empty);

        if (_cache.TryGetValue(TokenPrefix + token, out RefreshTokenData? data) && data is not null)
        {
            if (data.ExpiresAt > DateTime.UtcNow)
            {
                return (true, data.UserId, data.Role);
            }

            _cache.Remove(TokenPrefix + token);
        }

        return (false, Guid.Empty, string.Empty);
    }

    public void RevokeRefreshToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return;

        if (_cache.TryGetValue(TokenPrefix + token, out RefreshTokenData? data) && data is not null)
        {
            _cache.Remove(TokenPrefix + token);

            var userKey = UserTokensPrefix + data.UserId;
            if (_cache.TryGetValue(userKey, out HashSet<string>? tokens) && tokens is not null)
            {
                lock (tokens)
                {
                    tokens.Remove(token);
                }
            }
        }
    }

    public void RevokeAllUserTokens(Guid userId)
    {
        var userKey = UserTokensPrefix + userId;
        if (_cache.TryGetValue(userKey, out HashSet<string>? tokens) && tokens is not null)
        {
            lock (tokens)
            {
                foreach (var token in tokens)
                {
                    _cache.Remove(TokenPrefix + token);
                }
                tokens.Clear();
            }
            _cache.Remove(userKey);
        }
    }
}
