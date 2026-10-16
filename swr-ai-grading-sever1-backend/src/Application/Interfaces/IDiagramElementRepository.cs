using Domain.Entities;

namespace Application.Interfaces;

public interface IDiagramElementRepository : IRepository<DiagramElement>
{
    Task<IReadOnlyList<DiagramElement>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default);
    Task<DiagramElement?> GetWithDetailsByIdAsync(Guid elementId, CancellationToken ct = default);
    Task<bool> IsElementCodeExistsAsync(Guid diagramId, string elementCode, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> HasParsedAnswersAsync(Guid elementId, CancellationToken ct = default);
    Task<bool> HasChildReferencesAsync(Guid elementId, CancellationToken ct = default);
}
