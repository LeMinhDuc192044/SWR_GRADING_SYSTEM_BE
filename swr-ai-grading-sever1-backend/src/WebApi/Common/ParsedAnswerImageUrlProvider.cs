using System.Security.Cryptography;
using System.Text;
using Application.Common;
using Application.Common.Interfaces;

namespace WebApi.Common;

/// <summary>
/// Images live in a private bucket and are encrypted at rest, so a raw storage path
/// (or a Supabase URL) cannot be opened by a browser. This provider points at the API
/// endpoint that decrypts and streams the image, signed with an expiry so the link can
/// be used directly (e.g. in an img tag) without a bearer token.
/// </summary>
public sealed class ParsedAnswerImageUrlProvider : IImageUrlProvider
{
    private const int UrlLifetimeMinutes = 60;

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly byte[]? _signingKey;

    public ParsedAnswerImageUrlProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;

        var secret = Environment.GetEnvironmentVariable("IMAGE_URL_SIGNING_KEY")
            ?? Environment.GetEnvironmentVariable("JWT_SECRET");

        _signingKey = string.IsNullOrWhiteSpace(secret) ? null : Encoding.UTF8.GetBytes(secret);
    }

    public string? BuildParsedAnswerImageUrl(Guid parsedAnswerId, string? storedImagePath)
    {
        if (parsedAnswerId == Guid.Empty ||
            string.IsNullOrWhiteSpace(storedImagePath) ||
            _signingKey is null)
        {
            return null;
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
            return null;

        var expires = DateTimeOffset.UtcNow.AddMinutes(UrlLifetimeMinutes).ToUnixTimeSeconds();
        var signature = Sign($"{parsedAnswerId:N}:{expires}");

        return $"{request.Scheme}://{request.Host}{request.PathBase}" +
               $"/api/submission-parsed-answers/{parsedAnswerId}/image" +
               $"?expires={expires}&signature={signature}";
    }

    public string? BuildDiagramElementImageUrl(Guid elementId, string? storedImagePath)
    {
        // A template URL shares the column with the uploaded image and is not servable here.
        if (_signingKey is null || !DiagramElementImagePath.Matches(elementId, storedImagePath))
        {
            return null;
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null)
            return null;

        var expires = DateTimeOffset.UtcNow.AddMinutes(UrlLifetimeMinutes).ToUnixTimeSeconds();
        var signature = Sign(DiagramElementPayload(elementId, expires));

        return $"{request.Scheme}://{request.Host}{request.PathBase}" +
               $"/api/diagram-elements/{elementId}/image" +
               $"?expires={expires}&signature={signature}";
    }

    public bool IsValidSignature(Guid parsedAnswerId, long expires, string? signature)
    {
        if (_signingKey is null ||
            string.IsNullOrWhiteSpace(signature) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        return SignatureMatches(Sign($"{parsedAnswerId:N}:{expires}"), signature);
    }

    public bool IsValidDiagramElementSignature(Guid elementId, long expires, string? signature)
    {
        if (_signingKey is null ||
            string.IsNullOrWhiteSpace(signature) ||
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            return false;
        }

        return SignatureMatches(Sign(DiagramElementPayload(elementId, expires)), signature);
    }

    private static string DiagramElementPayload(Guid elementId, long expires) => $"element:{elementId:N}:{expires}";

    private static bool SignatureMatches(string expected, string actual)
        => CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(_signingKey!);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
