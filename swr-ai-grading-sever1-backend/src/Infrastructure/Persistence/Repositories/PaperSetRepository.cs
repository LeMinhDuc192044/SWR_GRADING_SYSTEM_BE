using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class PaperSetRepository : Repository<PaperSet>, IPaperSetRepository
{
    public PaperSetRepository(AppDbContext db) : base(db) { }

    public override async Task<PaperSet?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _set
            .Include(material => material.Questions)
            .Include(material => material.CreateBy)
            .FirstOrDefaultAsync(material => material.PaperSetId == (Guid)id, ct);

    public override async Task<IReadOnlyList<PaperSet>> GetAllAsync(CancellationToken ct = default)
        => await _set
            .Include(material => material.Questions)
            .Include(material => material.CreateBy)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(m => m.PaperSetCode == code, ct);
}