using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class DiagramElementRepository : Repository<DiagramElement>, IDiagramElementRepository
{
    public DiagramElementRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<DiagramElement>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default)
    {
        return await _db.DiagramElements
            .Include(e => e.ReferenceElement)
            .Where(e => e.DiagramId == diagramId)
            .OrderBy(e => e.ElementCode)
            .ToListAsync(ct);
    }

    public async Task<DiagramElement?> GetWithDetailsByIdAsync(Guid elementId, CancellationToken ct = default)
    {
        return await _db.DiagramElements
            .Include(e => e.ReferenceElement)
            .Include(e => e.Diagram)
            .FirstOrDefaultAsync(e => e.ElementId == elementId, ct);
    }

    public async Task<bool> IsElementCodeExistsAsync(Guid diagramId, string elementCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = elementCode.Trim().ToLower();
        return await _db.DiagramElements.AnyAsync(e =>
            e.DiagramId == diagramId &&
            e.ElementCode.ToLower() == normalized &&
            (!excludeId.HasValue || e.ElementId != excludeId.Value), ct);
    }

    public async Task<bool> HasParsedAnswersAsync(Guid elementId, CancellationToken ct = default)
    {
        return await _db.Set<SubmissionParsedAnswer>().AnyAsync(p => p.ElementId == elementId, ct);
    }

    public async Task<bool> HasChildReferencesAsync(Guid elementId, CancellationToken ct = default)
    {
        return await _db.DiagramElements.AnyAsync(e => e.ElementReferenceId == elementId, ct);
    }
}
