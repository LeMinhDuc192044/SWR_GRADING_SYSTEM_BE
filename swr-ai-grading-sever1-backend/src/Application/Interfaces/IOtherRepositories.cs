using Domain.Entities;

namespace Application.Interfaces;

public interface ISubmissionRepository : IRepository<Submission>
{
    Task<bool> HasGradingsAsync(Guid submissionId, CancellationToken ct = default);
    Task<Submission?> GetWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> ListWithDiaryAsync(CancellationToken ct = default);
}

public interface IExamMaterialRepository : IRepository<ExamMaterial>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
}

public interface IGradingRepository : IRepository<Grading>
{
    Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default);
    Task<string> GenerateUniqueCodeAsync(CancellationToken ct = default);
}

public interface IGradingDiaryRepository : IRepository<GradingDiary>
{
    Task<GradingDiary?> GetWithLecturerAsync(Guid id, CancellationToken ct = default);
}
