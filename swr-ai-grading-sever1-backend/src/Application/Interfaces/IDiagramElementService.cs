using Application.Common;
using Application.DTOs.Diagrams;

namespace Application.Interfaces;

public interface IDiagramElementService
{
    Task<PagedResult<DiagramElementDTO>> GetPagedAsync(PagedRequest request, Guid? diagramId = null, string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<DiagramElementDTO>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default);
    Task<Result<DiagramElementDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    /// <summary>Creates an element; when an image is supplied, it is stored and fills FileTemplateUrl.</summary>
    Task<Result<DiagramElementDTO>> CreateAsync(
        CreateDiagramElementRequest request,
        DiagramElementImageUpload? image = null,
        CancellationToken ct = default);

    /// <summary>Moves an existing element into another diagram.</summary>
    Task<Result<DiagramElementDTO>> AssignToDiagramAsync(Guid diagramId, AssignDiagramElementRequest request, CancellationToken ct = default);
    Task<Result<DiagramElementDTO>> UpdateAsync(Guid id, UpdateDiagramElementRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<Result<DiagramElementDTO>> UploadImageAsync(Guid id, DiagramElementImageUpload upload, CancellationToken ct = default);

    Task<Result> DeleteImageAsync(Guid id, CancellationToken ct = default);

    Task<Result<StoredFileDownload>> GetImageAsync(Guid id, CancellationToken ct = default);

    /// <summary>Points this element at another element of the same diagram.</summary>
    Task<Result<DiagramElementDTO>> ConnectAsync(Guid id, ConnectDiagramElementRequest request, CancellationToken ct = default);

    /// <summary>Removes the connection of this element to another element.</summary>
    Task<Result<DiagramElementDTO>> DisconnectAsync(Guid id, CancellationToken ct = default);
}
