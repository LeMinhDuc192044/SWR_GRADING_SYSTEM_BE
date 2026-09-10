using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Persistence.Repositories;

public sealed class ExamMaterialRepository : Repository<ExamMaterial>, IExamMaterialRepository
{
    public ExamMaterialRepository(AppDbContext db) : base(db) { }

    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(m => m.ExamMaterialCode == code, ct);
}