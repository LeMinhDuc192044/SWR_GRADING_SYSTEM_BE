namespace Application.Interfaces;

public interface ISupabaseStorage
{
    Task UploadAsync(string path, Stream content, string contentType, CancellationToken ct = default);
    Task<SupabaseFileMetadata> GetMetadataAsync(string path, CancellationToken ct = default);
    Task<StoredFileDownload> DownloadAsync(string path, string fileName, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

public sealed class SupabaseFileMetadata
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long FileSize { get; init; }
}