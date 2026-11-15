namespace Application.Common.Interfaces;

/// <summary>
/// Builds browser-viewable links for images stored in private (and encrypted) object storage.
/// </summary>
public interface IImageUrlProvider
{
    /// <summary>
    /// Returns an absolute, directly usable URL for the image of a parsed answer,
    /// or null when the answer has no stored image.
    /// </summary>
    string? BuildParsedAnswerImageUrl(Guid parsedAnswerId, string? storedImagePath);

    /// <summary>
    /// Validates the signed query string produced by <see cref="BuildParsedAnswerImageUrl"/>.
    /// </summary>
    bool IsValidSignature(Guid parsedAnswerId, long expires, string? signature);
}
