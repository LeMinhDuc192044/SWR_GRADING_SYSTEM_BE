using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface ISubmissionService
{
    Task<Result<UploadSubmissionsResultDTO>> UploadAndGradeBatchAsync(
        Guid gradingDiaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> GetByIdAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> DecideScoreAsync(
        Guid submissionId,
        decimal lecturerScore,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> FinalizeScoreAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );
}

