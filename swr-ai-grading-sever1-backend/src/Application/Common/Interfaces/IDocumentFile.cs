namespace Application.Common.Interfaces;

public interface IDocumentFile
{
    Stream OpenReadStream();
    Task CopyToAsync(Stream target, CancellationToken cancellationToken = default);
    string FileName { get; }
    string? ContentType { get; }
    long Length { get; }
}
