using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository riêng cho Submission — kế thừa Repository&lt;Submission&gt; để có sẵn CRUD,
/// thêm 2 method nghiệp vụ: HasGradingsAsync (check FK khi xóa) và một số Include cho DTO.
/// </summary>
public sealed class SubmissionRepository : Repository<Submission>, ISubmissionRepository
{
    public SubmissionRepository(AppDbContext db) : base(db) { }

    /// <summary>
    /// Kiểm tra Submission có Grading liên kết hay không (dùng trước khi xóa).
    /// Trả true nếu có ít nhất 1 Grading (kể cả Cancelled).
    /// </summary>
    public Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default)
        => _db.Gradings.AnyAsync(g => g.SubmissionId == submissionId, ct);

    /// <summary>
    /// Lấy Submission kèm Lecturer + Gradings (cho GetByIdAsync → DTO chi tiết).
    /// Dùng AsNoTracking vì đây là read-only.
    /// </summary>
    public Task<Submission?> GetWithDetailsAsync(Guid id, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Include(s => s.Lecturer)
               .Include(s => s.Gradings)
               .FirstOrDefaultAsync(s => s.SubmissionId == id, ct);

    /// <summary>
    /// Lấy danh sách Submission kèm Lecturer (cho list page).
    /// Trả tuple (items, totalCount) sau khi sort/paging ở Service.
    /// </summary>
    public async Task<IReadOnlyList<Submission>> ListWithLecturerAsync(CancellationToken ct = default)
        => await _set.AsNoTracking()
                     .Include(s => s.Lecturer)
                     .ToListAsync(ct);
}
