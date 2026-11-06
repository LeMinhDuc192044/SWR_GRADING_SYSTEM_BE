using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface ISubmissionService
{
    // FLOW 1: Upload Submission(s)
    Task<Result<UploadSubmissionsResultDTO>> UploadSubmissionsAsync(
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        Guid? examinationSessionId = null,
        CancellationToken ct = default
    );

    Task<Result<IReadOnlyList<SubmissionDetailDTO>>> GetListAsync(
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
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    // FLOW 3: Lecturer Review
    Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid submissionId,
        ReviewSubmissionRequest request,
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

    // FLOW 3: Finalize
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

    /// <summary>
    /// Xem lịch sử và dòng thời gian các bước của bài thi (Audit log)
    /// </summary>
    Task<Result<Application.DTOs.Submissions.SubmissionHistoryDto>> GetHistoryAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );
}
