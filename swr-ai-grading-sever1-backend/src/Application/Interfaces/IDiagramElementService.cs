using Application.Common;
using Application.DTOs.Diagrams;

namespace Application.Interfaces;

public interface IDiagramElementService
{
    Task<PagedResult<DiagramElementDTO>> GetPagedAsync(PagedRequest request, Guid? diagramId = null, string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<DiagramElementDTO>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default);
    Task<Result<DiagramElementDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<DiagramElementDTO>> CreateAsync(CreateDiagramElementRequest request, CancellationToken ct = default);
    Task<Result<DiagramElementDTO>> UpdateAsync(Guid id, UpdateDiagramElementRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
