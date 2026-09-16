using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository cho GradingDiary. Chỉ thêm 1 method nghiệp vụ là đủ
/// (các CRUD thường đã có sẵn ở Repository&lt;TBase&gt;).
/// </summary>
public sealed class GradingDiaryRepository : Repository<GradingDiary>, IGradingDiaryRepository
{
    public GradingDiaryRepository(AppDbContext db) : base(db) { }

    /// <summary>
    /// Lấy GradingDiary kèm Lecturer (CreateBy) — dùng cho query ở Service khi cần tên Lecturer.
    /// </summary>
    public Task<GradingDiary?> GetWithLecturerAsync(Guid id, CancellationToken ct = default)
        => _set.AsNoTracking()
               .Include(d => d.CreateBy)
               .FirstOrDefaultAsync(d => d.GradingDiaryId == id, ct);
}
