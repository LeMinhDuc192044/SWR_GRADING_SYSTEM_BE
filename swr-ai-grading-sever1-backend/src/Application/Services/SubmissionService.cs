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

    // FLOW 1: Upload Submission
    public async Task<Result<UploadSubmissionsResultDTO>> UploadSubmissionsAsync(
        Guid diaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
            return Result<UploadSubmissionsResultDTO>.Failure("Vui lòng chọn ít nhất một file bài làm (.docx hoặc .rar).", "FILES_REQUIRED");

        var diary = await _diaryRepository.GetDetailByIdAsync(diaryId, ct);
        if (diary is null)
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy sổ chấm thi.", "DIARY_NOT_FOUND");

        var paperSet = diary.PaperSet ?? await _paperSetRepository.GetByIdAsync(diary.PaperSetId, ct);
        if (paperSet is null)
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy thông tin đề thi liên kết.", "PAPER_SET_NOT_FOUND");

        if (!paperSet.ExaminationId.HasValue || paperSet.ExaminationId.Value == Guid.Empty)
            return Result<UploadSubmissionsResultDTO>.Failure("PaperSet chưa liên kết với kỳ thi.", "PAPER_SET_NOT_LINKED_TO_EXAM");

        var examId = paperSet.ExaminationId.Value;
        var paperQuestions = await _dbContext.Questions
            .AsNoTracking()
            .Where(question => question.PaperSetId == paperSet.PaperSetId)
            .OrderBy(question => question.Title)
            .ToListAsync(ct);

        var paperSetCode = !string.IsNullOrWhiteSpace(paperSet.PaperSetCode) ? paperSet.PaperSetCode.Trim() : "UNKNOWN_PAPERSET";
        var safePaperSetCode = NormalizeStorageKeySegment(paperSetCode);
        var results = new List<UploadedSubmissionSummaryDTO>();
        var successCount = 0;

        foreach (var file in files)
        {
            var summary = new UploadedSubmissionSummaryDTO { SubmissionFile = file.FileName };
            var extension = Path.GetExtension(file.FileName);
            var isRar = string.Equals(extension, ".rar", StringComparison.OrdinalIgnoreCase);
            if (!isRar && !string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            {
                summary.IsSuccess = false;
                summary.ErrorMessage = "Hệ thống chỉ hỗ trợ file Word (.docx) hoặc archive RAR (.rar).";
                results.Add(summary);
                continue;
            }

            try
            {
                var studentExamResult = await ResolveStudentExaminationAsync(file.FileName, examId, ct);
                if (!studentExamResult.IsSuccess)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = studentExamResult.Error;
                    results.Add(summary);
                    continue;
                }

                var studentExam = studentExamResult.Data!;
                if (file.Length > SubmissionArchive.MaxArchiveBytes)
                    throw new InvalidDataException("File bài làm vượt quá giới hạn dung lượng cho phép.");

                using var uploadStream = new MemoryStream();
                await file.CopyToAsync(uploadStream, ct);
                uploadStream.Position = 0;
                var submissionContent = isRar
                    ? SubmissionArchive.Read(uploadStream)
                    : new SubmissionArchive.Content(uploadStream.ToArray(), null);

                using var wordStream = new MemoryStream(submissionContent.WordDocument);
                var extractedContent = await _geminiService.ExtractContentFromDocxAsync(wordStream, ct);
                var studentText = extractedContent.Text;
                if (string.IsNullOrWhiteSpace(studentText) && extractedContent.Images.Count == 0)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = "Không thể trích xuất văn bản hoặc hình ảnh từ bài làm.";
                    results.Add(summary);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(studentText))
                    studentText = "[Tài liệu bài làm chứa các hình ảnh/sơ đồ đính kèm]";
                if (!string.IsNullOrWhiteSpace(submissionContent.DiagramSource))
                    studentText += $"\n\nNội dung file sơ đồ trong archive:\n{submissionContent.DiagramSource}";

                var extractedAnswers = await _geminiService.ExtractAnswersForQuestionsAsync(
                    studentText, paperQuestions, extractedContent.Images, ct);
                var validQuestionIds = paperQuestions.Select(question => question.QuestionId).ToHashSet();
                var answerTextByQuestion = extractedAnswers
                    .Where(answer => validQuestionIds.Contains(answer.QuestionId))
                    .GroupBy(answer => answer.QuestionId)
                    .ToDictionary(group => group.Key, group => group.First().ExtractedText);
                var submissionId = Guid.NewGuid();
                var createdAt = DateTime.UtcNow;
                var parsedAnswers = paperQuestions.Select(question => new SubmissionParsedAnswer
                {
                    ParsedAnswerId = Guid.NewGuid(),
                    SubmissionId = submissionId,
                    QuestionId = question.QuestionId,
                    ExtractedText = answerTextByQuestion.GetValueOrDefault(question.QuestionId, string.Empty),
                    CreatedAt = createdAt
                }).ToList();

                var storagePath = $"submissions/{safePaperSetCode}/{file.FileName}";
                uploadStream.Position = 0;
                await _storage.UploadAsync(storagePath, uploadStream,
                    file.ContentType ?? (isRar ? "application/vnd.rar" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document"), ct);

                var submission = new StudentSubmission
                {
                    SubmissionId = submissionId,
                    FileSubmissions = storagePath,
                    StudentId = studentExam.StudentId,
                    Status = SubmissionStatus.Submitted,
                    DiaryId = diaryId,
                    StudentExaminationId = studentExam.StudentExaminationId,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    ParsedAnswers = parsedAnswers
                };

                await _submissionRepository.AddAsync(submission, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                summary.SubmissionId = submission.SubmissionId;
                summary.FileSubmissions = submission.FileSubmissions;
                summary.StudentId = submission.StudentId;
                summary.DiaryId = submission.DiaryId;
                summary.StudentExaminationId = submission.StudentExaminationId;
                summary.IsSuccess = true;
                summary.Status = submission.Status.ToString();
                successCount++;
            }
            catch (Exception ex)
            {
                summary.IsSuccess = false;
                summary.ErrorMessage = $"Lỗi xử lý: {ex.Message}";
            }

            results.Add(summary);
        }

        return Result<UploadSubmissionsResultDTO>.Success(new UploadSubmissionsResultDTO
        {
            GradingDiaryId = diaryId,
            TotalUploaded = files.Count,
            TotalSucceeded = successCount,
            Submissions = results
        });
    }

    // List submissions under a diary

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
            .Include(s => s.Evaluations)
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
            .Include(s => s.Evaluations)
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
            .Include(s => s.Evaluations)
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

    // FLOW 2: Lecturer Review
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
            .Include(s => s.Evaluations)
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
        AddEvaluation(submission, EvaluationType.Lecturer, lecturerScore, comment?.Trim());
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

    // FLOW 3: Finalize
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
            .Include(s => s.Evaluations)
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

        var lecturerEvaluation = GetLatestEvaluation(submission, EvaluationType.Lecturer);
        if (submission.Status != SubmissionStatus.Lecturer_Reviewed || lecturerEvaluation?.TotalScore.HasValue != true)
        {
            return Result<SubmissionDetailDTO>.Failure("Bài nộp phải được giảng viên review trước khi chốt điểm.", "INVALID_STATUS");
        }

        // Chuyển sang trạng thái Final (3). KHÔNG cập nhật lại điểm số và giữ nguyên ngày review (UpdatedDate).
        lecturerEvaluation!.IsFinalScore = true;
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

    private async Task<Result<StudentExamination>> ResolveStudentExaminationAsync(string filename, Guid examId, CancellationToken ct)
    {
        // Trích xuất mã sinh viên dạng SE123456, HE123456, QE123456 từ tên file
        var match = StudentCodeRegex().Match(filename);
        if (!match.Success)
        {
            return Result<StudentExamination>.Failure("Tên file không chứa mã sinh viên.", "INVALID_FILENAME");
        }

        var studentCode = match.Value.ToUpperInvariant();
        var student = await _dbContext.Students.FirstOrDefaultAsync(s => s.StudentCode == studentCode, ct);

        if (student is null)
        {
            return Result<StudentExamination>.Failure($"Không tìm thấy sinh viên với mã {studentCode}.", "STUDENT_NOT_FOUND");
        }

        var studentExam = await _dbContext.StudentExaminations
            .FirstOrDefaultAsync(se => se.StudentId == student.Id && se.ExamId == examId, ct);

        if (studentExam is null)
        {
            var now = DateTime.UtcNow;
            studentExam = new StudentExamination
            {
                StudentExaminationId = Guid.NewGuid(),
                StudentId = student.Id,
                ExamId = examId,
                CreatedDay = now,
                UpdatedDay = now
            };
            _dbContext.StudentExaminations.Add(studentExam);
            await _dbContext.SaveChangesAsync(ct);
        }
        else if (studentExam.CreatedDay == DateTime.MinValue || studentExam.UpdatedDay == DateTime.MinValue)
        {
            var now = DateTime.UtcNow;
            if (studentExam.CreatedDay == DateTime.MinValue)
                studentExam.CreatedDay = now;
            if (studentExam.UpdatedDay == DateTime.MinValue)
                studentExam.UpdatedDay = now;
            await _dbContext.SaveChangesAsync(ct);
        }

        return Result<StudentExamination>.Success(studentExam);
    }

    private static SubmissionDetailDTO ToDetailDto(StudentSubmission submission)
    {
        var aiEvaluation = GetLatestEvaluation(submission, EvaluationType.AI);
        var lecturerEvaluation = GetLatestEvaluation(submission, EvaluationType.Lecturer);
        var criteriaScores = ParseCriteriaScores(aiEvaluation?.GeneralFeedback);

        return new SubmissionDetailDTO
        {
            SubmissionId = submission.SubmissionId,
            FileSubmissions = submission.FileSubmissions,
            SubmissionFile = Path.GetFileName(submission.FileSubmissions),
            FilePath = submission.FileSubmissions,
            StudentId = submission.StudentId,
            DiaryId = submission.DiaryId,
            StudentExaminationId = submission.StudentExaminationId,
            DiaryName = submission.GradingDiary?.Name ?? string.Empty,
            StudentCode = submission.StudentExamination?.Student?.StudentCode ?? string.Empty,
            StudentName = submission.StudentExamination?.Student?.FullName ?? string.Empty,
            AiScore = aiEvaluation?.TotalScore,
            AiLogs = aiEvaluation?.GeneralFeedback,
            LecturerScore = lecturerEvaluation?.TotalScore,
            Comment = lecturerEvaluation?.GeneralFeedback ?? string.Empty,
            Status = submission.Status,
            CriteriaScores = criteriaScores,
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate
        };
    }

    private static SubmissionEvaluation? GetLatestEvaluation(StudentSubmission submission, EvaluationType evaluationType)
    {
        return submission.Evaluations
            .Where(evaluation => evaluation.EvaluationType == evaluationType)
            .OrderByDescending(evaluation => evaluation.EvaluationRound)
            .ThenByDescending(evaluation => evaluation.CreatedAt)
            .FirstOrDefault();
    }

    private static void AddEvaluation(
        StudentSubmission submission,
        EvaluationType evaluationType,
        decimal? score,
        string? feedback)
    {
        var nextRound = submission.Evaluations
            .Where(evaluation => evaluation.EvaluationType == evaluationType)
            .Select(evaluation => evaluation.EvaluationRound)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        submission.Evaluations.Add(new SubmissionEvaluation
        {
            EvaluationType = evaluationType,
            EvaluationRound = nextRound,
            TotalScore = score,
            GeneralFeedback = feedback,
            CreatedAt = DateTime.UtcNow
        });
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

    /// <summary>
    /// Xem chi tiết lịch sử và dòng thời gian xử lý của một bài thi.
    /// </summary>
    public async Task<Result<Application.DTOs.Submissions.SubmissionHistoryDto>> GetHistoryAsync(
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.Evaluations)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        if (submission is null)
        {
            return Result<Application.DTOs.Submissions.SubmissionHistoryDto>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary?.CreateById != currentUserId)
        {
            return Result<Application.DTOs.Submissions.SubmissionHistoryDto>.Failure("Bạn không có quyền truy cập lịch sử bài nộp này.", "FORBIDDEN");
        }

        var timeline = new List<Application.DTOs.Submissions.SubmissionHistoryTimelineEventDto>();

        // 1. Mốc nộp bài
        timeline.Add(new Application.DTOs.Submissions.SubmissionHistoryTimelineEventDto
        {
            Stage = "Submitted",
            Title = "Sinh viên nộp bài",
            Description = $"Bài làm '{Path.GetFileName(submission.FileSubmissions)}' đã được tải lên hệ thống lưu trữ.",
            Timestamp = submission.CreatedDate,
            IsCompleted = true
        });

        // 2. Mốc AI chấm
        var aiEvaluation = GetLatestEvaluation(submission, EvaluationType.AI);
        var lecturerEvaluation = GetLatestEvaluation(submission, EvaluationType.Lecturer);
        var hasAiGraded = submission.Status >= SubmissionStatus.AI_Graded && aiEvaluation?.TotalScore.HasValue == true;
        timeline.Add(new Application.DTOs.Submissions.SubmissionHistoryTimelineEventDto
        {
            Stage = "AI_Graded",
            Title = "Trợ lý AI chấm bài",
            Description = hasAiGraded
                ? $"AI đã chấm và gợi ý mức điểm: {aiEvaluation!.TotalScore:0.00}/10."
                : "Chưa kích hoạt hoặc chưa hoàn tất chấm AI.",
            Timestamp = hasAiGraded ? submission.CreatedDate : null,
            IsCompleted = hasAiGraded
        });

        // 3. Mốc Giảng viên Review
        var hasReviewed = submission.Status >= SubmissionStatus.Lecturer_Reviewed && lecturerEvaluation?.TotalScore.HasValue == true;
        timeline.Add(new Application.DTOs.Submissions.SubmissionHistoryTimelineEventDto
        {
            Stage = "Lecturer_Reviewed",
            Title = "Giảng viên đánh giá & chấm điểm",
            Description = hasReviewed
                ? $"Giảng viên đã chấm {lecturerEvaluation!.TotalScore:0.00}/10. Nhận xét: {(string.IsNullOrWhiteSpace(lecturerEvaluation.GeneralFeedback) ? "Không có" : lecturerEvaluation.GeneralFeedback)}"
                : "Giảng viên chưa review bài làm.",
            Timestamp = hasReviewed ? submission.UpdatedDate : null,
            IsCompleted = hasReviewed
        });

        // 4. Mốc Chốt điểm Final
        var isFinal = submission.Status == SubmissionStatus.Final;
        timeline.Add(new Application.DTOs.Submissions.SubmissionHistoryTimelineEventDto
        {
            Stage = "Final",
            Title = "Chốt điểm chính thức",
            Description = isFinal
                ? $"Điểm số chính thức của bài thi là: {lecturerEvaluation?.TotalScore:0.00}/10."
                : "Bài thi chưa được chốt điểm chính thức.",
            Timestamp = isFinal ? submission.UpdatedDate : null,
            IsCompleted = isFinal
        });

        var historyDto = new Application.DTOs.Submissions.SubmissionHistoryDto
        {
            SubmissionId = submission.SubmissionId,
            SubmissionFile = Path.GetFileName(submission.FileSubmissions),
            StudentCode = submission.StudentExamination?.Student?.StudentCode ?? string.Empty,
            StudentName = submission.StudentExamination?.Student?.FullName ?? string.Empty,
            DiaryName = submission.GradingDiary?.Name ?? string.Empty,
            CurrentStatus = submission.Status,
            AiScore = aiEvaluation?.TotalScore,
            LecturerScore = lecturerEvaluation?.TotalScore,
            LecturerComment = lecturerEvaluation?.GeneralFeedback,
            Timeline = timeline
        };

        return Result<Application.DTOs.Submissions.SubmissionHistoryDto>.Success(historyDto);
    }

    [GeneratedRegex(@"[A-Za-z]{2}\d{5,8}")]
    private static partial Regex StudentCodeRegex();
}
