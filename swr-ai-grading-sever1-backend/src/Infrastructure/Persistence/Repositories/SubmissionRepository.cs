using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository riêng cho Submission — kế thừa Repository&lt;Submission&gt; để có sẵn CRUD,
/// thêm method nghiệp vụ: HasGradingsAsync (check FK khi xóa) và Include cho DTO.
///
/// Cập nhật schema: Submission FK tới GradingDiary (không trực tiếp Lecturer).
/// </summary>
public sealed class SubmissionRepository : Repository<Submission>, ISubmissionRepository
{
    public SubmissionRepository(AppDbContext db) : base(db) { }

    /// <summary>
    /// Kiểm tra Submission có Grading liên kết hay không (dùng trước khi xóa).
    /// </summary>
    public Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default)
        => _db.Gradings.AnyAsync(g => g.SubmissionId == submissionId, ct);

    /// <summary>
    /// Lấy Submission kèm GradingDiary (+ Lecturer CreateBy) + Gradings (cho GetByIdAsync → DTO chi tiết).
    /// </summary>
    public Task<Submission?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Include(s => s.GradingDiary).ThenInclude(d => d.CreateBy)
               .Include(s => s.Gradings)
               .FirstOrDefaultAsync(s => s.SubmissionId == id, ct);

    /// <summary>
    /// Lấy danh sách Submission kèm GradingDiary (+ Lecturer CreateBy) cho list page.
    /// </summary>
    public async Task<IReadOnlyList<Submission>> ListWithDiaryAsync(CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(s => s.GradingDiary).ThenInclude(d => d.CreateBy)
                     .ToListAsync(ct);
}
