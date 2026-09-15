using Application.Common;
using Application.DTOs.Submissions;

namespace Application.Interfaces;

/// <summary>
/// Service nghiệp vụ cho Submission & Grading.
/// Tất cả method trả Result&lt;T&gt; / PagedResult&lt;T&gt; thay vì throw exception
/// (đồng nhất với ExaminationService / ExamMaterialService trong codebase).
///
/// Phạm vi Plan B:
///   - Submission: CRUD + upload/download file (Supabase Storage).
///   - Grading:    chỉ CreateAsync (sinh Grading khi submission đầu vào AI_Grading).
///                 Review/Update sẽ làm ở Plan sau.
/// </summary>
public interface ISubmissionService
{
    // -------- Submission --------
    Task<PagedResult<SubmissionSummaryDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SubmissionDTO>> CreateAsync(CreateSubmissionRequest request, CancellationToken ct = default);
    Task<Result<SubmissionDTO>> UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    // -------- Grading --------
    Task<Result<GradingDTO>> CreateGradingAsync(Guid submissionId, CreateGradingRequest request, CancellationToken ct = default);

    // -------- File (Submission) --------
    Task<Result<SubmissionFileDownload>> DownloadFileAsync(Guid id, CancellationToken ct = default);
}
