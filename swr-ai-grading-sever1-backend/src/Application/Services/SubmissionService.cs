using Application.Common;
using Application.DTOs.Submissions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

/// <summary>
/// Service nghiệp vụ Submission &amp; Grading.
///
/// Tuân thủ pattern codebase:
///   - Validate input → check FK → mutate → SaveChangesAsync → return Result&lt;T&gt;.
///   - Không throw exception cho lỗi nghiệp vụ; throw chỉ cho lỗi hệ thống (IO, DB).
///
/// Phạm vi Plan B (6 method — xem ISubmissionService):
///   - Submission: GetPaged, GetById, Create (upload), Update, Delete.
///   - Grading:    CreateGradingAsync (sinh record Grading khi bắt đầu chấm AI).
///   - File:       DownloadFileAsync.
///
/// Cập nhật Plan B+ (theo DB schema thật):
///   - Submission FK tới GradingDiary (không trực tiếp Lecturer).
///   - Lecturer lấy gián tiếp qua GradingDiary.CreateBy.
/// </summary>
public sealed class SubmissionService : ISubmissionService
{
    private const string StorageBasePath = "submissions";

    private readonly ISubmissionRepository _submissionRepository;
    private readonly IGradingRepository _gradingRepository;
    private readonly IGradingDiaryRepository _gradingDiaryRepository;
    private readonly ISupabaseStorage _storage;
    private readonly IUnitOfWork _unitOfWork;

    public SubmissionService(
        ISubmissionRepository submissionRepository,
        IGradingRepository gradingRepository,
        IGradingDiaryRepository gradingDiaryRepository,
        ISupabaseStorage storage,
        IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _gradingRepository = gradingRepository;
        _gradingDiaryRepository = gradingDiaryRepository;
        _storage = storage;
        _unitOfWork = unitOfWork;
    }

