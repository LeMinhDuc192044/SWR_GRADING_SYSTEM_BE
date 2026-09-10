using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ExaminationRepository : Repository<Examination>, IExaminationRepository
{
    public ExaminationRepository(AppDbContext db) : base(db) { }

    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(e => e.ExaminationCode == code, ct);

    public Task<bool> HasExamMaterialsAsync(Guid examinationId, CancellationToken ct = default)
        => _db.ExamMaterials.AnyAsync(m => m.ExaminationId == examinationId, ct);
}