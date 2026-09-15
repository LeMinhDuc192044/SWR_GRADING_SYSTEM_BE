using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class ExamMaterialRepository : Repository<ExamMaterial>, IExamMaterialRepository
{
    public ExamMaterialRepository(AppDbContext db) : base(db) { }

    public override async Task<ExamMaterial?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _set.Include(material => material.Questions).FirstOrDefaultAsync(material => material.ExamMaterialId == (Guid)id, ct);

    public override async Task<IReadOnlyList<ExamMaterial>> GetAllAsync(CancellationToken ct = default)
        => await _set.Include(material => material.Questions).AsNoTracking().ToListAsync(ct);

    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(m => m.ExamMaterialCode == code, ct);
}