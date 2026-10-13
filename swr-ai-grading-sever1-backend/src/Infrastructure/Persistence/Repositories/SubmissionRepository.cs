using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class SubmissionRepository : Repository<StudentSubmission>, ISubmissionRepository
{
    public SubmissionRepository(AppDbContext db) : base(db) { }

    public async Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default)
    {
        return await _set.AsNoTracking().AnyAsync(s =>
            s.SubmissionId == submissionId &&
            (s.Evaluations.Any(evaluation =>
                 evaluation.EvaluationType == EvaluationType.Lecturer && evaluation.TotalScore.HasValue) ||
             s.Status == SubmissionStatus.Lecturer_Reviewed ||
             s.Status == SubmissionStatus.Final), ct);
    }

    public async Task<IReadOnlyList<StudentSubmission>> GetByDiaryIdAsync(Guid diaryId, CancellationToken ct = default)
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
