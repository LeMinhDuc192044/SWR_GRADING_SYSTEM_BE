using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ExaminationRepository : Repository<Examination>, IExaminationRepository
{
    public ExaminationRepository(AppDbContext db) : base(db) { }

    public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
        => AnyAsync(e => e.ExaminationCode == code, ct);

    public Task<bool> HasPaperSetsAsync(Guid examinationId, CancellationToken ct = default)
        => _db.PaperSets.AnyAsync(m => m.ExaminationId == examinationId, ct);

    public async Task<(int Total, int Final)> GetSubmissionCountsAsync(Guid examinationId, CancellationToken ct = default)
    {
        var counts = await _db.StudentSubmissions
            .Where(submission => submission.StudentExamination.ExamId == examinationId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Final = group.Count(submission => submission.Status == SubmissionStatus.Final)
            })
            .FirstOrDefaultAsync(ct);

        return counts is null ? (0, 0) : (counts.Total, counts.Final);
    }
}