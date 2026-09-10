using Application.Common;
using Application.DTOs.ExamMaterials;
using Domain.Enums;

namespace Application.Interfaces;

public interface IExamMaterialService
{
    Task<PagedResult<ExamMaterialMetadataDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<ExamMaterialDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateAsync(
        Guid examinationId,
        IReadOnlyList<MaterialFileUpload> files,
        Guid createdById,
        CancellationToken ct = default);
    Task<Result<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateManyAsync(
        Guid examinationId,
        IReadOnlyList<IReadOnlyList<MaterialFileUpload>> materials,
        Guid createdById,
        CancellationToken ct = default);
    Task<Result<ExamMaterialDetailDTO>> AddFilesAsync(Guid id, IReadOnlyList<MaterialFileUpload> files, CancellationToken ct = default);
    Task<Result<ExamMaterialDetailDTO>> UpdateAsync(Guid id, UpdateExamMaterialRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<Result<StoredFileDownload>> DownloadAsync(Guid id, ExamMaterialFileType fileType, CancellationToken ct = default);
}

public sealed class StoredFileDownload
{
    public required Stream Content { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public string FileName { get; init; } = "download";
}