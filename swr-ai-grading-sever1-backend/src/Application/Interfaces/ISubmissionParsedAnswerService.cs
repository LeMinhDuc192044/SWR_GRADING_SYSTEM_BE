using Application.Common;
using Application.DTOs.Submissions;

namespace Application.Interfaces;

public interface ISubmissionParsedAnswerService
{
    Task<Result<IReadOnlyList<SubmissionParsedAnswerDTO>>> GetBySubmissionIdAsync(
        Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> GetByIdAsync(
        Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<StoredFileDownload>> GetImageAsync(
        Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> CreateAsync(
        SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result<SubmissionParsedAnswerDTO>> UpdateAsync(
        Guid id, SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);

    /// <summary>
    /// Links the answer to one diagram element of the diagram assigned to its question.
    /// </summary>
    Task<Result<SubmissionParsedAnswerDTO>> AssignElementAsync(
        Guid id, Guid? elementId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
}