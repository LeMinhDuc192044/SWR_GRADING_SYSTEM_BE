using Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace WebApi.Common;

public sealed class FormFileDocumentAdapter : IDocumentFile
{
    private readonly IFormFile _file;

    public FormFileDocumentAdapter(IFormFile file)
    {
        _file = file ?? throw new ArgumentNullException(nameof(file));
    }

    public Stream OpenReadStream() => _file.OpenReadStream();

    public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
        => _file.CopyToAsync(target, cancellationToken);

    public string FileName => _file.FileName;

    public string? ContentType => _file.ContentType;

    public long Length => _file.Length;
}
