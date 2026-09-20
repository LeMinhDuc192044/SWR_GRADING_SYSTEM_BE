using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface ISubmissionService
{
    // FLOW 1: Upload Submission(s)
    Task<Result<UploadSubmissionsResultDTO>> UploadSubmissionsAsync(
        Guid diaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default
    );

    Task<Result<UploadSubmissionsResultDTO>> UploadAndGradeBatchAsync(
        Guid gradingDiaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default
    );

    // List submissions under a diary
    Task<Result<IReadOnlyList<SubmissionDetailDTO>>> GetListByDiaryIdAsync(
        Guid diaryId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    // Get submission detail under a diary
    Task<Result<SubmissionDetailDTO>> GetDetailAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> GetByIdAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    // FLOW 2: AI Grading
    Task<Result<SubmissionDetailDTO>> TriggerAiGradingAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> TriggerAiGradingAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    // FLOW 3: Lecturer Review
    Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid diaryId,
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
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

    // FLOW 4: Finalize
    Task<Result<SubmissionDetailDTO>> FinalizeAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    Task<Result<SubmissionDetailDTO>> FinalizeAsync(
        Guid submissionId,
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
