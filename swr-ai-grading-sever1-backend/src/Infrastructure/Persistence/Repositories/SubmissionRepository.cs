using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class SubmissionRepository : Repository<Submission>, ISubmissionRepository
{
    public SubmissionRepository(AppDbContext db) : base(db) { }

    public async Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default)
    {
        var submission = await _set.AsNoTracking().FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);
        if (submission is null) return false;

        return submission.LecturerScore.HasValue ||
               submission.Status == SubmissionStatus.Lecturer_Reviewed ||
               submission.Status == SubmissionStatus.Final;
    }

    public async Task<IReadOnlyList<Submission>> GetByDiaryIdAsync(Guid diaryId, CancellationToken ct = default)
    {
        return await _set
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Where(s => s.DiaryId == diaryId)
            .OrderByDescending(s => s.CreatedDate)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
