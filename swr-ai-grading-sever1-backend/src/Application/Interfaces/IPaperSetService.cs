using Application.Common;
using Application.DTOs.PaperSets;
using Domain.Enums;

namespace Application.Interfaces;

public interface IPaperSetService
{
    Task<PagedResult<PaperSetMetadataDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<PaperSetDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<PaperSetMetadataDTO>>> CreateAsync(
        Guid semesterId,
        string description,
        IReadOnlyList<CreateQuestionInput> questions,
        IReadOnlyList<MaterialFileUpload> files,
        Guid createdById,
        CancellationToken ct = default);
    Task<Result<IReadOnlyList<PaperSetMetadataDTO>>> CreateManyAsync(
        Guid semesterId,
        IReadOnlyList<CreatePaperSetInput> materials,
        Guid createdById,
        CancellationToken ct = default);
    Task<Result<PaperSetDetailDTO>> AddFilesAsync(Guid id, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct = default);
    Task<Result<PaperSetDetailDTO>> UpdateAsync(Guid id, UpdatePaperSetRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<StoredFileDownload>> DownloadAsync(Guid id, CancellationToken ct = default);
}

public sealed class StoredFileDownload
{
    public required Stream Content { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "download";
}