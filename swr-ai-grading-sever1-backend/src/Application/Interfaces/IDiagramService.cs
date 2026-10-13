using Application.Common;
using Application.DTOs.Diagrams;

namespace Application.Interfaces;

public interface IDiagramService
{
    Task<PagedResult<DiagramDTO>> GetPagedAsync(PagedRequest request, string? search = null, CancellationToken ct = default);
    Task<Result<DiagramDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<DiagramDetailDTO>> CreateAsync(CreateDiagramRequest request, CancellationToken ct = default);
    Task<Result<DiagramDTO>> UpdateAsync(Guid id, UpdateDiagramRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}
