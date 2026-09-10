using Application.Interfaces;
using System.Net.Http.Headers;

namespace Infrastructure.Storage;

public sealed class SupabaseStorage : ISupabaseStorage
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _bucket;

    public SupabaseStorage(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = (Environment.GetEnvironmentVariable("SUPABASE_URL") ?? string.Empty).TrimEnd('/');
        _bucket = Environment.GetEnvironmentVariable("SUPABASE_STORAGE_BUCKET") ?? "exam-materials";
        var secretKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY");

        if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(secretKey))
            throw new InvalidOperationException("SUPABASE_URL and SUPABASE_SECRET_KEY must be set.");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        _httpClient.DefaultRequestHeaders.Add("apikey", secretKey);
    }

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        // Buffer first: guarantees a known Content-Length regardless of whether
        // the incoming stream supports seeking, which avoids HttpClient silently
        // falling back to chunked transfer encoding for the binary payload.
        await using var buffered = new MemoryStream();
        await content.CopyToAsync(buffered, ct);
        buffered.Position = 0;

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildObjectUrl(path));
        var binaryContent = new StreamContent(buffered);
        binaryContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        binaryContent.Headers.ContentLength = buffered.Length;
        request.Content = binaryContent;
        request.Headers.Add("x-upsert", "false");
        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "upload");
    }

    public async Task<SupabaseFileMetadata> GetMetadataAsync(string path, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, BuildObjectUrl(path));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccessAsync(response, "metadata lookup");

        var contentDisposition = response.Content.Headers.ContentDisposition;
        return new SupabaseFileMetadata
        {
            FileName = contentDisposition?.FileNameStar?.Trim('"')
                ?? contentDisposition?.FileName?.Trim('"')
                ?? GetFileNameFromPath(path),
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            FileSize = response.Content.Headers.ContentLength ?? 0
        };
    }

    public async Task<StoredFileDownload> DownloadAsync(string path, string fileName, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(BuildObjectUrl(path), ct);
        await EnsureSuccessAsync(response, "download");
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return new StoredFileDownload
        {
            Content = new MemoryStream(bytes),
            ContentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            FileName = fileName
        };
    }

    public async Task DeleteAsync(string path, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BuildObjectUrl(path));
        using var response = await _httpClient.SendAsync(request, ct);
        await EnsureSuccessAsync(response, "delete");
    }

    private string BuildObjectUrl(string path)
    {
        var encodedPath = string.Join('/', path.Split('/').Select(Uri.EscapeDataString));
        return $"{_baseUrl}/storage/v1/object/{Uri.EscapeDataString(_bucket)}/{encodedPath}";
    }

    private static string GetFileNameFromPath(string path)
    {
        var storedName = Uri.UnescapeDataString(path[(path.LastIndexOf('/') + 1)..]);
        var separator = storedName.IndexOf('_');
        return separator > 0 && storedName[..separator].Length == 32
            ? storedName[(separator + 1)..]
            : storedName;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string operation)
    {
        if (response.IsSuccessStatusCode)
            return;

        var details = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Supabase storage {operation} failed ({(int)response.StatusCode}): {details}");
    }
}