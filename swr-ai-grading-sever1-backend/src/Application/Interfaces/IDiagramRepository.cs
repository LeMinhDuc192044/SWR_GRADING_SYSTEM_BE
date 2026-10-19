using Domain.Entities;

namespace Application.Interfaces;

public interface IDiagramRepository : IRepository<Diagram>
{
    Task<Diagram?> GetWithElementsByIdAsync(Guid diagramId, CancellationToken ct = default);
    Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<bool> HasRubricsAsync(Guid diagramId, CancellationToken ct = default);
}
