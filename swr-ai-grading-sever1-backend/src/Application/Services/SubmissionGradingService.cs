using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
    public async Task<Result<SubmissionGradingOverviewDTO>> GetHistoriesAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess)
            return Result<SubmissionGradingOverviewDTO>.Failure(access.Error!, access.ErrorCode);

        var allHistories = await HistoriesWithDetails().AsNoTracking()
            .Where(h => h.SubmissionId == submissionId)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var cycle = await ResolveCycleContextAsync(submissionId, access.Data!, allHistories, ct);
        var visibleHistories = cycle.IsBlindRegrade
            ? FilterHistoriesForCurrentCycle(allHistories, cycle)
            : allHistories;

        var parsedAnswers = await _db.SubmissionParsedAnswers.AsNoTracking()
            .Include(p => p.Question)
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.Question.Title)
            .ThenBy(p => p.CreatedAt)
            .ToListAsync(ct);

        var questionIds = parsedAnswers.Select(p => p.QuestionId).Distinct().ToList();
        var criteria = await _db.QuestionCriteria.AsNoTracking()
            .Where(c => questionIds.Contains(c.QuestionId))
            .ToListAsync(ct);
        var criteriaByQuestion = criteria
            .GroupBy(c => c.QuestionId)
            .ToDictionary(g => g.Key, g => g.Select(c => new ParsedAnswerCriteriaDTO
            {
                Criteria = c.Criteria,
                Point = c.Point
            }).ToList());

        return Result<SubmissionGradingOverviewDTO>.Success(new SubmissionGradingOverviewDTO
        {
            SubmissionId = submissionId,
            Histories = visibleHistories.Select(Map).ToList(),
            ParsedAnswers = parsedAnswers.Select(p => new ParsedAnswerDTO
            {
                ParsedAnswerId = p.ParsedAnswerId,
                QuestionId = p.QuestionId,
                QuestionTitle = p.Question.Title,
                MaxScore = p.Question.Point,
                Criteria = criteriaByQuestion.TryGetValue(p.QuestionId, out var list) ? list : [],
                ElementId = p.ElementId,
                ExtractedText = p.ExtractedText,
                ExtractedXml = p.Extracted_Xml,
                ExtractedImageUrl = p.ExtractedImageUrl,
                CreatedAt = p.CreatedAt
            }).ToList()
        });
    }

    // ---------------------------------------------------------------- AI grading
    public async Task<Result<GradingHistoryDTO>> GradeWithAiAsync(
        Guid submissionId, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        var allHistories = await _db.SubmissionGradingHistories.AsNoTracking()
            .Where(h => h.SubmissionId == submissionId)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var cycle = await ResolveCycleContextAsync(submissionId, access.Data!, allHistories, ct);
        if (HasOfficialForCycle(allHistories, cycle))
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

        var allHistories = await HistoriesWithDetails().AsNoTracking()
            .Where(h => h.SubmissionId == submissionId)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var cycle = await ResolveCycleContextAsync(submissionId, access.Data!, allHistories, ct);
        if (HasOfficialForCycle(allHistories, cycle))
            return Fail("This submission already has an official grade.", "ALREADY_FINALIZED");

        var currentCycleHistories = cycle.IsBlindRegrade
            ? FilterHistoriesForCurrentCycle(allHistories, cycle)
            : allHistories;

        SubmissionGradingHistory? baseHistory = null;
        if (request.BaseHistoryId.HasValue)
        {
            baseHistory = currentCycleHistories.FirstOrDefault(h => h.HistoryId == request.BaseHistoryId.Value);
            if (baseHistory is null)
            {
                if (cycle.IsBlindRegrade && allHistories.Any(h => h.HistoryId == request.BaseHistoryId.Value))
                    return Fail("Blind grading does not allow referencing grading results from a prior round.", "FORBIDDEN");

                return Fail("No grading result to review. Run AI grading first.", "BASE_HISTORY_NOT_FOUND");
            }
        }
        else
        {
            baseHistory = currentCycleHistories.OrderByDescending(h => h.GradingRound).FirstOrDefault();
            if (baseHistory is null && !cycle.IsBlindRegrade)
                return Fail("No grading result to review. Run AI grading first.", "BASE_HISTORY_NOT_FOUND");
        }

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
        var previousByAnswer = baseHistory?.Details.ToDictionary(d => d.ParsedAnswerId)
            ?? new Dictionary<Guid, SubmissionItemDetail>();

        // New list = every answer: updated items replace old ones, the rest are carried over (or 0 in blind regrade).
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
            GeneralFeedback = request.GeneralFeedback ?? baseHistory?.GeneralFeedback,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            Details = details
        };

        _db.SubmissionGradingHistories.Add(history);
        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- update one graded item
    public async Task<Result<GradingHistoryDTO>> UpdateItemDetailAsync(
        Guid detailId, UpdateGradingItemRequest request,
        Guid currentUserId, CancellationToken ct = default)
    {
        // Only the detail id is needed: its grading history, submission and question come from it.
        var detail = await _db.SubmissionItemDetails
            .Include(d => d.ParsedAnswer)
                .ThenInclude(p => p.Question)
            .Include(d => d.SubmissionGradingHistory)
                .ThenInclude(h => h.Details)
                    .ThenInclude(item => item.ParsedAnswer)
                        .ThenInclude(p => p.Question)
            .FirstOrDefaultAsync(d => d.DetailId == detailId, ct);

        if (detail is null)
            return Fail("Graded item not found.", "GRADING_DETAIL_NOT_FOUND");

        var history = detail.SubmissionGradingHistory;
        var submissionId = history.SubmissionId;

        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        if (history.Status == SubmissionGradingHistoryStatus.OFFICIAL)
            return Fail("An official grading result cannot be edited.", "GRADING_HISTORY_FINALIZED");

        var question = detail.ParsedAnswer.Question;
        if (request.Score < 0 || request.Score > question.Point)
            return Fail(
                $"Score for '{question.Title}' must be between 0 and {question.Point}.",
                "INVALID_SCORE");

        var newTotal = history.Details.Sum(d => d.DetailId == detailId ? request.Score : d.Score);
        if (newTotal > MaxTotalScore)
            return Fail($"Total score {newTotal} exceeds the maximum of {MaxTotalScore}.", "TOTAL_SCORE_EXCEEDS_MAX");

        detail.Score = request.Score;
        detail.Comment = request.Comment;
        history.TotalScore = newTotal;

        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- lecturer grading (manual entry)
    public async Task<Result<GradingHistoryDTO>> CreateLecturerGradingAsync(
        Guid submissionId, CreateLecturerGradingRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var access = await GetAssignmentAsync(submissionId, currentUserId, ct);
        if (!access.IsSuccess) return Fail(access.Error, access.ErrorCode);

        if (request.Items.Count == 0)
            return Fail("At least one graded question is required.", "NO_GRADING_ITEMS");

        if (request.Items.Any(i => i.QuestionNumber <= 0))
            return Fail("Question numbers start at 1.", "INVALID_QUESTION_NUMBER");

        if (request.Items.GroupBy(i => i.QuestionNumber).Any(g => g.Count() > 1))
            return Fail("Each question can only be graded once.", "DUPLICATE_QUESTION");

        var parsedAnswers = await _db.SubmissionParsedAnswers.AsNoTracking()
            .Include(p => p.Question)
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

        if (parsedAnswers.Count == 0)
            return Fail("The submission has no parsed answers to grade.", "NO_PARSED_ANSWERS");

        // Question number is the number in the question title, e.g. "Question 1" -> 1.
        var answersByNumber = new Dictionary<int, Domain.Entities.SubmissionParsedAnswer>();
        foreach (var answer in parsedAnswers)
        {
            var number = ExtractQuestionNumber(answer.Question.Title);
            if (number <= 0)
                return Fail(
                    $"Question '{answer.Question.Title}' has no number in its title, so it cannot be graded by question number.",
                    "INVALID_QUESTION_TITLE");

            if (!answersByNumber.TryAdd(number, answer))
                return Fail(
                    $"More than one question is numbered {number}; grading by question number is ambiguous.",
                    "DUPLICATE_QUESTION_NUMBER");
        }

        var submittedNumbers = request.Items
            .Select(i => i.QuestionNumber)
            .ToList();

        var unknownNumbers = submittedNumbers
            .Where(number => !answersByNumber.ContainsKey(number))
            .OrderBy(number => number)
            .ToList();
        if (unknownNumbers.Count > 0)
            return Fail(
                $"These question numbers do not belong to the submission: {string.Join(", ", unknownNumbers)}.",
                "QUESTION_NOT_FOUND");

        // Every question of the paper must be scored so the total covers question 1..n.
        var missingNumbers = answersByNumber.Keys
            .Where(number => !submittedNumbers.Contains(number))
            .OrderBy(number => number)
            .ToList();
        if (missingNumbers.Count > 0)
            return Fail(
                $"Missing scores for question number(s): {string.Join(", ", missingNumbers)}.",
                "MISSING_QUESTION_SCORES");

        var details = new List<SubmissionItemDetail>();
        foreach (var item in request.Items)
        {
            var answer = answersByNumber[item.QuestionNumber];
            if (item.Score < 0 || item.Score > answer.Question.Point)
                return Fail(
                    $"Score for question {item.QuestionNumber} must be between 0 and {answer.Question.Point}.",
                    "INVALID_SCORE");

            details.Add(new SubmissionItemDetail
            {
                ParsedAnswerId = answer.ParsedAnswerId,
                Score = item.Score,
                Comment = item.Comment
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
            GeneralFeedback = request.GeneralFeedback,
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

        var allHistories = await _db.SubmissionGradingHistories.AsNoTracking()
            .Where(h => h.SubmissionId == submissionId)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

        var cycle = await ResolveCycleContextAsync(submissionId, access.Data!, allHistories, ct);
        if (cycle.IsBlindRegrade && !IsInCurrentCycle(history, cycle))
            return Fail("Blind grading does not allow finalizing a grading result from a prior round.", "FORBIDDEN");

        if (history.Status == SubmissionGradingHistoryStatus.OFFICIAL)
            return Fail("This grading result is already official.", "ALREADY_FINALIZED");
        if (HasOfficialForCycle(allHistories, cycle))
            return Fail("Another grading result is already official for this grading assignment.", "ALREADY_FINALIZED");
        if (history.TotalScore is null or > MaxTotalScore)
            return Fail($"Total score must be set and at most {MaxTotalScore}.", "INVALID_TOTAL_SCORE");

        history.Status = SubmissionGradingHistoryStatus.OFFICIAL;

        var assignment = access.Data!;
        assignment.Status = AssignmentStatus.GRADED;
        assignment.UpdatedDate = VietnamTime.Now;

        await _unitOfWork.SaveChangesAsync(ct);
        return await ReloadAsync(history.HistoryId, ct);
    }

    // ---------------------------------------------------------------- update status (ONLY OFFICIAL)
    public async Task<Result<GradingHistoryDTO>> UpdateStatusAsync(
        Guid historyId, UpdateGradingStatusRequest request,
        Guid currentUserId, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(request.Status))
            return Fail("Invalid grading status.", "INVALID_GRADING_STATUS");

        if (request.Status != SubmissionGradingHistoryStatus.OFFICIAL)
            return Fail("Only the OFFICIAL status can be set through this endpoint.", "INVALID_GRADING_STATUS");

        // Only the history id is needed: the submission it belongs to comes from it.
        var history = await _db.SubmissionGradingHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.HistoryId == historyId, ct);

        if (history is null)
            return Fail("Grading result not found.", "GRADING_HISTORY_NOT_FOUND");

        var submissionId = history.SubmissionId;

        // A submission may only have one official grading result.
        var alreadyOfficial = await _db.SubmissionGradingHistories
            .AsNoTracking()
            .AnyAsync(h => h.SubmissionId == submissionId
                           && h.Status == SubmissionGradingHistoryStatus.OFFICIAL, ct);

        if (alreadyOfficial)
            return Fail("This submission already has an official grading result.", "ALREADY_FINALIZED");

        return await FinalizeAsync(submissionId, historyId, currentUserId, ct);
    }

    // ---------------------------------------------------------------- helpers
    private async Task<Result<Assignment>> GetAssignmentAsync(Guid submissionId, Guid staffId, CancellationToken ct)
    {
        var assignment = await _db.Assignments
            .Include(a => a.GradingSession)
            .Where(a => a.SubmissionId == submissionId && a.StaffId == staffId)
            .OrderByDescending(a => a.AssignedDate)
            .FirstOrDefaultAsync(ct);

        return assignment is null
            ? Result<Assignment>.Failure("You are not assigned to grade this submission.", "FORBIDDEN")
            : Result<Assignment>.Success(assignment);
    }

    private sealed record GradingCycleContext(
        Assignment Assignment,
        bool IsBlindRegrade,
        int? BaselineRound);

    private async Task<GradingCycleContext> ResolveCycleContextAsync(
        Guid submissionId,
        Assignment currentAssignment,
        IReadOnlyList<SubmissionGradingHistory> allHistories,
        CancellationToken ct)
    {
        var otherAssignments = await _db.Assignments.AsNoTracking()
            .Include(a => a.GradingSession)
            .Where(a => a.SubmissionId == submissionId && a.AssignmentId != currentAssignment.AssignmentId)
            .ToListAsync(ct);

        var isRegradeByTitle = IsRegradeSessionTitle(currentAssignment.GradingSession?.Title);
        var currentSessionCreated = currentAssignment.GradingSession?.CreatedDate;
        var hasPriorAssignment = otherAssignments.Any(a =>
            a.AssignedDate < currentAssignment.AssignedDate
            || (a.AssignedDate == currentAssignment.AssignedDate
                && (a.Status is AssignmentStatus.GRADED or AssignmentStatus.COMPLETED
                    || (currentSessionCreated.HasValue
                        && a.GradingSession is not null
                        && a.GradingSession.CreatedDate < currentSessionCreated.Value))));

        var isBlindRegrade = isRegradeByTitle || hasPriorAssignment;
        if (!isBlindRegrade)
            return new GradingCycleContext(currentAssignment, IsBlindRegrade: false, BaselineRound: null);

        var officials = allHistories
            .Where(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToList();

        int? baselineRound = null;
        var isCurrentFinalized = currentAssignment.Status is AssignmentStatus.GRADED or AssignmentStatus.COMPLETED;

        if (isCurrentFinalized && officials.Count >= 2)
        {
            baselineRound = officials[^2].GradingRound;
        }
        else if (!isCurrentFinalized && officials.Count > 0)
        {
            baselineRound = officials[^1].GradingRound;
        }
        else
        {
            var priorHistories = allHistories
                .Where(h => h.CreatedAt < currentAssignment.AssignedDate)
                .ToList();
            if (priorHistories.Count > 0)
                baselineRound = priorHistories.Max(h => h.GradingRound);
        }

        return new GradingCycleContext(currentAssignment, IsBlindRegrade: true, BaselineRound: baselineRound);
    }

    private static int ExtractQuestionNumber(string? title)
    {
        var match = Regex.Match(title ?? string.Empty, @"\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static bool IsRegradeSessionTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        return title.Contains("phúc khảo", StringComparison.OrdinalIgnoreCase)
            || title.Contains("phuc khao", StringComparison.OrdinalIgnoreCase)
            || title.Contains("regrade", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsInCurrentCycle(SubmissionGradingHistory history, GradingCycleContext cycle)
    {
        if (!cycle.IsBlindRegrade)
            return true;

        if (cycle.BaselineRound.HasValue)
            return history.GradingRound > cycle.BaselineRound.Value;

        return history.CreatedAt >= cycle.Assignment.AssignedDate;
    }

    private static List<SubmissionGradingHistory> FilterHistoriesForCurrentCycle(
        IReadOnlyList<SubmissionGradingHistory> allHistories,
        GradingCycleContext cycle)
        => allHistories.Where(h => IsInCurrentCycle(h, cycle)).ToList();

    private static bool HasOfficialForCycle(
        IReadOnlyList<SubmissionGradingHistory> allHistories,
        GradingCycleContext cycle)
    {
        if (cycle.Assignment.Status is AssignmentStatus.GRADED or AssignmentStatus.COMPLETED)
            return true;

        return allHistories.Any(h =>
            h.Status == SubmissionGradingHistoryStatus.OFFICIAL && IsInCurrentCycle(h, cycle));
    }

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