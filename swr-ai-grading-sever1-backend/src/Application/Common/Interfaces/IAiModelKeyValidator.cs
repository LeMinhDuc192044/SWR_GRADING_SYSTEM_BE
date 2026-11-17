using Application.Common;

namespace Application.Common.Interfaces;

public interface IAiModelKeyValidator
{
    /// <summary>
    /// Checks that <paramref name="apiKey"/> can actually call <paramref name="modelName"/>.
    /// Returns a failure with a user-facing message when it cannot.
    /// </summary>
    Task<Result> ValidateAsync(string modelName, string apiKey, CancellationToken ct = default);
}
