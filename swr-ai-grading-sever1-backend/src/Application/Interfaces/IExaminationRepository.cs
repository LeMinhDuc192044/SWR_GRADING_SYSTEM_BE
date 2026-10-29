using Domain.Entities;

namespace Application.Interfaces;

public interface IExaminationRepository : IRepository<Examination>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
    Task<bool> HasPaperSetsAsync(Guid examinationId, CancellationToken ct = default);
}