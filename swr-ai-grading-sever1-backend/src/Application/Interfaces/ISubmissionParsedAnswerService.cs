using Application.Common;
using Application.DTOs.Submissions;

namespace Application.Interfaces;

public interface ISubmissionParsedAnswerService
{
    Task<Result<IReadOnlyList<SubmissionParsedAnswerDTO>>> GetBySubmissionIdAsync(
        Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> GetByIdAsync(
        Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> CreateAsync(
        SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> UpdateAsync(
        Guid id, SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
}