    // ====================== GET LIST (PAGED) ======================
    public async Task<PagedResult<SubmissionSummaryDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default)
    {
        var all = await _submissionRepository.ListWithDiaryAsync(ct);
        var ordered = all
            .OrderByDescending(s => s.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var items = ordered.Select(ToSummaryDto).ToList();
        return new PagedResult<SubmissionSummaryDTO>
        {
            Items = items,
            TotalCount = all.Count,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    // ====================== GET BY ID ======================
    public async Task<Result<SubmissionDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var submission = await _submissionRepository.GetWithDetailsAsync(id, ct);
        return submission is null
            ? Result<SubmissionDTO>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND")
            : Result<SubmissionDTO>.Success(ToDto(submission));
    }

    // ====================== CREATE (upload file) ======================
    public async Task<Result<SubmissionDTO>> CreateAsync(CreateSubmissionRequest request, CancellationToken ct = default)
    {
        var validation = Validate(request.SubmissionName, request.Folder, request.DiaryId);
        if (validation is not null)
            return Result<SubmissionDTO>.Failure(validation, "INVALID_SUBMISSION");

        // Check FK: GradingDiary phải tồn tại
        var diary = await _gradingDiaryRepository.GetByIdAsync(request.DiaryId, ct);
        if (diary is null)
            return Result<SubmissionDTO>.Failure("Grading diary not found.", "GRADING_DIARY_NOT_FOUND");

        // Upload file lên Supabase Storage (nếu có) TRƯỚC khi tạo DB row.
        // Nếu upload fail → không có row orphan.
        //
        // QUAN TRỌNG: SubmissionId phải được generate trước (không đợi EF tự assign),
        // để upload thẳng vào folder "submissions/{submissionId}/..." chỉ 1 lần duy nhất.
        // Tránh bug stream-position: stream đã đọc đến cuối ở upload 1, nếu re-upload lần 2
        // (kiểu cũ: upload rồi đổi path) thì upload 2 sẽ đọc 0 byte.
        var submissionId = Guid.NewGuid();
        string? storagePath = null;
        if (request.File is not null && request.File.Length > 0)
        {
            if (!IsValidFileName(request.File.FileName))
                return Result<SubmissionDTO>.Failure("Invalid file name.", "INVALID_FILE_NAME");

            try
            {
                storagePath = await UploadSubmissionFileAsync(submissionId, request.File, ct);
            }
            catch (Exception ex)
            {
                return Result<SubmissionDTO>.Failure(
                    $"Failed to upload file: {ex.Message}",
                    "FILE_UPLOAD_FAILED");
            }
        }

        var now = DateTime.UtcNow;
        var submission = new Submission
        {
            SubmissionId = submissionId,
            SubmissionName = request.SubmissionName.Trim(),
            Folder = request.File is not null && storagePath is not null
                ? storagePath
                : request.Folder.Trim(),
            Status = SubmissionStatus.Draft,
            CreatedDate = now,
            UpdatedDate = now,
            DiaryId = request.DiaryId,   // ← Lecturer FK gián tiếp qua Diary.CreateById
        };

        await _submissionRepository.AddAsync(submission, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<SubmissionDTO>.Success(await ToDtoAsync(submission, ct));
    }

    // ====================== UPDATE ======================
    public async Task<Result<SubmissionDTO>> UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken ct = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(id, ct);
        if (submission is null)
            return Result<SubmissionDTO>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");

        if (!string.IsNullOrWhiteSpace(request.SubmissionName))
            submission.SubmissionName = request.SubmissionName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Folder))
            submission.Folder = request.Folder.Trim();
        if (request.Status.HasValue && Enum.IsDefined(request.Status.Value))
            submission.Status = request.Status.Value;

        // Đổi Diary: check FK mới tồn tại.
        // (Lưu ý: chưa check quyền Lecturer-owns-diary — sẽ làm khi có authz.)
        if (request.DiaryId.HasValue && request.DiaryId.Value != submission.DiaryId)
        {
            var diary = await _gradingDiaryRepository.GetByIdAsync(request.DiaryId.Value, ct);
            if (diary is null)
                return Result<SubmissionDTO>.Failure("Grading diary not found.", "GRADING_DIARY_NOT_FOUND");
            submission.DiaryId = request.DiaryId.Value;
        }

        submission.UpdatedDate = DateTime.UtcNow;

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<SubmissionDTO>.Success(ToDto(submission));
    }

    // ====================== DELETE ======================
    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(id, ct);
        if (submission is null)
            return Result.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");

        if (await _submissionRepository.HasGradingsAsync(id, ct))
            return Result.Failure("Cannot delete a submission that has gradings.", "SUBMISSION_HAS_GRADINGS");

        if (IsStoragePath(submission.Folder))
        {
            try { await _storage.DeleteAsync(submission.Folder, ct); }
            catch { /* không chặn flow nếu storage lỗi — DB đã xóa, file orphan có thể dọn sau */ }
        }

        _submissionRepository.Remove(submission);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    // ====================== CREATE GRADING ======================
    public async Task<Result<GradingDTO>> CreateGradingAsync(Guid submissionId, CreateGradingRequest request, CancellationToken ct = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(submissionId, ct);
        if (submission is null)
            return Result<GradingDTO>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");

        var code = await ResolveGradingCodeAsync(request.GradingCode, ct);

        var now = DateTime.UtcNow;
        var grading = new Grading
        {
            GradingCode = code,
            AiScore = request.AiScore,
            AiLogs = request.AiLogs,
            CreateDate = now,
            UpdateDate = now,
            Comment = request.Comment?.Trim(),
            Status = GradingStatus.Pending,
            SubmissionId = submissionId
        };

        await _gradingRepository.AddAsync(grading, ct);

        // Auto-update Submission status: Draft/Submitted → AI_Grading
        if (submission.Status == SubmissionStatus.Draft || submission.Status == SubmissionStatus.Submitted)
        {
            submission.Status = SubmissionStatus.AI_Grading;
            submission.UpdatedDate = now;
            _submissionRepository.Update(submission);
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return Result<GradingDTO>.Success(ToDto(grading));
    }

    // ====================== DOWNLOAD FILE ======================
    public async Task<Result<SubmissionFileDownload>> DownloadFileAsync(Guid id, CancellationToken ct = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(id, ct);
        if (submission is null)
            return Result<SubmissionFileDownload>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");

        if (!IsStoragePath(submission.Folder))
            return Result<SubmissionFileDownload>.Failure(
                "Submission has no uploaded file.",
                "FILE_NOT_FOUND");

        try
        {
            var metadata = await _storage.GetMetadataAsync(submission.Folder, ct);
            var download = await _storage.DownloadAsync(submission.Folder, metadata.FileName, ct);
            return Result<SubmissionFileDownload>.Success(new SubmissionFileDownload
            {
                Content = download.Content,
                ContentType = download.ContentType,
                FileName = download.FileName
            });
        }
        catch
        {
            return Result<SubmissionFileDownload>.Failure(
                "Failed to download file from storage.",
                "FILE_DOWNLOAD_FAILED");
        }
    }

    // ====================== HELPERS ======================

    private static string? Validate(string name, string folder, Guid diaryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Submission name is required.";
        if (string.IsNullOrWhiteSpace(folder))
            return "Folder is required.";
        if (diaryId == Guid.Empty)
            return "Grading diary is required.";
        if (name.Length > 200)
            return "Submission name must be 200 characters or fewer.";
        if (folder.Length > 500)
            return "Folder must be 500 characters or fewer.";
        return null;
    }

    private static bool IsValidFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        return !fileName.Contains("..") && !fileName.Contains('/') && !fileName.Contains('\\');
    }

    private static bool IsStoragePath(string folder)
        => !string.IsNullOrWhiteSpace(folder)
           && folder.StartsWith(StorageBasePath + "/", StringComparison.OrdinalIgnoreCase);

    private async Task<string> UploadSubmissionFileAsync(Guid submissionId, SubmissionFileUpload file, CancellationToken ct)
    {
        var path = $"{StorageBasePath}/{submissionId}/{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        await _storage.UploadAsync(path, file.Content, file.ContentType, ct);
        return path;
    }

    private async Task<string> ResolveGradingCodeAsync(string? requested, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            if (await _gradingRepository.IsCodeExistsAsync(requested, ct))
                throw new InvalidOperationException($"Grading code '{requested}' already exists.");
            return requested.Trim();
        }
        return await _gradingRepository.GenerateUniqueCodeAsync(ct);
    }

    private async Task<SubmissionDTO> ToDtoAsync(Submission submission, CancellationToken ct)
    {
        // Reload để có GradingDiary + Lecturer (CreateBy) sau khi Create.
        if (submission.GradingDiary is null || submission.GradingDiary.CreateBy is null)
        {
            var withDetails = await _submissionRepository.GetWithDetailsAsync(submission.SubmissionId, ct);
            if (withDetails is not null) submission = withDetails;
        }
        return ToDto(submission);
    }

    private static SubmissionDTO ToDto(Submission s) => new()
    {
        SubmissionId = s.SubmissionId,
        SubmissionName = s.SubmissionName,
        Folder = s.Folder,
        Status = s.Status,
        CreatedDate = s.CreatedDate,
        UpdatedDate = s.UpdatedDate,
        DiaryId = s.DiaryId,
        GradingDiaryName = s.GradingDiary?.Name,
        LecturerId = s.GradingDiary?.CreateById,
        LecturerName = s.GradingDiary?.CreateBy?.FullName,
        GradingCount = s.Gradings?.Count ?? 0
    };

    private static SubmissionSummaryDTO ToSummaryDto(Submission s) => new()
    {
        SubmissionId = s.SubmissionId,
        SubmissionName = s.SubmissionName,
        Status = s.Status,
        CreatedDate = s.CreatedDate,
        DiaryId = s.DiaryId,
        GradingCount = s.Gradings?.Count ?? 0
    };

    private static GradingDTO ToDto(Grading g) => new()
    {
        GradingId = g.GradingId,
        GradingCode = g.GradingCode,
        AiScore = g.AiScore,
        LecturerScore = g.LecturerScore,
        AiLogs = g.AiLogs,
        CreateDate = g.CreateDate,
        UpdateDate = g.UpdateDate,
        Comment = g.Comment,
        FinalScore = g.FinalScore,
        Status = g.Status,
        SubmissionId = g.SubmissionId
    };
}
