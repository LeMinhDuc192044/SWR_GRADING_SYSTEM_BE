using Domain.Entities;

namespace Application.Interfaces;

public interface ISemesterRepository : IRepository<Semester>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
    Task<bool> HasExaminationsAsync(Guid semesterId, CancellationToken ct = default);
}
