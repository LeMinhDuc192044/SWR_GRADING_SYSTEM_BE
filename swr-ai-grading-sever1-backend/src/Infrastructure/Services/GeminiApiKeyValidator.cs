using System.Net;
using Application.Common;
using Application.Common.Interfaces;

namespace Infrastructure.Services;

/// <summary>
/// Verifies an API key against Google Gemini by reading the model metadata. This costs no
/// tokens and fails when the key is invalid or the model is not available to it.
/// </summary>
public sealed class GeminiApiKeyValidator : IAiModelKeyValidator
{
    private readonly HttpClient _httpClient;

    public GeminiApiKeyValidator(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result> ValidateAsync(string modelName, string apiKey, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(modelName)}");
        request.Headers.Add("x-goog-api-key", apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Result.Failure(
                "Timed out while checking the API key with Google Gemini. Try again.",
                "AI_MODEL_KEY_CHECK_FAILED");
        }
        catch (HttpRequestException exception)
        {
            return Result.Failure(
                $"Could not reach Google Gemini to check the API key: {exception.Message}",
                "AI_MODEL_KEY_CHECK_FAILED");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
                return Result.Success();

            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ||
                body.Contains("API_KEY_INVALID", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure(
                    "The API key is not valid for Google Gemini or has no access to this model.",
                    "AI_MODEL_API_KEY_INVALID");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Result.Failure(
                    $"The API key cannot access AI model '{modelName}'. Check the model name and that the key's project is allowed to use it.",
                    "AI_MODEL_NOT_AVAILABLE");
            }

            // A quota problem means the key itself is fine.
            if (response.StatusCode == (HttpStatusCode)429)
                return Result.Success();

            return Result.Failure(
                $"Google Gemini rejected the API key check ({(int)response.StatusCode}): {Truncate(body)}",
                "AI_MODEL_KEY_CHECK_FAILED");
        }
    }

    private static string Truncate(string value) => value.Length <= 300 ? value : value[..300] + "…";
}
