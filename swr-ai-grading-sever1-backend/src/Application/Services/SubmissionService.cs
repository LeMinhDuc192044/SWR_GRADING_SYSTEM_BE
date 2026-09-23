using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed partial class SubmissionService : ISubmissionService
{
    private readonly IGradingDiaryRepository _diaryRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IPaperSetRepository _paperSetRepository;
    private readonly ISupabaseStorage _storage;
    private readonly IGeminiAIService _geminiService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<SubmissionService> _logger;

    public SubmissionService(
        IGradingDiaryRepository diaryRepository,
        ISubmissionRepository submissionRepository,
        IPaperSetRepository paperSetRepository,
        ISupabaseStorage storage,
        IGeminiAIService geminiService,
        IUnitOfWork unitOfWork,
        IApplicationDbContext dbContext,
        ILogger<SubmissionService> logger)
    {
        _diaryRepository = diaryRepository;
        _submissionRepository = submissionRepository;
        _paperSetRepository = paperSetRepository;
        _storage = storage;
        _geminiService = geminiService;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
        _logger = logger;
    }

    public Task<Result<UploadSubmissionsResultDTO>> UploadAndGradeBatchAsync(
        Guid gradingDiaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        return UploadSubmissionsAsync(gradingDiaryId, files, currentUserId, ct);
    }

    // FLOW 1: Upload Submission
    public async Task<Result<UploadSubmissionsResultDTO>> UploadSubmissionsAsync(
        Guid diaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Vui lòng chọn ít nhất một file bài làm (.docx).", "FILES_REQUIRED");
        }

        var diary = await _diaryRepository.GetDetailByIdAsync(diaryId, ct);
        if (diary is null)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy sổ chấm thi.", "DIARY_NOT_FOUND");
        }

        var paperSet = diary.PaperSet ?? await _paperSetRepository.GetByIdAsync(diary.PaperSetId, ct);
        if (paperSet is null)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy thông tin đề thi liên kết.", "PAPER_SET_NOT_FOUND");
        }

        // Tải và chuẩn bị nội dung Rubric
        var rubricText = await LoadRubricTextAsync(paperSet, ct);

        // Chuẩn bị ExamId
        if (!paperSet.ExaminationId.HasValue || paperSet.ExaminationId.Value == Guid.Empty)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("PaperSet chưa liên kết với kỳ thi.", "PAPER_SET_NOT_LINKED_TO_EXAM");
        }

        var examId = paperSet.ExaminationId.Value;

        // Định dạng storage path: submissions/{paperSetCode}/{originalFileName}
        var paperSetCode = !string.IsNullOrWhiteSpace(paperSet?.PaperSetCode)
            ? paperSet.PaperSetCode.Trim()
            : "UNKNOWN_PAPERSET";
        var safePaperSetCode = NormalizeStorageKeySegment(paperSetCode);

        var results = new List<UploadedSubmissionSummaryDTO>();
        int successCount = 0;

        foreach (var file in files)
        {
            var summary = new UploadedSubmissionSummaryDTO
            {
                SubmissionFile = file.FileName
            };

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                summary.IsSuccess = false;
                summary.ErrorMessage = "Hệ thống chỉ hỗ trợ định dạng bài làm Word (.docx).";
                results.Add(summary);
                continue;
            }

            try
            {
                // 1. Kiểm tra và tìm StudentExamination trước (Regex [A-Za-z]{2}\d{5,8})
                var studentExamResult = await ResolveStudentExaminationAsync(file.FileName, examId, ct);
                if (!studentExamResult.IsSuccess)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = studentExamResult.Error;
                    results.Add(summary);
                    continue;
                }

                var studentExam = studentExamResult.Data!;

                // 2. Đọc file vào memoryStream
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream, ct);
                memoryStream.Position = 0;

                // 3. Trích xuất text từ file docx trước để validate nội dung
                var studentText = await _geminiService.ExtractTextFromDocxAsync(memoryStream, ct);
                if (string.IsNullOrWhiteSpace(studentText))
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = "Không thể trích xuất văn bản từ bài làm (file rỗng hoặc chỉ có ảnh scan).";
                    results.Add(summary);
                    continue;
                }

                // 4. File hợp lệ -> Upload lên Supabase Storage với format chuẩn
                memoryStream.Position = 0;
                var storagePath = $"submissions/{safePaperSetCode}/{file.FileName}";
                await _storage.UploadAsync(storagePath, memoryStream, file.ContentType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct);

                // 5. Tạo bản ghi Submission với status = Submitted (0)
                var submission = new Submission
                {
                    SubmissionId = Guid.NewGuid(),
                    SubmissionFile = file.FileName,
                    FilePath = storagePath,
                    Status = SubmissionStatus.Submitted, // 0 = Submitted
                    DiaryId = diaryId,
                    StudentExaminationId = studentExam.StudentExaminationId,
                    Comment = string.Empty, // Nhận xét dành riêng cho giảng viên
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };

                await _submissionRepository.AddAsync(submission, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                summary.SubmissionId = submission.SubmissionId;
                summary.FilePath = submission.FilePath;

                // 6. Tùy chọn: Chấm AI tự động ngay sau khi upload
                try
                {
                    var gradeResult = await _geminiService.GradeSubmissionAsync(studentText, rubricText, ct);
                    submission.AiScore = gradeResult.TotalScore;
                    submission.AiLogs = gradeResult.RawAiLogJson;
                    submission.Status = SubmissionStatus.AI_Graded; // 1 = AI_Graded

                    _submissionRepository.Update(submission);
                    await _unitOfWork.SaveChangesAsync(ct);

                    summary.AiScore = submission.AiScore;
                    summary.CriteriaScores = gradeResult.CriteriaScores;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Chưa thể hoàn tất chấm AI ngay lúc upload cho bài {File}. Trạng thái giữ nguyên Submitted.", file.FileName);
                }

                summary.IsSuccess = true;
                summary.Status = submission.Status.ToString();
                summary.Comment = submission.Comment;
                successCount++;
            }
            catch (Exception ex)
            {
                summary.IsSuccess = false;
                summary.ErrorMessage = $"Lỗi xử lý: {ex.Message}";
            }

            results.Add(summary);
        }

        var response = new UploadSubmissionsResultDTO
        {
            GradingDiaryId = diaryId,
            TotalUploaded = files.Count,
            TotalGradedSuccessfully = successCount,
            Submissions = results
        };

        return Result<UploadSubmissionsResultDTO>.Success(response);
    }

    // List submissions under a diary
    public async Task<Result<IReadOnlyList<SubmissionDetailDTO>>> GetListByDiaryIdAsync(
        Guid diaryId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var diary = await _diaryRepository.GetByIdAsync(diaryId, ct);
        if (diary is null)
        {
            return Result<IReadOnlyList<SubmissionDetailDTO>>.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result<IReadOnlyList<SubmissionDetailDTO>>.Failure("Bạn không có quyền truy cập sổ chấm này.", "FORBIDDEN");
        }

        var submissions = await _dbContext.Submissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Where(s => s.DiaryId == diaryId)
            .OrderByDescending(s => s.CreatedDate)
            .ToListAsync(ct);

        var dtos = submissions.Select(ToDetailDto).ToList();
        return Result<IReadOnlyList<SubmissionDetailDTO>>.Success(dtos);
    }

    // Get submission detail under a diary
    public async Task<Result<SubmissionDetailDTO>> GetDetailAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.Submissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && s.DiaryId == diaryId, ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp trong sổ chấm này.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền truy cập bài nộp này.", "FORBIDDEN");
        }

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public async Task<Result<SubmissionDetailDTO>> GetByIdAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.Submissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền truy cập bài nộp này.", "FORBIDDEN");
        }

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    // FLOW 2: AI Grading
    public async Task<Result<SubmissionDetailDTO>> TriggerAiGradingAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.Submissions
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.PaperSet)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && (diaryId == Guid.Empty || s.DiaryId == diaryId), ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền kích hoạt chấm AI cho bài nộp này.", "FORBIDDEN");
        }

        if (string.IsNullOrWhiteSpace(submission.FilePath))
        {
            return Result<SubmissionDetailDTO>.Failure("Bài nộp chưa có file đính kèm trên hệ thống lưu trữ.", "FILE_NOT_FOUND");
        }

        // 1. Tải file từ storage
        var download = await _storage.DownloadAsync(submission.FilePath, "submissions", ct);

        // 2. Trích xuất text
        var studentText = await _geminiService.ExtractTextFromDocxAsync(download.Content, ct);
        if (string.IsNullOrWhiteSpace(studentText))
        {
            return Result<SubmissionDetailDTO>.Failure("Không thể trích xuất văn bản từ file bài làm.", "EMPTY_CONTENT");
        }

        // 3. Lấy Rubric
        var paperSet = submission.GradingDiary.PaperSet
            ?? await _paperSetRepository.GetByIdAsync(submission.GradingDiary.PaperSetId, ct);

        if (paperSet is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy đề thi để lấy rubric.", "PAPER_SET_NOT_FOUND");
        }

        var rubricText = await LoadRubricTextAsync(paperSet, ct);

        // 4. Gọi Gemini chấm bài
        var gradeResult = await _geminiService.GradeSubmissionAsync(studentText, rubricText, ct);

        // 5. Cập nhật AI score & AI logs, chuyển status sang AI_Graded (1). KHÔNG ghi đè comment của GV và không đổi UpdatedDate!
        submission.AiScore = gradeResult.TotalScore;
        submission.AiLogs = gradeResult.RawAiLogJson;
        submission.Status = SubmissionStatus.AI_Graded; // 1 = AI_Graded

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public Task<Result<SubmissionDetailDTO>> TriggerAiGradingAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return TriggerAiGradingAsync(Guid.Empty, submissionId, currentUserId, isElevatedRole, ct);
    }

    // FLOW 3: Lecturer Review
    public async Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid diaryId,
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        // TODO: Lấy max từ PaperSet.MaxScore khi schema được bổ sung
        if (lecturerScore < 0 || lecturerScore > 10)
        {
            return Result<SubmissionDetailDTO>.Failure("Điểm số phải nằm trong thang điểm từ 0 đến 10.", "INVALID_SCORE");
        }

        var submission = await _dbContext.Submissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && (diaryId == Guid.Empty || s.DiaryId == diaryId), ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền chấm bài nộp này.", "FORBIDDEN");
        }

        // Cập nhật điểm giảng viên và nhận xét (chuyển sang trạng thái Lecturer_Reviewed)
        // KHÔNG sửa ai_score hay ai_logs!
        submission.LecturerScore = lecturerScore;
        submission.Comment = comment?.Trim() ?? string.Empty;
        submission.Status = SubmissionStatus.Lecturer_Reviewed; // 2 = Lecturer_Reviewed
        submission.UpdatedDate = DateTime.UtcNow;

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(Guid.Empty, submissionId, lecturerScore, comment, currentUserId, isElevatedRole, ct);
    }

    public Task<Result<SubmissionDetailDTO>> DecideScoreAsync(
        Guid submissionId,
        decimal lecturerScore,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(Guid.Empty, submissionId, lecturerScore, null, currentUserId, isElevatedRole, ct);
    }

    // FLOW 4: Finalize
    public async Task<Result<SubmissionDetailDTO>> FinalizeAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var query = _dbContext.Submissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .AsQueryable();

        if (diaryId != Guid.Empty)
        {
            query = query.Where(s => s.DiaryId == diaryId);
        }

        var submission = await query.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền chốt điểm cho bài nộp này.", "FORBIDDEN");
        }

        if (submission.Status != SubmissionStatus.Lecturer_Reviewed && !submission.LecturerScore.HasValue)
        {
            return Result<SubmissionDetailDTO>.Failure("Bài nộp phải được giảng viên review trước khi chốt điểm.", "INVALID_STATUS");
        }

        // Chuyển sang trạng thái Final (3). KHÔNG cập nhật lại điểm số và giữ nguyên ngày review (UpdatedDate).
        submission.Status = SubmissionStatus.Final; // 3 = Final

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public Task<Result<SubmissionDetailDTO>> FinalizeAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return FinalizeAsync(Guid.Empty, submissionId, currentUserId, isElevatedRole, ct);
    }

    public Task<Result<SubmissionDetailDTO>> FinalizeScoreAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return FinalizeAsync(Guid.Empty, submissionId, currentUserId, isElevatedRole, ct);
    }

    private async Task<string> LoadRubricTextAsync(PaperSet paperSet, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(paperSet.FileAnswerRubric))
        {
            try
            {
                var download = await _storage.DownloadAsync(paperSet.FileAnswerRubric, "rubric", ct);
                var extension = Path.GetExtension(paperSet.FileAnswerRubric);

                if (string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
                {
                    var text = await _geminiService.ExtractTextFromDocxAsync(download.Content, ct);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
                else
                {
                    using var reader = new StreamReader(download.Content);
                    var text = await reader.ReadToEndAsync(ct);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể tải hoặc đọc rubric từ storage: {Path}", paperSet.FileAnswerRubric);
            }
        }

        // Fallback: Xây dựng rubric từ danh sách câu hỏi của đề thi
        if (paperSet.Questions.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ĐỀ THI: {paperSet.PaperSetCode} - {paperSet.Description}]");
            sb.AppendLine("DANH SÁCH CÂU HỎI VÀ THANG ĐIỂM:");
            foreach (var q in paperSet.Questions)
            {
                sb.AppendLine($"- {q.Title}: {q.Content} (Điểm tối đa: {q.Point}đ)");
            }
            return sb.ToString();
        }

        return $"Đề thi mã {paperSet.PaperSetCode}. Hãy chấm điểm bài làm tổng thể theo thang điểm 10 dựa trên độ chính xác, đầy đủ và tính chuyên nghiệp.";
    }

    private async Task<Result<StudentExamination>> ResolveStudentExaminationAsync(string filename, Guid examId, CancellationToken ct)
    {
        // Trích xuất mã sinh viên dạng SE123456, HE123456, QE123456 từ tên file
        var match = StudentCodeRegex().Match(filename);
        if (!match.Success)
        {
            return Result<StudentExamination>.Failure("Tên file không chứa mã sinh viên hợp lệ.", "INVALID_FILENAME");
        }

        var studentCode = match.Value.ToUpperInvariant();
        var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.StundentCode == studentCode, ct);

        if (student is null)
        {
            return Result<StudentExamination>.Failure($"Không tìm thấy sinh viên với mã {studentCode}.", "STUDENT_NOT_FOUND");
        }

        var studentExam = await _dbContext.StudentExaminations
            .FirstOrDefaultAsync(se => se.StudentId == student.Id && se.ExamId == examId, ct);

        if (studentExam is null)
        {
            studentExam = new StudentExamination
            {
                StudentExaminationId = Guid.NewGuid(),
                StudentId = student.Id,
                ExamId = examId
            };
            _dbContext.StudentExaminations.Add(studentExam);
            await _dbContext.SaveChangesAsync(ct);
        }

        return Result<StudentExamination>.Success(studentExam);
    }

    private static SubmissionDetailDTO ToDetailDto(Submission submission)
    {
        var criteriaScores = ParseCriteriaScores(submission.AiLogs);

        return new SubmissionDetailDTO
        {
            SubmissionId = submission.SubmissionId,
            SubmissionFile = submission.SubmissionFile,
            FilePath = submission.FilePath,
            DiaryId = submission.DiaryId,
            DiaryName = submission.GradingDiary?.Name ?? string.Empty,
            StudentCode = submission.StudentExamination?.Student?.StundentCode ?? string.Empty,
            StudentName = submission.StudentExamination?.Student?.FullName ?? string.Empty,
            AiScore = submission.AiScore,
            AiLogs = submission.AiLogs,
            LecturerScore = submission.LecturerScore,
            Comment = submission.Comment ?? string.Empty,
            Status = submission.Status,
            CriteriaScores = criteriaScores,
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate
        };
    }

    private static List<CriterionScoreDto> ParseCriteriaScores(string? aiLogsJson)
    {
        var criteriaScores = new List<CriterionScoreDto>();
        if (!string.IsNullOrWhiteSpace(aiLogsJson))
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var aiResult = System.Text.Json.JsonSerializer.Deserialize<GradingResultDto>(aiLogsJson, options);
                if (aiResult?.CriteriaScores is not null)
                {
                    criteriaScores.AddRange(aiResult.CriteriaScores);
                }
            }
            catch { }
        }
        return criteriaScores;
    }

    private static string NormalizeStorageKeySegment(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "default_exam";

        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        var clean = stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        clean = clean.Replace("đ", "d").Replace("Đ", "D");
        clean = Regex.Replace(clean, @"[^\w\-\.]", "_");
        clean = Regex.Replace(clean, @"_+", "_").Trim('_');

        return string.IsNullOrWhiteSpace(clean) ? "default_exam" : clean;
    }

    [GeneratedRegex(@"[A-Za-z]{2}\d{5,8}")]
    private static partial Regex StudentCodeRegex();
}
