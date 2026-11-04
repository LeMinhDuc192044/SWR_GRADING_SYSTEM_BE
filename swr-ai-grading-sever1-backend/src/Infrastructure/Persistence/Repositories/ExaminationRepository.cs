using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ExaminationRepository : Repository<Examination>, IExaminationRepository
{
    public ExaminationRepository(AppDbContext db) : base(db) { }


    public Task<bool> HasPaperSetsAsync(Guid examinationId, CancellationToken ct = default)
        => _db.PaperSets.AnyAsync(m => m.ExaminationId == examinationId, ct);
}