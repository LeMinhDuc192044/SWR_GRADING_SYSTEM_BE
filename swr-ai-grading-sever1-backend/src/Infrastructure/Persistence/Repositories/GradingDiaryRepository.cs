using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class GradingDiaryRepository : Repository<GradingDiary>, IGradingDiaryRepository
{
    public GradingDiaryRepository(AppDbContext db) : base(db) { }

    public async Task<GradingDiary?> GetDetailByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _set
            .Include(gd => gd.PaperSet)
            .Include(gd => gd.CreatedBy)
            .Include(gd => gd.Submissions)
            .FirstOrDefaultAsync(gd => gd.GradingDiaryId == id, ct);
    }

    public async Task<IReadOnlyList<GradingDiary>> GetPagedAsync(int skip, int take, Guid? lecturerId = null, CancellationToken ct = default)
    {
        var query = _set
            .Include(gd => gd.PaperSet)
            .Include(gd => gd.CreatedBy)
            .Include(gd => gd.Submissions)
            .AsNoTracking();

        if (lecturerId.HasValue)
        {
            query = query.Where(gd => gd.CreateById == lecturerId.Value);
        }

        return await query
            .OrderByDescending(gd => gd.GradingDiaryId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountByLecturerAsync(Guid? lecturerId = null, CancellationToken ct = default)
    {
        if (lecturerId.HasValue)
        {
            return await _set.CountAsync(gd => gd.CreateById == lecturerId.Value, ct);
        }

        return await _set.CountAsync(ct);
    }

    public Task<bool> ExistsByPaperSetIdAsync(Guid paperSetId, CancellationToken ct = default)
    {
        return _set.AnyAsync(gd => gd.PaperSetId == paperSetId, ct);
    }
}
