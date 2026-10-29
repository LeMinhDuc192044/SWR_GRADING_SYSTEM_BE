using Domain.Entities;

namespace Application.Interfaces;

public interface IGradingDiaryRepository : IRepository<GradingDiary>
{
    Task<GradingDiary?> GetDetailByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GradingDiary>> GetPagedAsync(int skip, int take, Guid? lecturerId = null, CancellationToken ct = default);
    Task<int> CountByLecturerAsync(Guid? lecturerId = null, CancellationToken ct = default);
    Task<bool> ExistsByPaperSetIdAsync(Guid paperSetId, CancellationToken ct = default);
}
