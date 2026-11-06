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

        if (examinationSessionId.HasValue && session is null)
            return Result<UploadSubmissionsResultDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");
        if (!paperSetId.HasValue)
            return Result<UploadSubmissionsResultDTO>.Failure("An examination session must be linked to a paper set.", "PAPER_SET_NOT_LINKED");

        var questions = await _db.Questions.AsNoTracking()
            .Where(question => question.PaperSetId == paperSetId.Value)
            .OrderBy(question => question.Title)
            .ToListAsync(ct);

        var students = await _db.Students.AsNoTracking().Where(student => !student.IsDeleted).ToListAsync(ct);
        var staff = await _db.Staff.AsNoTracking().FirstOrDefaultAsync(item => item.Id == currentUserId, ct);
        if (staff is null)
            return Result<UploadSubmissionsResultDTO>.Failure("The current user is not a staff member.", "STAFF_NOT_FOUND");
        if (staff.Role != UserRole.Examiner)
            return Result<UploadSubmissionsResultDTO>.Failure("Only examiners can upload submissions.", "FORBIDDEN");

        foreach (var file in files)
        {
            var summary = new UploadedSubmissionSummaryDTO { SubmissionFile = file.FileName };
            try
            {
                var extension = Path.GetExtension(file.FileName);
                if (file.Length > SubmissionArchive.MaxArchiveBytes)
                    throw new InvalidDataException("Submission file exceeds the 100 MB limit.");

                using var uploadStream = new MemoryStream();
                await file.CopyToAsync(uploadStream, ct);
                uploadStream.Position = 0;

                SubmissionArchive.Content content;
                if (extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) || extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                    content = SubmissionArchive.Read(uploadStream);
                else if (extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                    content = new SubmissionArchive.Content(uploadStream.ToArray(), new Dictionary<string, string>(), new Dictionary<string, byte[]>());
                else
                    throw new InvalidDataException("Only Microsoft Word (.docx), RAR (.rar), or ZIP (.zip) submissions are supported.");

                var submissionId = Guid.NewGuid();
                var student = ResolveStudent(file.FileName, students);
                var imagePaths = new Dictionary<int, string>();
                string extractedText = string.Empty;
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
                    var imageExtension = Path.GetExtension(image.Key + ".png").Equals(".png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".png";
                    var externalPath = $"submissions/{submissionId}/images/{Normalize(image.Key)}{imageExtension}";
                    await _storage.UploadAsync(externalPath, new MemoryStream(image.Value), "image/png", ct);
                    var current = answers[question.QuestionId];
                    answers[question.QuestionId] = (current.Text, current.Xml, externalPath);
                }

                var now = DateTime.UtcNow;
                var storagePath = $"submissions/{submissionId}/{Guid.NewGuid():N}_{Normalize(Path.GetFileNameWithoutExtension(file.FileName))}{extension.ToLowerInvariant()}";
                uploadStream.Position = 0;
                await _storage.UploadAsync(storagePath, uploadStream, file.ContentType ?? "application/octet-stream", ct);

                var submission = new StudentSubmission
                {
                    SubmissionId = submissionId,
                    FileSubmissions = storagePath,
                    StudentId = student.Id,
                    Status = SubmissionStatus.Submitted,
                    CreatedDate = now,
                    UpdatedDate = now,
                    UploadedById = staff.Id,
                    ExaminationSessionId = sessionId
                };
                _db.StudentSubmissions.Add(submission);

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
            summaries.Add(summary);
        }

        return Result<UploadSubmissionsResultDTO>.Success(new UploadSubmissionsResultDTO
        {
            TotalUploaded = files.Count,
            TotalSucceeded = summaries.Count(item => item.IsSuccess),
            Submissions = summaries
        });
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

    private static Student ResolveStudent(string fileName, IReadOnlyList<Student> students)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var match = Regex.Match(stem, @"[A-Za-z]{2}\d{5,8}", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var student = students.FirstOrDefault(item => item.StudentCode.Equals(match.Value, StringComparison.OrdinalIgnoreCase));
            if (student is not null) return student;
        }
        return students.First();
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
