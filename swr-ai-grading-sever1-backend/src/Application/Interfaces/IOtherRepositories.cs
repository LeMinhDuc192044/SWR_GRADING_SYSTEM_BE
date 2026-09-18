using Domain.Entities;

namespace Application.Interfaces;

public interface ISubmissionRepository : IRepository<Submission>
{
    Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default);
}

public interface IPaperSetRepository : IRepository<PaperSet>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
}


