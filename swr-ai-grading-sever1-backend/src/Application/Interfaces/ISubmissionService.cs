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
        ReviewSubmissionRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

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

    Task<Result<SubmissionDetailDTO>> UpdateItemDetailAsync(
        Guid detailId,
        UpdateEvaluationItemDetailRequest request,
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

    /// <summary>
    /// Xem lịch sử và dòng thời gian các bước của bài thi (Audit log)
    /// </summary>
    Task<Result<Application.DTOs.Submissions.SubmissionHistoryDto>> GetHistoryAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    /// <summary>
    /// Lấy danh sách bài nộp có phân trang, tìm kiếm, lọc (không bắt buộc diaryId, dùng cho Admin hoặc Giảng viên)
    /// </summary>
    Task<Result<PagedResult<Application.DTOs.Submissions.AdminSubmissionItemDto>>> GetSubmissionsAsync(
        Application.DTOs.Submissions.GetSubmissionsRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );

    /// <summary>
    /// Điều chỉnh trạng thái của một bài nộp
    /// </summary>
    Task<Result<Application.DTOs.Submissions.AdminSubmissionItemDto>> UpdateSubmissionStatusAsync(
        Guid submissionId,
        Application.DTOs.Submissions.UpdateSubmissionStatusRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default
    );
}
