using Application.Common;
using Application.DTOs.Grading;

namespace Application.Interfaces;

public interface ISubmissionGradingService
{
    Task<Result<IReadOnlyList<GradingHistoryDTO>>> GetHistoriesAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default);

    Task<Result<GradingHistoryDTO>> GradeWithAiAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default);

    Task<Result<GradingHistoryDTO>> ReviewAsync(
        Guid submissionId, ReviewGradingRequest request, Guid currentUserId, CancellationToken ct = default);

    Task<Result<GradingHistoryDTO>> FinalizeAsync(
        Guid submissionId, Guid historyId, Guid currentUserId, CancellationToken ct = default);
}