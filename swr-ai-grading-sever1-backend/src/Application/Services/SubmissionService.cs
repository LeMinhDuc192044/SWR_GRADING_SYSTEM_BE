using System.Globalization;
using System.Text.RegularExpressions;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.DTOs.Submissions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class SubmissionService : ISubmissionService
{
    private readonly IApplicationDbContext _db;
    private readonly ISupabaseStorage _storage;
    private readonly IGeminiAIService _gemini;

    public SubmissionService(IApplicationDbContext db, ISupabaseStorage storage, IGeminiAIService gemini)
    {
        _db = db;
        _storage = storage;
        _gemini = gemini;
    }

    public async Task<Result<UploadSubmissionsResultDTO>> UploadSubmissionsAsync(
        IReadOnlyList<IDocumentFile> files, Guid currentUserId, Guid? examinationSessionId = null, CancellationToken ct = default)
    {
        var summaries = new List<UploadedSubmissionSummaryDTO>();
        var session = examinationSessionId.HasValue
            ? await _db.ExaminationSessions.AsNoTracking().FirstOrDefaultAsync(item => item.ExaminationSessionId == examinationSessionId.Value, ct)
            : await ResolveDefaultSessionAsync(ct);
        var sessionId = session?.ExaminationSessionId;
        var paperSetId = session?.PaperSetId ?? (await _db.PaperSets.AsNoTracking().Where(p => p.PaperSetId != Guid.Empty).OrderByDescending(p => p.PaperSetId).FirstOrDefaultAsync(ct))?.PaperSetId;

        if (session is null)
            return Result<UploadSubmissionsResultDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");
        if (!paperSetId.HasValue)
            return Result<UploadSubmissionsResultDTO>.Failure("An examination session must be linked to a paper set.", "PAPER_SET_NOT_LINKED");

        var questions = await _db.Questions.AsNoTracking()
            .Where(question => question.PaperSetId == paperSetId.Value)
            .OrderBy(question => question.Title)
            .ToListAsync(ct);

        var roomStudentIds = sessionId.HasValue
            ? await _db.StudentExaminationSessions.AsNoTracking()
                .Where(ses => ses.ExaminationSessionId == sessionId.Value)
                .Select(ses => ses.StudentId)
                .ToListAsync(ct)
            : [];

        var students = roomStudentIds.Count > 0
            ? await _db.Students.AsNoTracking()
                .Where(student => !student.IsDeleted && roomStudentIds.Contains(student.Id))
                .ToListAsync(ct)
            : await _db.Students.AsNoTracking()
                .Where(student => !student.IsDeleted)
                .ToListAsync(ct);

        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(item => item.Id == currentUserId, ct);
        if (staff is null)
            return Result<UploadSubmissionsResultDTO>.Failure("The current user is not a staff member.", "STAFF_NOT_FOUND");
        if (staff.Role != UserRole.Examiner)
            return Result<UploadSubmissionsResultDTO>.Failure("Only examiners can upload submissions.", "FORBIDDEN");

        foreach (var file in files)
        {
            foreach (var package in await ExtractPackagesFromFileAsync(file, ct))
            {
                var summary = await ProcessPackageAsync(package, sessionId!.Value, staff.Id, questions, students, ct);
                summaries.Add(summary);
            }
        }


        return Result<UploadSubmissionsResultDTO>.Success(new UploadSubmissionsResultDTO
        {
            TotalUploaded = summaries.Count,
            TotalSucceeded = summaries.Count(item => item.IsSuccess),
            Submissions = summaries
        });
    }

    private static async Task<IReadOnlyList<SubmissionArchive.ExtractedPackage>> ExtractPackagesFromFileAsync(
        IDocumentFile file,
        CancellationToken ct)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName);
            if (file.Length > SubmissionArchive.MaxArchiveBytes)
                throw new InvalidDataException("Submission file exceeds the 500 MB limit.");

            using var uploadStream = new MemoryStream();
            await file.CopyToAsync(uploadStream, ct);
            uploadStream.Position = 0;

            if (extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return SubmissionArchive.ReadPackages(uploadStream, file.FileName);
            }

            if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = uploadStream.ToArray();
                var singleContent = new SubmissionArchive.Content(
                    bytes,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase));

                return
                [
                    new SubmissionArchive.ExtractedPackage(
                        IsSuccess: true,
                        DisplayFileName: file.FileName,
                        ErrorMessage: null,
                        OriginalBytes: bytes,
                        StorageExtension: ".docx",
                        StorageContentType: file.ContentType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                        Content: singleContent,
                        StudentHints: [Path.GetFileNameWithoutExtension(file.FileName)])
                ];
            }

            throw new InvalidDataException("Only Microsoft Word (.docx), RAR (.rar), or ZIP (.zip) submissions are supported.");
        }
        catch (Exception ex)
        {
            return
            [
                new SubmissionArchive.ExtractedPackage(
                    IsSuccess: false,
                    DisplayFileName: file.FileName,
                    ErrorMessage: ex.Message,
                    OriginalBytes: [],
                    StorageExtension: Path.GetExtension(file.FileName).ToLowerInvariant(),
                    StorageContentType: "application/octet-stream",
                    Content: null,
                    StudentHints: [])
            ];
        }
    }

    private async Task<UploadedSubmissionSummaryDTO> ProcessPackageAsync(
        SubmissionArchive.ExtractedPackage package,
        Guid sessionId,
        Guid staffId,
        IReadOnlyList<Question> questions,
        IReadOnlyList<Student> students,
        CancellationToken ct)
    {
        var summary = new UploadedSubmissionSummaryDTO { SubmissionFile = package.DisplayFileName };
        if (!package.IsSuccess || package.Content is null)
        {
            summary.IsSuccess = false;
            summary.ErrorMessage = package.ErrorMessage ?? "Cannot read submission package.";
            return summary;
        }

        try
        {
            var content = package.Content;
            var student = ResolveStudent(package.StudentHints, content, students, package.DisplayFileName);

            var existing = await _db.StudentSubmissions
                .Where(s => s.StudentId == student.Id && s.ExaminationSessionId == sessionId)
                .OrderByDescending(s => s.CreatedDate)
                .ToListAsync(ct);
            var previous = existing.FirstOrDefault();

            if (existing.Count > 0)
            {
                var existingIds = existing.Select(s => s.SubmissionId).ToList();
                var alreadyGraded =
                    await _db.Assignments.AnyAsync(a => existingIds.Contains(a.SubmissionId)
                        && (a.Status == AssignmentStatus.COMPLETED || a.Status == AssignmentStatus.GRADED), ct)
                    || await _db.SubmissionGradingHistories.AnyAsync(h => existingIds.Contains(h.SubmissionId)
                        && h.Status == SubmissionGradingHistoryStatus.OFFICIAL, ct);
                if (alreadyGraded)
                    throw new InvalidDataException(
                        $"Student {student.StudentCode} already has a graded submission in this examination session; it cannot be replaced.");
            }

            var submissionId = previous?.SubmissionId ?? Guid.NewGuid();
            var imagePaths = new Dictionary<int, string>();
            var extractedText = string.Empty;
            var extractedImages = new List<DocxImageDto>();

            if (content.WordDocument is not null)
            {
                using var wordStream = new MemoryStream(content.WordDocument);
                var wordContent = await _gemini.ExtractContentFromDocxAsync(wordStream, ct);
                extractedText = wordContent.Text ?? string.Empty;
                extractedImages = wordContent.Images;
            }

            for (var index = 0; index < extractedImages.Count; index++)
            {
                var image = extractedImages[index];
                if (string.IsNullOrWhiteSpace(image.Base64Data)) continue;
                var mime = image.MimeType ?? "image/png";
                var ext = mime.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg"
                    : mime.Contains("gif", StringComparison.OrdinalIgnoreCase) ? ".gif"
                    : mime.Contains("webp", StringComparison.OrdinalIgnoreCase) ? ".webp" : ".png";
                var path = $"submissions/{submissionId}/images/image-{index + 1}{ext}";
                await _storage.UploadAsync(path, new MemoryStream(Convert.FromBase64String(image.Base64Data)), mime, ct);
                imagePaths[index + 1] = path;
            }

            var answers = questions.ToDictionary(
                question => question.QuestionId,
                _ => (Text: string.Empty, Xml: (string?)null, ImageUrl: (string?)null));

            if (!string.IsNullOrWhiteSpace(extractedText))
            {
                var extractedAnswers = await _gemini.ExtractAnswersForQuestionsAsync(extractedText, questions, extractedImages, ct);
                foreach (var answer in extractedAnswers.Where(answer => answers.ContainsKey(answer.QuestionId)))
                {
                    var imageUrl = answer.ImageIndexes.FirstOrDefault(imagePaths.ContainsKey) is var imageIndex && imageIndex > 0
                        ? imagePaths[imageIndex] : null;
                    answers[answer.QuestionId] = (answer.ExtractedText ?? string.Empty, null, imageUrl);
                }
            }

            foreach (var diagram in content.DiagramSources)
            {
                var question = questions.FirstOrDefault(item => SubmissionArchive.GetQuestionKey(item.Title) == diagram.Key);
                if (question is null) continue;
                var current = answers[question.QuestionId];
                answers[question.QuestionId] = (current.Text, diagram.Value, current.ImageUrl);
            }

            foreach (var image in content.ImageSources)
            {
                var question = questions.FirstOrDefault(item => SubmissionArchive.GetQuestionKey(item.Title) == image.Key);
                if (question is null) continue;
                var externalPath = $"submissions/{submissionId}/images/{Normalize(image.Key)}.png";
                await _storage.UploadAsync(externalPath, new MemoryStream(image.Value), "image/png", ct);
                var current = answers[question.QuestionId];
                answers[question.QuestionId] = (current.Text, current.Xml, externalPath);
            }

            var now = DateTime.UtcNow;
            var baseTitle = Path.GetFileNameWithoutExtension(Path.GetFileName(package.DisplayFileName));
            var storagePath = $"submissions/{submissionId}/{Guid.NewGuid():N}_{Normalize(baseTitle)}{package.StorageExtension}";
            using (var packageStream = new MemoryStream(package.OriginalBytes, writable: false))
            {
                await _storage.UploadAsync(storagePath, packageStream, package.StorageContentType, ct);
            }

            StudentSubmission submission;
            if (previous is null)
            {
                submission = new StudentSubmission
                {
                    SubmissionId = submissionId,
                    FileSubmissions = storagePath,
                    StudentId = student.Id,
                    Status = SubmissionStatus.Submitted,
                    CreatedDate = now,
                    UpdatedDate = now,
                    UploadedById = staffId,
                    ExaminationSessionId = sessionId
                };
                _db.StudentSubmissions.Add(submission);
            }
            else
            {
                submission = previous;
                submission.FileSubmissions = storagePath;
                submission.Status = SubmissionStatus.Submitted;
                submission.CreatedDate = now;
                submission.UpdatedDate = now;
                submission.UploadedById = staffId;

                // Old answers and old (draft) AI/review results belong to the old file: remove them.
                _db.SubmissionParsedAnswers.RemoveRange(
                    await _db.SubmissionParsedAnswers.Where(p => p.SubmissionId == previous.SubmissionId).ToListAsync(ct));
                _db.SubmissionGradingHistories.RemoveRange(
                    await _db.SubmissionGradingHistories.Where(h => h.SubmissionId == previous.SubmissionId).ToListAsync(ct));
                // Any extra duplicates already in the database are removed too.
                if (existing.Count > 1)
                {
                    _db.StudentSubmissions.RemoveRange(existing.Skip(1));
                }

                summary.IsReplaced = true;
            }

            foreach (var question in questions)
            {
                var answer = answers[question.QuestionId];
                _db.SubmissionParsedAnswers.Add(new SubmissionParsedAnswer
                {
                    ParsedAnswerId = Guid.NewGuid(),
                    SubmissionId = submissionId,
                    QuestionId = question.QuestionId,
                    ExtractedText = string.IsNullOrWhiteSpace(answer.Text) ? null : answer.Text,
                    Extracted_Xml = answer.Xml,
                    ExtractedImageUrl = answer.ImageUrl,
                    CreatedAt = now
                });
            }

            await _db.SaveChangesAsync(ct);
            summary.SubmissionId = submissionId;
            summary.FileSubmissions = storagePath;
            summary.StudentId = student.Id;
            summary.StudentExaminationId = Guid.Empty;
            summary.Status = submission.Status.ToString();
            summary.IsSuccess = true;
        }
        catch (Exception exception)
        {
            summary.IsSuccess = false;
            summary.ErrorMessage = exception.Message;
        }

        return summary;
    }

    public async Task<Result<PagedResult<SubmissionDetailDTO>>> GetPagedListAsync(
        PagedRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        Guid? examinationSessionId = null,
        CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var query = _db.StudentSubmissions.AsNoTracking();

        if (examinationSessionId.HasValue)
            query = query.Where(s => s.ExaminationSessionId == examinationSessionId.Value);

        var totalCount = await query.CountAsync(ct);

        var submissions = await query
            .Include(item => item.ParsedAnswers).ThenInclude(answer => answer.Question)
            .Include(item => item.SubmissionGradingHistories)
            .OrderByDescending(item => item.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var paged = new PagedResult<SubmissionDetailDTO>
        {
            Items = submissions.Select(ToDetail).ToList(),
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<SubmissionDetailDTO>>.Success(paged);
    }

    public async Task<Result<IReadOnlyList<SubmissionDetailDTO>>> GetListAsync(Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var submissions = await _db.StudentSubmissions.AsNoTracking()
            .Include(item => item.ParsedAnswers).ThenInclude(answer => answer.Question)
            .Include(item => item.SubmissionGradingHistories)
            .OrderByDescending(item => item.CreatedDate)
            .ToListAsync(ct);
        return Result<IReadOnlyList<SubmissionDetailDTO>>.Success(submissions.Select(ToDetail).ToList());
    }

    public async Task<Result<SubmissionDetailDTO>> GetByIdAsync(Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var submission = await _db.StudentSubmissions.AsNoTracking()
            .Include(item => item.ParsedAnswers).ThenInclude(answer => answer.Question)
            .Include(item => item.SubmissionGradingHistories)
            .FirstOrDefaultAsync(item => item.SubmissionId == submissionId, ct);
        return submission is null
            ? Result<SubmissionDetailDTO>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND")
            : Result<SubmissionDetailDTO>.Success(ToDetail(submission));
    }

    public Task<Result<SubmissionDetailDTO>> TriggerAiGradingAsync(Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);
    public Task<Result<SubmissionDetailDTO>> ReviewAsync(Guid submissionId, ReviewSubmissionRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);
    public Task<Result<SubmissionDetailDTO>> ReviewAsync(Guid submissionId, decimal lecturerScore, string? comment, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);
    public Task<Result<SubmissionDetailDTO>> DecideScoreAsync(Guid submissionId, decimal lecturerScore, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);
    public Task<Result<SubmissionDetailDTO>> FinalizeAsync(Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);
    public Task<Result<SubmissionDetailDTO>> FinalizeScoreAsync(Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
        => GetByIdAsync(submissionId, currentUserId, isElevatedRole, ct);

    public async Task<Result<SubmissionHistoryDto>> GetHistoryAsync(Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var submission = await _db.StudentSubmissions.AsNoTracking()
            .Include(item => item.SubmissionGradingHistories)
            .FirstOrDefaultAsync(item => item.SubmissionId == submissionId, ct);
        if (submission is null)
            return Result<SubmissionHistoryDto>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");
        var timeline = new List<SubmissionHistoryTimelineEventDto>
        {
            new() { Stage = "Submitted", Title = "Student submission uploaded", Timestamp = submission.CreatedDate, IsCompleted = true }
        };
        return Result<SubmissionHistoryDto>.Success(new SubmissionHistoryDto
        {
            SubmissionId = submissionId,
            SubmissionFile = Path.GetFileName(submission.FileSubmissions),
            CurrentStatus = submission.Status,
            Timeline = timeline
        });
    }

    private async Task<ExaminationSession?> ResolveDefaultSessionAsync(CancellationToken ct)
    {
        return await _db.ExaminationSessions.AsNoTracking()
            .Where(item => item.PaperSetId.HasValue)
            .OrderByDescending(item => item.StartDate)
            .ThenByDescending(item => item.StartTime)
            .FirstOrDefaultAsync(ct);
    }

    private static Student ResolveStudent(
        IReadOnlyList<string> hints,
        SubmissionArchive.Content content,
        IReadOnlyList<Student> students,
        string displayFileName)
    {
        if (students.Count == 0)
            throw new InvalidDataException("Không có danh sách sinh viên hợp lệ để gán bài nộp.");

        foreach (var hint in hints)
        {
            if (string.IsNullOrWhiteSpace(hint))
                continue;

            var matched = FindStudentByCodesInText(hint, students);
            if (matched is not null)
                return matched;
        }

        var docxPlainText = SubmissionArchive.TryExtractDocxPlainText(content.WordDocument);
        if (!string.IsNullOrWhiteSpace(docxPlainText))
        {
            var matched = FindStudentByCodesInText(docxPlainText, students);
            if (matched is not null)
                return matched;
        }

        throw new InvalidDataException(
            $"Không thể xác định sinh viên thuộc phòng thi cho file '{displayFileName}'. " +
            "Vui lòng đặt tên file/thư mục theo MSSV (ví dụ SE180001) hoặc ghi MSSV ở đầu bài làm.");
    }

    private static Student? FindStudentByCodesInText(string text, IReadOnlyList<Student> students)
    {
        foreach (Match match in Regex.Matches(text, @"[A-Za-z]{2}\d{5,8}", RegexOptions.IgnoreCase))
        {
            var student = students.FirstOrDefault(item =>
                item.StudentCode.Equals(match.Value, StringComparison.OrdinalIgnoreCase));
            if (student is not null)
                return student;
        }

        return null;
    }

    private static SubmissionDetailDTO ToDetail(StudentSubmission submission)
    {
        var histories = submission.SubmissionGradingHistories.OrderByDescending(item => item.CreatedAt).ToList();
        var ai = histories.FirstOrDefault(item => item.EvaluationType == EvaluationType.AI);
        var lecturer = histories.FirstOrDefault(item => item.EvaluationType == EvaluationType.Lecturer);
        return new SubmissionDetailDTO
        {
            SubmissionId = submission.SubmissionId,
            FileSubmissions = submission.FileSubmissions,
            SubmissionFile = Path.GetFileName(submission.FileSubmissions),
            FilePath = submission.FileSubmissions,
            StudentId = submission.StudentId,
            StudentExaminationId = Guid.Empty,
            Status = submission.Status,
            StudentAnswers = submission.ParsedAnswers.OrderBy(answer => answer.CreatedAt).Select(answer => new StudentAnswerDto
            {
                QuestionNumber = ExtractQuestionNumber(answer.Question?.Title),
                Question = answer.Question?.Title ?? string.Empty,
                ExtractedText = answer.ExtractedText,
                ExtractedImageUrl = answer.ExtractedImageUrl
            }).ToList(),
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate
        };
    }

    private static int ExtractQuestionNumber(string? title)
    {
        var match = Regex.Match(title ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static string Normalize(string value)
    {
        var normalized = Regex.Replace(value ?? string.Empty, @"[^A-Za-z0-9._-]", "_");
        return string.IsNullOrWhiteSpace(normalized) ? "file" : normalized.Trim('_');
    }

    
}
