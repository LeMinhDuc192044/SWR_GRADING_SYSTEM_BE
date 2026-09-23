using Domain.Entities;

namespace Application.Interfaces;

public interface ISemesterRepository : IRepository<Semester>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
    Task<bool> IsDateRangeOverlappingAsync(DateOnly startDate, DateOnly endDate, Guid? excludedSemesterId = null, CancellationToken ct = default);
    Task<bool> HasExaminationsAsync(Guid semesterId, CancellationToken ct = default);
}
