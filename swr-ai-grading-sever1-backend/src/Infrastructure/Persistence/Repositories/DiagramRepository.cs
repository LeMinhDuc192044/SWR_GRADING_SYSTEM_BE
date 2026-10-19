using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class DiagramRepository : Repository<Diagram>, IDiagramRepository
{
    public DiagramRepository(AppDbContext db) : base(db) { }

    public async Task<Diagram?> GetWithElementsByIdAsync(Guid diagramId, CancellationToken ct = default)
    {
        return await _db.Diagrams
            .Include(d => d.Elements)
            .FirstOrDefaultAsync(d => d.DiagramId == diagramId, ct);
    }

    public async Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return await _db.Diagrams.AnyAsync(d =>
            d.Name.ToLower() == normalized &&
            (!excludeId.HasValue || d.DiagramId != excludeId.Value), ct);
    }

    public async Task<bool> HasRubricsAsync(Guid diagramId, CancellationToken ct = default)
    {
        return await _db.Set<QuestionRubric>().AnyAsync(r => r.DiagramId == diagramId, ct);
    }
}
