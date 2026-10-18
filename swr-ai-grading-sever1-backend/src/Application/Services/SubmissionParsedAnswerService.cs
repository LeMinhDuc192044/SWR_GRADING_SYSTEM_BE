using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Submissions;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class SubmissionParsedAnswerService : ISubmissionParsedAnswerService
{
    private readonly IApplicationDbContext _db;

    public SubmissionParsedAnswerService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<IReadOnlyList<SubmissionParsedAnswerDTO>>> GetBySubmissionIdAsync(
        Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var submissionResult = await FindAccessibleSubmissionAsync(submissionId, currentUserId, isElevatedRole, ct);
        if (!submissionResult.IsSuccess)
            return Result<IReadOnlyList<SubmissionParsedAnswerDTO>>.Failure(submissionResult.Error!, submissionResult.ErrorCode);

        var answers = await _db.SubmissionParsedAnswers.AsNoTracking()
            .Where(answer => answer.SubmissionId == submissionId)
            .OrderBy(answer => answer.CreatedAt)
            .ToListAsync(ct);
        return Result<IReadOnlyList<SubmissionParsedAnswerDTO>>.Success(answers.Select(ToDto).ToList());
    }

    public async Task<Result<SubmissionParsedAnswerDTO>> GetByIdAsync(
        Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var answer = await _db.SubmissionParsedAnswers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ParsedAnswerId == id, ct);
        if (answer is null)
            return Result<SubmissionParsedAnswerDTO>.Failure("Parsed answer not found.", "PARSED_ANSWER_NOT_FOUND");

        var submissionResult = await FindAccessibleSubmissionAsync(answer.SubmissionId, currentUserId, isElevatedRole, ct);
        return submissionResult.IsSuccess
            ? Result<SubmissionParsedAnswerDTO>.Success(ToDto(answer))
            : Result<SubmissionParsedAnswerDTO>.Failure(submissionResult.Error!, submissionResult.ErrorCode);
    }

    public async Task<Result<SubmissionParsedAnswerDTO>> CreateAsync(
        SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var validation = await ValidateRequestAsync(request, currentUserId, isElevatedRole, ct);
        if (!validation.IsSuccess)
            return Result<SubmissionParsedAnswerDTO>.Failure(validation.Error!, validation.ErrorCode);

        if (await _db.SubmissionParsedAnswers.AnyAsync(answer =>
                answer.SubmissionId == request.SubmissionId && answer.QuestionId == request.QuestionId, ct))
            return Result<SubmissionParsedAnswerDTO>.Failure("This question already has a parsed answer for the submission.", "PARSED_ANSWER_EXISTS");

        var answer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = request.SubmissionId,
            QuestionId = request.QuestionId,
            ElementId = request.ElementId,
            ExtractedText = request.ExtractedText,
            ExtractedImageUrl = request.ExtractedImageUrl,
            CreatedAt = DateTime.UtcNow
        };
        _db.SubmissionParsedAnswers.Add(answer);
        await _db.SaveChangesAsync(ct);
        return Result<SubmissionParsedAnswerDTO>.Success(ToDto(answer));
    }

    public async Task<Result<SubmissionParsedAnswerDTO>> UpdateAsync(
        Guid id, SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var answer = await _db.SubmissionParsedAnswers
            .FirstOrDefaultAsync(item => item.ParsedAnswerId == id, ct);
        if (answer is null)
            return Result<SubmissionParsedAnswerDTO>.Failure("Parsed answer not found.", "PARSED_ANSWER_NOT_FOUND");

        var currentAccess = await FindAccessibleSubmissionAsync(answer.SubmissionId, currentUserId, isElevatedRole, ct);
        if (!currentAccess.IsSuccess)
            return Result<SubmissionParsedAnswerDTO>.Failure(currentAccess.Error!, currentAccess.ErrorCode);

        var validation = await ValidateRequestAsync(request, currentUserId, isElevatedRole, ct);
        if (!validation.IsSuccess)
            return Result<SubmissionParsedAnswerDTO>.Failure(validation.Error!, validation.ErrorCode);

        if (await _db.SubmissionParsedAnswers.AnyAsync(item =>
                item.ParsedAnswerId != id && item.SubmissionId == request.SubmissionId && item.QuestionId == request.QuestionId, ct))
            return Result<SubmissionParsedAnswerDTO>.Failure("This question already has a parsed answer for the submission.", "PARSED_ANSWER_EXISTS");

        answer.SubmissionId = request.SubmissionId;
        answer.QuestionId = request.QuestionId;
        answer.ElementId = request.ElementId;
        answer.ExtractedText = request.ExtractedText;
        answer.ExtractedImageUrl = request.ExtractedImageUrl;
        await _db.SaveChangesAsync(ct);
        return Result<SubmissionParsedAnswerDTO>.Success(ToDto(answer));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var answer = await _db.SubmissionParsedAnswers
            .FirstOrDefaultAsync(item => item.ParsedAnswerId == id, ct);
        if (answer is null)
            return Result.Failure("Parsed answer not found.", "PARSED_ANSWER_NOT_FOUND");

        var submissionResult = await FindAccessibleSubmissionAsync(answer.SubmissionId, currentUserId, isElevatedRole, ct);
        if (!submissionResult.IsSuccess)
            return Result.Failure(submissionResult.Error!, submissionResult.ErrorCode);

        _db.SubmissionParsedAnswers.Remove(answer);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result<StudentSubmission>> ValidateRequestAsync(
        SaveSubmissionParsedAnswerRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct)
    {
        if (request.SubmissionId == Guid.Empty || request.QuestionId == Guid.Empty)
            return Result<StudentSubmission>.Failure("SubmissionId and QuestionId are required.", "INVALID_REQUEST");

        var submissionResult = await FindAccessibleSubmissionAsync(request.SubmissionId, currentUserId, isElevatedRole, ct);
        if (!submissionResult.IsSuccess)
            return submissionResult;

        var paperSetId = submissionResult.Data!.GradingDiary.PaperSetId;
        if (!await _db.Questions.AnyAsync(question =>
                question.QuestionId == request.QuestionId && question.PaperSetId == paperSetId, ct))
            return Result<StudentSubmission>.Failure("Question does not belong to the paper set for this submission.", "QUESTION_NOT_IN_PAPER_SET");

        if (request.ElementId.HasValue &&
            !await _db.DiagramElements.AnyAsync(element => element.ElementId == request.ElementId.Value, ct))
            return Result<StudentSubmission>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        return submissionResult;
    }

    private async Task<Result<StudentSubmission>> FindAccessibleSubmissionAsync(
        Guid submissionId, Guid currentUserId, bool isElevatedRole, CancellationToken ct)
    {
        var submission = await _db.StudentSubmissions
            .Include(item => item.GradingDiary)
            .FirstOrDefaultAsync(item => item.SubmissionId == submissionId, ct);
        if (submission is null)
            return Result<StudentSubmission>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");
        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
            return Result<StudentSubmission>.Failure("You are not allowed to access this submission.", "FORBIDDEN");
        return Result<StudentSubmission>.Success(submission);
    }

    private static SubmissionParsedAnswerDTO ToDto(SubmissionParsedAnswer answer) => new()
    {
        ParsedAnswerId = answer.ParsedAnswerId,
        SubmissionId = answer.SubmissionId,
        QuestionId = answer.QuestionId,
        ElementId = answer.ElementId,
        ExtractedText = answer.ExtractedText,
        ExtractedImageUrl = answer.ExtractedImageUrl,
        CreatedAt = answer.CreatedAt
    };
}