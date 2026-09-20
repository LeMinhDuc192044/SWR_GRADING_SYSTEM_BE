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

    public async Task<Result<UploadSubmissionsResultDTO>> UploadAndGradeBatchAsync(
        Guid gradingDiaryId,
        IReadOnlyList<IDocumentFile> files,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (files is null || files.Count == 0)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Vui lòng chọn ít nhất một file bài làm (.docx).", "FILES_REQUIRED");
        }

        var diary = await _diaryRepository.GetDetailByIdAsync(gradingDiaryId, ct);
        if (diary is null)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy sổ chấm thi.", "DIARY_NOT_FOUND");
        }

        var paperSet = diary.PaperSet ?? await _paperSetRepository.GetByIdAsync(diary.PaperSetId, ct);
        if (paperSet is null)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("Không tìm thấy thông tin đề thi liên kết.", "PAPER_SET_NOT_FOUND");
        }

        // 1. Tải và chuẩn bị nội dung Rubric
        var rubricText = await LoadRubricTextAsync(paperSet, ct);

        // 2. Chuẩn bị ExamId
        if (!paperSet.ExaminationId.HasValue || paperSet.ExaminationId.Value == Guid.Empty)
        {
            return Result<UploadSubmissionsResultDTO>.Failure("PaperSet chưa liên kết với kỳ thi.", "PAPER_SET_NOT_LINKED_TO_EXAM");
        }

        var examId = paperSet.ExaminationId.Value;

        var results = new List<UploadedSubmissionSummaryDTO>();
        int successCount = 0;

        foreach (var file in files)
        {
            var summary = new UploadedSubmissionSummaryDTO
            {
                SubmissionName = file.FileName
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
                // 2.1. Kiểm tra và tìm StudentExamination trước
                var studentExamResult = await ResolveStudentExaminationAsync(file.FileName, examId, ct);
                if (!studentExamResult.IsSuccess)
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = studentExamResult.Error;
                    results.Add(summary);
                    continue;
                }

                var studentExam = studentExamResult.Data!;

                // 2.2. Đọc file vào memoryStream
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream, ct);
                memoryStream.Position = 0;

                // 2.3. Trích xuất text từ file docx trước để validate nội dung
                var studentText = await _geminiService.ExtractTextFromDocxAsync(memoryStream, ct);
                if (string.IsNullOrWhiteSpace(studentText))
                {
                    summary.IsSuccess = false;
                    summary.ErrorMessage = "Không thể trích xuất văn bản từ bài làm (file rỗng hoặc chỉ có ảnh scan).";
                    results.Add(summary);
                    continue;
                }

                // 2.4. File hợp lệ -> Upload lên Supabase Storage
                memoryStream.Position = 0;
                var storagePath = $"submissions/{gradingDiaryId}/{file.FileName}";

                // Dùng tạm SubmissionName làm storage path (sẽ có migration tách riêng)
                await _storage.UploadAsync(storagePath, memoryStream, file.ContentType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ct);

                // 2.5. Tạo bản ghi Submission với FilePath lưu storage path
                var submission = new Submission
                {
                    SubmissionId = Guid.NewGuid(),
                    SubmissionName = file.FileName,
                    FilePath = storagePath,
                    Status = SubmissionStatus.AI_Grading,
                    DiaryId = gradingDiaryId,
                    StudentExaminationId = studentExam.StudentExaminationId,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow,
                    Comment = "Đang trong quá trình AI chấm điểm..."
                };

                await _submissionRepository.AddAsync(submission, ct);
                await _unitOfWork.SaveChangesAsync(ct);
                summary.SubmissionId = submission.SubmissionId;
                summary.FilePath = submission.FilePath;

                // 2.6. Gọi Gemini chấm bài theo Rubric
                var gradeResult = await _geminiService.GradeSubmissionAsync(studentText, rubricText, ct);

                // 2.7. Cập nhật kết quả chấm vào Database
                submission.AiScore = gradeResult.TotalScore;
                submission.Comment = gradeResult.OverallComment;
                submission.AiLogs = gradeResult.RawAiLogJson;
                submission.Status = SubmissionStatus.AI_Graded;
                submission.UpdatedDate = DateTime.UtcNow;

                _submissionRepository.Update(submission);
                await _unitOfWork.SaveChangesAsync(ct);

                summary.IsSuccess = true;
                summary.AiScore = submission.AiScore;
                summary.Status = submission.Status.ToString();
                summary.Comment = submission.Comment;
                summary.CriteriaScores = gradeResult.CriteriaScores;
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
            GradingDiaryId = gradingDiaryId,
            TotalUploaded = files.Count,
            TotalGradedSuccessfully = successCount,
            Submissions = results
        };

        return Result<UploadSubmissionsResultDTO>.Success(response);
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
                sb.AppendLine($"- {q.Title}: {q.content} (Điểm tối đa: {q.point}đ)");
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

    public async Task<Result<SubmissionDetailDTO>> DecideScoreAsync(
        Guid submissionId,
        decimal lecturerScore,
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
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        if (submission is null)
        {
            return Result<SubmissionDetailDTO>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
        {
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền ra quyết định điểm cho bài nộp này.", "FORBIDDEN");
        }

        // Cập nhật điểm giảng viên quyết định (chuyển sang trạng thái Lecturer_Reviewed)
        // GIỮ NGUYÊN Comment nhận xét của AI, không chỉnh sửa
        submission.LecturerScore = lecturerScore;
        submission.Status = SubmissionStatus.Lecturer_Reviewed;
        submission.UpdatedDate = DateTime.UtcNow;

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    public async Task<Result<SubmissionDetailDTO>> FinalizeScoreAsync(
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
            return Result<SubmissionDetailDTO>.Failure("Bạn không có quyền chốt điểm cho bài nộp này.", "FORBIDDEN");
        }

        if (submission.Status != SubmissionStatus.Lecturer_Reviewed && !submission.LecturerScore.HasValue)
        {
            return Result<SubmissionDetailDTO>.Failure("Bài nộp chưa có điểm giảng viên đánh giá để chốt điểm.", "INVALID_STATUS");
        }

        submission.Status = SubmissionStatus.Final;
        submission.UpdatedDate = DateTime.UtcNow;

        _submissionRepository.Update(submission);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<SubmissionDetailDTO>.Success(ToDetailDto(submission));
    }

    private static SubmissionDetailDTO ToDetailDto(Submission submission)
    {
        var criteriaScores = new List<CriterionScoreDto>();
        if (!string.IsNullOrWhiteSpace(submission.AiLogs))
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var aiResult = System.Text.Json.JsonSerializer.Deserialize<GradingResultDto>(submission.AiLogs, options);
                if (aiResult?.CriteriaScores is not null)
                {
                    criteriaScores.AddRange(aiResult.CriteriaScores);
                }
            }
            catch { }
        }

        return new SubmissionDetailDTO
        {
            SubmissionId = submission.SubmissionId,
            SubmissionName = submission.SubmissionName,
            FilePath = submission.FilePath,
            DiaryId = submission.DiaryId,
            DiaryName = submission.GradingDiary?.Name ?? string.Empty,
            StudentCode = submission.StudentExamination?.Student?.StundentCode ?? string.Empty,
            StudentName = submission.StudentExamination?.Student?.FullName ?? string.Empty,
            AiScore = submission.AiScore,
            LecturerScore = submission.LecturerScore,
            Status = submission.Status,
            AiComment = submission.Comment,
            CriteriaScores = criteriaScores,
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate
        };
    }

    [GeneratedRegex(@"[A-Za-z]{2}\d{5,8}")]
    private static partial Regex StudentCodeRegex();
}
