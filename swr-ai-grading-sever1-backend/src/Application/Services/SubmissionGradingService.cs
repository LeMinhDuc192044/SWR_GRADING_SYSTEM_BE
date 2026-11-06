using System.Text;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Grading;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class SubmissionGradingService : ISubmissionGradingService
{
    private const decimal MaxTotalScore = 10m;

    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeminiAIService _gemini;
    private readonly ISupabaseStorage _storage;

    public SubmissionGradingService(
        IApplicationDbContext db, IUnitOfWork unitOfWork, IGeminiAIService gemini, ISupabaseStorage storage)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _gemini = gemini;
        _storage = storage;
    }

    // ---------------------------------------------------------------- list
    public async Task<Result<IReadOnlyList<GradingHistoryDTO>>> GetHistoriesAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess)
            return Result<IReadOnlyList<GradingHistoryDTO>>.Failure(access.Error!, access.ErrorCode);

        var histories = await HistoriesWithDetails().AsNoTracking()
            .Where(h => h.SubmissionId == submissionId)
            .OrderBy(h => h.GradingRound)
            .ToListAsync(ct);

        return Result<IReadOnlyList<GradingHistoryDTO>>.Success(histories.Select(Map).ToList());
    }

    // ---------------------------------------------------------------- AI grading
    public async Task<Result<GradingHistoryDTO>> GradeWithAiAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        if (await HasOfficialAsync(submissionId, ct))
            return Fail("This submission already has an official grade.", "ALREADY_FINALIZED");

        var parsedAnswers = await _db.SubmissionParsedAnswers.AsNoTracking()
            .Include(p => p.Question)
            .Where(p => p.SubmissionId == submissionId)
            .ToListAsync(ct);
        if (parsedAnswers.Count == 0)
            return Fail("The submission has no parsed answers to grade.", "NO_PARSED_ANSWERS");

        var questionIds = parsedAnswers.Select(p => p.QuestionId).Distinct().ToList();
        var criteria = await _db.QuestionCriteria.AsNoTracking()
            .Where(c => questionIds.Contains(c.QuestionId))
            .ToListAsync(ct);
        var rubric = BuildRubric(parsedAnswers, criteria);

        GradingResultDto aiResult;
        try
        {
            var images = await LoadImagesAsync(parsedAnswers, ct);
            var pairs = parsedAnswers
                .OrderBy(p => p.Question.Title)
                .Select(p => (Question: p.Question, ParsedAnswer: p))
                .ToList();
            aiResult = await _gemini.GradeSubmissionAnswersAsync(pairs, rubric, images, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException)
        {
            return Fail($"AI grading failed: {ex.Message}", "AI_GRADING_FAILED");
        }

        // ASSUMPTION: GradingResultDto exposes CriteriaScores (QuestionId, ActualScore, Comment) and OverallComment.
        var details = new List<SubmissionItemDetail>();
        var graded = new HashSet<Guid>();
        foreach (var answer in parsedAnswers)
        {
            var criterion = aiResult.CriteriaScores.FirstOrDefault(c =>
                string.Equals(c.QuestionId.ToString(), answer.QuestionId.ToString(), StringComparison.OrdinalIgnoreCase));

            decimal score = 0m;
            string? comment;
            if (criterion is null)
                comment = "AI did not return a score for this question.";
            else if (!graded.Add(answer.QuestionId))
                comment = "Duplicate answer for this question; scored on the first one.";
            else
            {
                score = Math.Clamp(Convert.ToDecimal(criterion.ActualScore), 0m, answer.Question.Point);
                comment = criterion.Comment;
            }

            details.Add(new SubmissionItemDetail { ParsedAnswerId = answer.ParsedAnswerId, Score = score, Comment = comment });
        }

        var total = details.Sum(d => d.Score);
        if (total > MaxTotalScore)
            return Fail($"Total score {total} exceeds the maximum of {MaxTotalScore}.", "TOTAL_SCORE_EXCEEDS_MAX");

        var history = new SubmissionGradingHistory
        {
            SubmissionId = submissionId,
            EvaluationType = EvaluationType.AI,
            GradingRound = await NextRoundAsync(submissionId, ct),
            TotalScore = total,
            GeneralFeedback = aiResult.OverallComment,
            Status = SubmissionGradingHistoryStatus.DRAFT,   // AI results are always DRAFT
            CreatedAt = DateTime.UtcNow,
            Details = details
        };

        _db.SubmissionGradingHistories.Add(history);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- monitor review
    public async Task<Result<GradingHistoryDTO>> ReviewAsync(
        Guid submissionId, ReviewGradingRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        if (request.Items.Count == 0)
            return Fail("At least one updated item is required.", "NO_CHANGES");
        if (request.Items.GroupBy(i => i.ParsedAnswerId).Any(g => g.Count() > 1))
            return Fail("Each answer can only be updated once per review.", "DUPLICATE_ITEMS");
        if (await HasOfficialAsync(submissionId, ct))
            return Fail("This submission already has an official grade.", "ALREADY_FINALIZED");

        var baseQuery = HistoriesWithDetails().AsNoTracking().Where(h => h.SubmissionId == submissionId);
        var baseHistory = request.BaseHistoryId.HasValue
            ? await baseQuery.FirstOrDefaultAsync(h => h.HistoryId == request.BaseHistoryId.Value, ct)
            : await baseQuery.OrderByDescending(h => h.GradingRound).FirstOrDefaultAsync(ct);
        if (baseHistory is null)
            return Fail("No grading result to review. Run AI grading first.", "BASE_HISTORY_NOT_FOUND");

        var parsedAnswers = await _db.SubmissionParsedAnswers.AsNoTracking()
            .Include(p => p.Question)
            .Where(p => p.SubmissionId == submissionId)
            .ToListAsync(ct);
        var answersById = parsedAnswers.ToDictionary(p => p.ParsedAnswerId);

        foreach (var item in request.Items)
        {
            if (!answersById.TryGetValue(item.ParsedAnswerId, out var answer))
                return Fail($"Answer {item.ParsedAnswerId} does not belong to this submission.", "INVALID_PARSED_ANSWER");
            if (item.Score < 0 || item.Score > answer.Question.Point)
                return Fail($"Score for '{answer.Question.Title}' must be between 0 and {answer.Question.Point}.", "INVALID_SCORE");
        }

        var overrides = request.Items.ToDictionary(i => i.ParsedAnswerId);
        var previousByAnswer = baseHistory.Details.ToDictionary(d => d.ParsedAnswerId);

        // New list = every answer: updated items replace old ones, the rest are carried over.
        var details = new List<SubmissionItemDetail>();
        foreach (var answer in parsedAnswers)
        {
            previousByAnswer.TryGetValue(answer.ParsedAnswerId, out var previous);
            if (overrides.TryGetValue(answer.ParsedAnswerId, out var item))
                details.Add(new SubmissionItemDetail
                {
                    ParsedAnswerId = answer.ParsedAnswerId,
                    Score = item.Score,
                    Comment = item.Comment ?? previous?.Comment
                });
            else
                details.Add(new SubmissionItemDetail
                {
                    ParsedAnswerId = answer.ParsedAnswerId,
                    Score = previous?.Score ?? 0m,
                    Comment = previous?.Comment
                });
        }

        var total = details.Sum(d => d.Score);
        if (total > MaxTotalScore)
            return Fail($"Total score {total} exceeds the maximum of {MaxTotalScore}.", "TOTAL_SCORE_EXCEEDS_MAX");

        var history = new SubmissionGradingHistory
        {
            SubmissionId = submissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = await NextRoundAsync(submissionId, ct),
            TotalScore = total,
            GeneralFeedback = request.GeneralFeedback ?? baseHistory.GeneralFeedback,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            Details = details
        };

        _db.SubmissionGradingHistories.Add(history);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- finalize (DRAFT -> OFFICIAL)
    public async Task<Result<GradingHistoryDTO>> FinalizeAsync(
        Guid submissionId, Guid historyId, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        var history = await _db.SubmissionGradingHistories
            .FirstOrDefaultAsync(h => h.HistoryId == historyId && h.SubmissionId == submissionId, ct);
        if (history is null)
            return Fail("Grading result not found.", "GRADING_HISTORY_NOT_FOUND");

        if (history.Status == SubmissionGradingHistoryStatus.OFFICIAL)
            return Fail("This grading result is already official.", "ALREADY_FINALIZED");
        if (await HasOfficialAsync(submissionId, ct))
            return Fail("Another grading result is already official for this submission.", "ALREADY_FINALIZED");
        if (history.TotalScore is null or > MaxTotalScore)
            return Fail($"Total score must be set and at most {MaxTotalScore}.", "INVALID_TOTAL_SCORE");

        history.Status = SubmissionGradingHistoryStatus.OFFICIAL;

        var assignment = access.Data!;
        assignment.Status = AssignmentStatus.GRADED;
        assignment.UpdatedDate = VietnamTime.Now;

        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- helpers
    private async Task<Result<Assignment>> GetAssignmentAsync(Guid submissionId, Guid staffId, CancellationToken ct)
    {
        var assignment = await _db.Assignments
            .Where(a => a.SubmissionId == submissionId && a.StaffId == staffId)
            .OrderByDescending(a => a.AssignedDate)
            .FirstOrDefaultAsync(ct);

        return assignment is null
            ? Result<Assignment>.Failure("You are not assigned to grade this submission.", "FORBIDDEN")
            : Result<Assignment>.Success(assignment);
    }

    private async Task<bool> HasOfficialAsync(Guid submissionId, CancellationToken ct)
        => await _db.SubmissionGradingHistories.AsNoTracking().AnyAsync(h =>
            h.SubmissionId == submissionId && h.Status == SubmissionGradingHistoryStatus.OFFICIAL, ct);

    private async Task<int> NextRoundAsync(Guid submissionId, CancellationToken ct)
        => (await _db.SubmissionGradingHistories
               .Where(h => h.SubmissionId == submissionId)
               .MaxAsync(h => (int?)h.GradingRound, ct) ?? 0) + 1;

    private IQueryable<SubmissionGradingHistory> HistoriesWithDetails()
        => _db.SubmissionGradingHistories
            .Include(h => h.Details).ThenInclude(d => d.ParsedAnswer).ThenInclude(p => p.Question);

    private async Task<Result<GradingHistoryDTO>> ReloadAsync(Guid historyId, CancellationToken ct)
    {
        var saved = await HistoriesWithDetails().AsNoTracking().FirstAsync(h => h.HistoryId == historyId, ct);
        return Result<GradingHistoryDTO>.Success(Map(saved));
    }

    private static string BuildRubric(IEnumerable<SubmissionParsedAnswer> answers, IReadOnlyList<QuestionCriteria> criteria)
    {
        var sb = new StringBuilder();
        foreach (var question in answers.Select(a => a.Question).DistinctBy(q => q.QuestionId).OrderBy(q => q.Title))
        {
            sb.AppendLine($"{question.Title} (question_id: {question.QuestionId}, max {question.Point} points)");
            foreach (var c in criteria.Where(c => c.QuestionId == question.QuestionId))
                sb.AppendLine($"  - {c.Criteria} ({c.Point} points)");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private async Task<List<DocxImageDto>> LoadImagesAsync(IEnumerable<SubmissionParsedAnswer> answers, CancellationToken ct)
    {
        var images = new List<DocxImageDto>();
            var paths = answers.OrderBy(a => a.Question.Title)
            .Select(a => a.ExtractedImageUrl)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct();

        foreach (var path in paths)
        {
            var file = await _storage.DownloadAsync(path!, Path.GetFileName(path!), ct);
            await using var stream = file.Content;

            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);

            images.Add(new DocxImageDto
            {
                MimeType = GetMimeType(path!),
                Base64Data = Convert.ToBase64String(ms.ToArray())
            });
        }
        return images;
    }

    private static string GetMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/png"
    };

    private static Result<GradingHistoryDTO> Fail(string? error, string? code)
        => Result<GradingHistoryDTO>.Failure(error ?? "Request failed.", code);

    private static GradingHistoryDTO Map(SubmissionGradingHistory h) => new()
    {
        HistoryId = h.HistoryId,
        SubmissionId = h.SubmissionId,
        EvaluationType = h.EvaluationType,
        GradingRound = h.GradingRound,
        TotalScore = h.TotalScore,
        GeneralFeedback = h.GeneralFeedback,
        Status = h.Status,
        CreatedAt = h.CreatedAt,
        Details = h.Details
            .OrderBy(d => d.ParsedAnswer.Question.Title)
            .Select(d => new GradingItemDetailDTO
            {
                DetailId = d.DetailId,
                ParsedAnswerId = d.ParsedAnswerId,
                QuestionId = d.ParsedAnswer.QuestionId,
                QuestionTitle = d.ParsedAnswer.Question.Title,
                MaxScore = d.ParsedAnswer.Question.Point,
                Score = d.Score,
                Comment = d.Comment
            }).ToList()
    };
}