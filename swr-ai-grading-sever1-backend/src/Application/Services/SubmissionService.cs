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
                    : new SubmissionArchive.Content(uploadStream.ToArray(), new Dictionary<string, string>());

                using var wordStream = new MemoryStream(submissionContent.WordDocument);
                var extractedContent = await _geminiService.ExtractContentFromDocxAsync(wordStream, ct);
                var diagramAnswers = new Dictionary<Guid, string>();
                foreach (var question in paperQuestions)
                {
                    var diagramKey = SubmissionArchive.GetQuestionKey(question.Title);
                    if (submissionContent.DiagramSources.TryGetValue(diagramKey, out var diagramText))
                        diagramAnswers[question.QuestionId] = diagramText;
                }

                var hasWordContent = !string.IsNullOrWhiteSpace(extractedContent.Text) || extractedContent.Images.Count > 0;
                if (!hasWordContent && diagramAnswers.Count == 0)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = "Không thể trích xuất văn bản hoặc hình ảnh từ bài làm.";
                    results.Add(summary);
                    continue;
                }

                var studentText = extractedContent.Text;
                if (string.IsNullOrWhiteSpace(studentText))
                    studentText = "[Tài liệu bài làm chứa các hình ảnh/sơ đồ đính kèm]";

                IReadOnlyList<ExtractedQuestionAnswerDto> extractedAnswers = hasWordContent
                    ? await _geminiService.ExtractAnswersForQuestionsAsync(studentText, paperQuestions, extractedContent.Images, ct)
                    : [];
                var submissionId = Guid.NewGuid();
                var validQuestionIds = paperQuestions.Select(question => question.QuestionId).ToHashSet();
                var answersByQuestion = extractedAnswers
                    .Where(answer => validQuestionIds.Contains(answer.QuestionId))
                    .GroupBy(answer => answer.QuestionId)
                    .ToDictionary(group => group.Key, group => group.First());
                var answerTextByQuestion = answersByQuestion
                    .ToDictionary(pair => pair.Key, pair => pair.Value.ExtractedText);
                foreach (var (questionId, diagramText) in diagramAnswers)
                    answerTextByQuestion[questionId] = diagramText;

                var imagePaths = new Dictionary<int, string>();
                for (var imageIndex = 0; imageIndex < extractedContent.Images.Count; imageIndex++)
                {
                    var image = extractedContent.Images[imageIndex];
                    if (string.IsNullOrWhiteSpace(image.Base64Data))
                        continue;

                    var imageNumber = imageIndex + 1;
                    var imageExtension = image.MimeType switch
                    {
                        "image/jpeg" => ".jpg",
                        "image/gif" => ".gif",
                        "image/webp" => ".webp",
                        _ => ".png"
                    };
                    var imagePath = $"submissions/{safePaperSetCode}/{submissionId}/images/image-{imageNumber}{imageExtension}";
                    await using var imageStream = new MemoryStream(Convert.FromBase64String(image.Base64Data));
                    await _storage.UploadAsync(imagePath, imageStream, image.MimeType, ct);
                    imagePaths[imageNumber] = imagePath;
                }

                var imagePathByQuestion = answersByQuestion
                    .Where(pair => pair.Value.ImageIndexes.Count > 0)
                    .Select(pair => new
                    {
                        pair.Key,
                        ImagePath = pair.Value.ImageIndexes
                            .Where(imagePaths.ContainsKey)
                            .Select(imageIndex => imagePaths[imageIndex])
                            .FirstOrDefault()
                    })
                    .Where(pair => pair.ImagePath is not null)
                    .ToDictionary(pair => pair.Key, pair => pair.ImagePath!);

                var createdAt = DateTime.UtcNow;
                var parsedAnswers = paperQuestions.Select(question => new SubmissionParsedAnswer
                {
                    ParsedAnswerId = Guid.NewGuid(),
                    SubmissionId = submissionId,
                    QuestionId = question.QuestionId,
                    ExtractedText = answerTextByQuestion.GetValueOrDefault(question.QuestionId, string.Empty),
                    ExtractedImageUrl = imagePathByQuestion.GetValueOrDefault(question.QuestionId),
                    CreatedAt = createdAt
                }).ToList();

                var safeFileBaseName = NormalizeStorageKeySegment(Path.GetFileNameWithoutExtension(file.FileName));
                var fileExt = Path.GetExtension(file.FileName);
                var storagePath = $"submissions/{safePaperSetCode}/{Guid.NewGuid():N}_{safeFileBaseName}{fileExt}";
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
            .Include(s => s.ParsedAnswers)
                .ThenInclude(answer => answer.Question)
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
            .Include(s => s.ParsedAnswers)
                .ThenInclude(answer => answer.Question)
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
            .Include(s => s.ParsedAnswers)
                .ThenInclude(answer => answer.Question)
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
        var submission = await _dbContext.StudentSubmissions
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.PaperSet)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.Evaluations)
                .ThenInclude(e => e.Details)
            .Include(s => s.ParsedAnswers)
                .ThenInclude(pa => pa.Question)
                    .ThenInclude(q => q.Rubric)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && (diaryId == Guid.Empty || s.DiaryId == diaryId), ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền kích hoạt chấm AI cho bài nộp này.", "FORBIDDEN");
        }

        var paperSet = submission.GradingDiary.PaperSet
            ?? await _paperSetRepository.GetByIdAsync(submission.GradingDiary.PaperSetId, ct);

        if (paperSet is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy đề thi để lấy rubric.", "PAPER_SET_NOT_FOUND");
        }

        // Tải nội dung Rubric
        var rubricText = await LoadRubricTextAsync(paperSet, ct);

        // Chuẩn bị danh sách (Question, ParsedAnswer)
        var questionAnswers = submission.ParsedAnswers
            .Where(pa => pa.Question is not null)
            .Select(pa => (Question: pa.Question!, ParsedAnswer: pa))
            .ToList();

        if (questionAnswers.Count == 0)
        {
            return Result<SubmissionDetailDTO>.Failure("Bài nộp chưa có câu trả lời nào được trích xuất để chấm.", "NO_PARSED_ANSWERS");
        }

        // Tải các hình ảnh nếu có đính kèm
        var docxImages = new List<DocxImageDto>();
        foreach (var qa in questionAnswers)
        {
            if (!string.IsNullOrWhiteSpace(qa.ParsedAnswer.ExtractedImageUrl))
            {
                try
                {
                    var imgDownload = await _storage.DownloadAsync(qa.ParsedAnswer.ExtractedImageUrl, "submissions", ct);
                    using var ms = new MemoryStream();
                    await imgDownload.Content.CopyToAsync(ms, ct);
                    var imgBytes = ms.ToArray();
                    if (imgBytes.Length > 0)
                    {
                        var ext = Path.GetExtension(qa.ParsedAnswer.ExtractedImageUrl).ToLowerInvariant();
                        var mime = ext switch
                        {
                            ".jpg" or ".jpeg" => "image/jpeg",
                            ".webp" => "image/webp",
                            ".gif" => "image/gif",
                            _ => "image/png"
                        };
                        docxImages.Add(new DocxImageDto
                        {
                            MimeType = mime,
                            Base64Data = Convert.ToBase64String(imgBytes)
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể tải hình ảnh đính kèm của câu {Title}: {Url}", qa.Question.Title, qa.ParsedAnswer.ExtractedImageUrl);
                }
            }
        }

        // Gọi Gemini AI chấm bài
        var gradingResult = await _geminiService.GradeSubmissionAnswersAsync(questionAnswers, rubricText, docxImages, ct);

        // Tính vòng lặp tiếp theo cho AI
        var nextRound = submission.Evaluations
            .Where(e => e.EvaluationType == EvaluationType.AI)
            .Select(e => e.EvaluationRound)
            .DefaultIfEmpty(-1)
            .Max() + 1;

        var aiEvaluation = new SubmissionEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.AI,
            EvaluationRound = nextRound,
            TotalScore = gradingResult.TotalScore,
            GeneralFeedback = gradingResult.RawAiLogJson,
            IsFinalScore = false,
            CreatedAt = DateTime.UtcNow
        };

        // Thêm Evaluation và EvaluationItemDetail trực tiếp vào DbContext
        await _dbContext.SubmissionEvaluations.AddAsync(aiEvaluation, ct);

        foreach (var qa in questionAnswers)
        {
            var matchedCrit = gradingResult.CriteriaScores.FirstOrDefault(c =>
                c.QuestionId == qa.Question.QuestionId ||
                (!string.IsNullOrWhiteSpace(c.Criterion) && c.Criterion.Contains(qa.Question.Title, StringComparison.OrdinalIgnoreCase)));

            var itemDetail = new EvaluationItemDetail
            {
                DetailId = Guid.NewGuid(),
                EvaluationId = aiEvaluation.EvaluationId,
                ParsedAnswerId = qa.ParsedAnswer.ParsedAnswerId,
                Score = matchedCrit?.ActualScore ?? 0m,
                Comment = matchedCrit?.Comment ?? "Chưa có nhận xét chi tiết từ AI."
            };

            await _dbContext.EvaluationItemDetails.AddAsync(itemDetail, ct);
        }

        submission.Evaluations.Add(aiEvaluation);
        submission.Status = SubmissionStatus.AI_Graded;
        submission.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

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

    // FLOW 3: Lecturer Review & Pick AI Score
    public async Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid diaryId,
        Guid submissionId,
        ReviewSubmissionRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.StudentSubmissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.Evaluations)
                .ThenInclude(e => e.Details)
            .Include(s => s.ParsedAnswers)
                .ThenInclude(pa => pa.Question)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId && (diaryId == Guid.Empty || s.DiaryId == diaryId), ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền chấm bài nộp này.", "FORBIDDEN");
        }

        var aiEvaluation = GetLatestEvaluation(submission, EvaluationType.AI);

        if (request.PickAiScore)
        {
            if (aiEvaluation is null || !aiEvaluation.TotalScore.HasValue)
            {
                return Result<SubmissionDetailDTO>.Failure("Bài làm chưa được AI chấm nên không thể pick điểm AI.", "AI_EVALUATION_NOT_FOUND");
            }

            // Người chấm chọn pick điểm AI:
            // Tạo hoặc cập nhật bản ghi Review của Giảng viên với điểm và nhận xét kế thừa từ AI
            var chosenFeedback = !string.IsNullOrWhiteSpace(request.Comment)
                ? request.Comment.Trim()
                : $"Giảng viên đồng ý với điểm và nhận xét của AI.";

            if (request.IsFinal)
            {
                // Reset tất cả IsFinalScore trước đó của bài làm
                foreach (var eval in submission.Evaluations)
                {
                    eval.IsFinalScore = false;
                }
            }

            var lecturerEval = submission.Evaluations
                .FirstOrDefault(e => e.EvaluationType == EvaluationType.Lecturer);

            if (lecturerEval == null)
            {
                lecturerEval = new SubmissionEvaluation
                {
                    EvaluationId = Guid.NewGuid(),
                    SubmissionId = submission.SubmissionId,
                    EvaluationType = EvaluationType.Lecturer,
                    EvaluationRound = 1,
                    TotalScore = aiEvaluation.TotalScore.Value,
                    GeneralFeedback = chosenFeedback,
                    IsFinalScore = request.IsFinal,
                    AdminSelectedId = currentUserId,
                    SelectionReason = !string.IsNullOrWhiteSpace(request.SelectionReason) 
                        ? request.SelectionReason.Trim() 
                        : "Giảng viên xác nhận pick điểm của AI",
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.SubmissionEvaluations.AddAsync(lecturerEval, ct);
                submission.Evaluations.Add(lecturerEval);

                // Sao chép chi tiết từng câu từ AI sang Lecturer, ghi nhận thêm override nếu có
                foreach (var aiDetail in aiEvaluation.Details)
                {
                    var pa = submission.ParsedAnswers.FirstOrDefault(p => p.ParsedAnswerId == aiDetail.ParsedAnswerId);
                    var overrideItem = request.ItemDetails?.FirstOrDefault(i =>
                        (pa is not null && i.QuestionId == pa.QuestionId));

                    var lecturerDetail = new EvaluationItemDetail
                    {
                        DetailId = Guid.NewGuid(),
                        EvaluationId = lecturerEval.EvaluationId,
                        ParsedAnswerId = aiDetail.ParsedAnswerId,
                        Score = overrideItem is not null ? overrideItem.Score : aiDetail.Score,
                        Comment = overrideItem is not null && !string.IsNullOrWhiteSpace(overrideItem.Comment) ? overrideItem.Comment : aiDetail.Comment,
                        AiExtractionId = aiDetail.DetailId
                    };

                    await _dbContext.EvaluationItemDetails.AddAsync(lecturerDetail, ct);
                    lecturerEval.Details.Add(lecturerDetail);
                }
            }
            else
            {
                lecturerEval.TotalScore = aiEvaluation.TotalScore.Value;
                lecturerEval.GeneralFeedback = chosenFeedback;
                lecturerEval.IsFinalScore = request.IsFinal;
                lecturerEval.AdminSelectedId = currentUserId;
                lecturerEval.SelectionReason = !string.IsNullOrWhiteSpace(request.SelectionReason) 
                    ? request.SelectionReason.Trim() 
                    : "Giảng viên xác nhận pick điểm của AI";

                foreach (var aiDetail in aiEvaluation.Details)
                {
                    var pa = submission.ParsedAnswers.FirstOrDefault(p => p.ParsedAnswerId == aiDetail.ParsedAnswerId);
                    var overrideItem = request.ItemDetails?.FirstOrDefault(i =>
                        pa is not null && i.QuestionId == pa.QuestionId);

                    var existingDetail = lecturerEval.Details.FirstOrDefault(d => d.ParsedAnswerId == aiDetail.ParsedAnswerId);
                    if (existingDetail != null)
                    {
                        existingDetail.Score = overrideItem is not null ? overrideItem.Score : aiDetail.Score;
                        existingDetail.Comment = overrideItem is not null && !string.IsNullOrWhiteSpace(overrideItem.Comment) ? overrideItem.Comment : aiDetail.Comment;
                        existingDetail.AiExtractionId = aiDetail.DetailId;
                    }
                    else
                    {
                        var newDetail = new EvaluationItemDetail
                        {
                            DetailId = Guid.NewGuid(),
                            EvaluationId = lecturerEval.EvaluationId,
                            ParsedAnswerId = aiDetail.ParsedAnswerId,
                            Score = overrideItem is not null ? overrideItem.Score : aiDetail.Score,
                            Comment = overrideItem is not null && !string.IsNullOrWhiteSpace(overrideItem.Comment) ? overrideItem.Comment : aiDetail.Comment,
                            AiExtractionId = aiDetail.DetailId
                        };
                        await _dbContext.EvaluationItemDetails.AddAsync(newDetail, ct);
                        lecturerEval.Details.Add(newDetail);
                    }
                }
            }
            submission.Status = request.IsFinal ? SubmissionStatus.Final : SubmissionStatus.Lecturer_Reviewed;
            submission.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
        }
        else
        {
            if (!request.LecturerScore.HasValue)
            {
                return Result<SubmissionDetailDTO>.Failure("Vui lòng cung cấp lecturerScore khi không pick điểm AI.", "SCORE_REQUIRED");
            }

            var score = request.LecturerScore.Value;
            if (score < 0 || score > 10)
            {
                return Result<SubmissionDetailDTO>.Failure("Điểm số phải nằm trong thang điểm từ 0 đến 10.", "INVALID_SCORE");
            }

            var lecturerEval = submission.Evaluations
                .FirstOrDefault(e => e.EvaluationType == EvaluationType.Lecturer);

            if (lecturerEval == null)
            {
                lecturerEval = new SubmissionEvaluation
                {
                    EvaluationId = Guid.NewGuid(),
                    SubmissionId = submission.SubmissionId,
                    EvaluationType = EvaluationType.Lecturer,
                    EvaluationRound = 1,
                    TotalScore = score,
                    GeneralFeedback = request.Comment?.Trim(),
                    IsFinalScore = request.IsFinal,
                    AdminSelectedId = currentUserId,
                    SelectionReason = request.SelectionReason?.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.SubmissionEvaluations.AddAsync(lecturerEval, ct);
                submission.Evaluations.Add(lecturerEval);

                // Chấm thủ công: tạo chi tiết cho từng câu hỏi dựa vào QuestionId
                foreach (var pa in submission.ParsedAnswers)
                {
                    var reviewItem = request.ItemDetails?.FirstOrDefault(i => i.QuestionId == pa.QuestionId);

                    var lecturerDetail = new EvaluationItemDetail
                    {
                        DetailId = Guid.NewGuid(),
                        EvaluationId = lecturerEval.EvaluationId,
                        ParsedAnswerId = pa.ParsedAnswerId,
                        Score = reviewItem?.Score ?? 0m,
                        Comment = reviewItem?.Comment,
                    };

                    await _dbContext.EvaluationItemDetails.AddAsync(lecturerDetail, ct);
                    lecturerEval.Details.Add(lecturerDetail);
                }
            }
            else
            {
                // Cập nhật lại evaluation của Lecturer đã tồn tại
                lecturerEval.TotalScore = score;
                lecturerEval.GeneralFeedback = request.Comment?.Trim();
                lecturerEval.IsFinalScore = request.IsFinal;
                lecturerEval.AdminSelectedId = currentUserId;
                lecturerEval.SelectionReason = request.SelectionReason?.Trim();

                if (request.ItemDetails != null && request.ItemDetails.Count > 0)
                {
                    foreach (var pa in submission.ParsedAnswers)
                    {
                        var reviewItem = request.ItemDetails.FirstOrDefault(i => i.QuestionId == pa.QuestionId);
                        if (reviewItem is null)
                        {
                            continue;
                        }

                        var existingDetail = lecturerEval.Details.FirstOrDefault(d => d.ParsedAnswerId == pa.ParsedAnswerId);
                        if (existingDetail != null)
                        {
                            existingDetail.Score = reviewItem.Score;
                            existingDetail.Comment = reviewItem.Comment?.Trim();
                        }
                        else
                        {
                            var newDetail = new EvaluationItemDetail
                            {
                                DetailId = Guid.NewGuid(),
                                EvaluationId = lecturerEval.EvaluationId,
                                ParsedAnswerId = pa.ParsedAnswerId,
                                Score = reviewItem.Score,
                                Comment = reviewItem.Comment?.Trim(),
                            };
                            await _dbContext.EvaluationItemDetails.AddAsync(newDetail, ct);
                            lecturerEval.Details.Add(newDetail);
                        }
                    }
                }
            }

            submission.Status = request.IsFinal ? SubmissionStatus.Final : SubmissionStatus.Lecturer_Reviewed;
            submission.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
        }
    }

    public Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid diaryId,
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(diaryId, submissionId, new ReviewSubmissionRequest
        {
            LecturerScore = lecturerScore,
            Comment = comment,
            PickAiScore = false,
            IsFinal = false
        }, currentUserId, isElevatedRole, ct);
    }

    public Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid submissionId,
        ReviewSubmissionRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(Guid.Empty, submissionId, request, currentUserId, isElevatedRole, ct);
    }

    public Task<Result<SubmissionDetailDTO>> ReviewAsync(
        Guid submissionId,
        decimal lecturerScore,
        string? comment,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(Guid.Empty, submissionId, new ReviewSubmissionRequest
        {
            LecturerScore = lecturerScore,
            Comment = comment,
            PickAiScore = false,
            IsFinal = false
        }, currentUserId, isElevatedRole, ct);
    }

    public async Task<Result<SubmissionDetailDTO>> UpdateItemDetailAsync(
        Guid detailId,
        UpdateEvaluationItemDetailRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var detail = await _dbContext.EvaluationItemDetails
            .Include(d => d.Evaluation)
            .FirstOrDefaultAsync(d => d.DetailId == detailId, ct);

        if (detail == null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy chi tiết chấm điểm.", "DETAIL_NOT_FOUND");
        }

        var submission = await _dbContext.StudentSubmissions
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.Evaluations)
                .ThenInclude(e => e.Details)
            .Include(s => s.ParsedAnswers)
                .ThenInclude(pa => pa.Question)
            .FirstOrDefaultAsync(s => s.SubmissionId == detail.Evaluation.SubmissionId, ct);

        if (submission == null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp liên kết.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền chỉnh sửa bài nộp này.", "FORBIDDEN");
        }

        if (request.Score < 0 || request.Score > 10)
        {
            return Result<SubmissionDetailDTO>.Failure("Điểm số phải nằm trong thang điểm từ 0 đến 10.", "INVALID_SCORE");
        }

        // Cập nhật chi tiết câu hỏi
        detail.Score = request.Score;
        detail.Comment = request.Comment?.Trim();


        // Tự động tính lại tổng điểm của evaluation nếu là evaluation của Lecturer
        var eval = detail.Evaluation;
        if (eval != null)
        {
            // Lấy tất cả details của evaluation này để sum lại điểm
            var allDetails = eval.Details.ToList();
            if (allDetails.Count > 0)
            {
                var newTotal = allDetails.Sum(d => d.DetailId == detailId ? request.Score : d.Score);
                eval.TotalScore = Math.Min(10m, Math.Round(newTotal, 2));
            }
        }

        submission.UpdatedDate = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public Task<Result<SubmissionDetailDTO>> DecideScoreAsync(
        Guid submissionId,
        decimal lecturerScore,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        return ReviewAsync(Guid.Empty, submissionId, new ReviewSubmissionRequest
        {
            LecturerScore = lecturerScore,
            PickAiScore = false,
            IsFinal = false
        }, currentUserId, isElevatedRole, ct);
    }

    // FLOW 4: Finalize
    public async Task<Result<SubmissionDetailDTO>> FinalizeAsync(
        Guid diaryId,
        Guid submissionId,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var query = _dbContext.StudentSubmissions
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

        // Đảm bảo chỉ có một evaluation được IsFinalScore
        foreach (var eval in submission.Evaluations)
        {
            eval.IsFinalScore = false;
        }

        lecturerEvaluation.IsFinalScore = true;
        lecturerEvaluation.AdminSelectedId = currentUserId;
        submission.Status = SubmissionStatus.Final; // 3 = Final
        submission.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

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
                else if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase) || string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase))
                {
                    var text = await _geminiService.ExtractTextFromXlsxAsync(download.Content, ct);
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

        // Fallback: Xây dựng rubric từ danh sách câu hỏi và QuestionRubrics của đề thi
        var questions = await _dbContext.Questions
            .Include(q => q.Rubric)
            .Where(q => q.PaperSetId == paperSet.PaperSetId)
            .ToListAsync(ct);

        if (questions.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[ĐỀ THI: {paperSet.PaperSetCode} - {paperSet.Description}]");
            sb.AppendLine("DANH SÁCH CÂU HỎI VÀ THANG ĐIỂM CHI TIẾT:");
            foreach (var q in questions)
            {
                sb.AppendLine($"- {q.Title}: {q.Content} (Điểm tối đa: {q.Point}đ)");
                if (q.Rubric is not null && !string.IsNullOrWhiteSpace(q.Rubric.Content))
                {
                    sb.AppendLine($"  + Tiêu chí rubric: {q.Rubric.Content}");
                }
            }
            return sb.ToString();
        }

        return $"Đề thi mã {paperSet.PaperSetCode}. Hãy chấm điểm bài làm chi tiết từng câu theo thang điểm 10.";
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
        var finalEvaluation = submission.Evaluations.FirstOrDefault(e => e.IsFinalScore) ?? lecturerEvaluation;
        var criteriaScores = ParseCriteriaScores(aiEvaluation?.GeneralFeedback);

        // criteriaScores: Điểm & nhận xét theo tiêu chí của AI (EvaluationType.AI)
        // itemDetails: Chi tiết điểm từng câu do Giảng viên (Lecturer) chấm
        var lecturerDisplayEvaluation = (finalEvaluation != null && finalEvaluation.EvaluationType == EvaluationType.Lecturer)
            ? finalEvaluation
            : lecturerEvaluation;

        var itemDetails = lecturerDisplayEvaluation?.Details?
            .GroupBy(d => d.DetailId)
            .Select(g => g.First())
            .Select(d =>
            {
                var pa = submission.ParsedAnswers.FirstOrDefault(p => p.ParsedAnswerId == d.ParsedAnswerId);
                return new EvaluationItemDetailDto
                {
                    DetailId = d.DetailId,
                    EvaluationId = d.EvaluationId,
                    ParsedAnswerId = d.ParsedAnswerId,
                    QuestionId = pa?.QuestionId,
                    QuestionTitle = pa?.Question?.Title ?? string.Empty,
                    Score = d.Score,
                    Comment = d.Comment,
                    AiExtractionId = d.AiExtractionId
                };
            })
            .ToList() ?? new List<EvaluationItemDetailDto>();

        var studentAnswers = submission.ParsedAnswers
            .OrderBy(answer => answer.CreatedAt)
            .Select(answer => new StudentAnswerDto
            {
                QuestionNumber = GetQuestionNumber(answer.Question),
                Question = !string.IsNullOrWhiteSpace(answer.Question?.Title)
                    ? answer.Question.Title
                    : answer.Question?.Content ?? string.Empty,
                ExtractedText = answer.ExtractedText,
                ExtractedImageUrl = answer.ExtractedImageUrl
            })
            .ToList();

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
            IsFinalScore = finalEvaluation?.IsFinalScore ?? false,
            SelectionReason = finalEvaluation?.SelectionReason,
            Status = submission.Status,
            CriteriaScores = criteriaScores,
            ItemDetails = itemDetails,
            StudentAnswers = studentAnswers,
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate
        };
    }

    private static int GetQuestionNumber(Question? question)
    {
        if (question is null)
            return 0;

        var match = Regex.Match(
            question.Title,
            @"(?:Question|Câu)\s*(?<number>\d+)|(?:^|\s)Q\s*(?<number>\d+)",
            RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            match = Regex.Match(question.Content ?? string.Empty, @"(?:Question|Câu)\s*(?<number>\d+)", RegexOptions.IgnoreCase);
        }

        return match.Success && int.TryParse(match.Groups["number"].Value, out var number)
            ? number
            : 0;
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
