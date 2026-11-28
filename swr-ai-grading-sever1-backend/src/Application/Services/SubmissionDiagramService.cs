using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class SubmissionDiagramService : ISubmissionDiagramService
{
    private readonly IApplicationDbContext _db;

    public SubmissionDiagramService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ConnectSubmissionDiagramResultDTO>> ConnectSubmissionAsync(
        Guid submissionId, CancellationToken ct = default)
    {
        if (!await _db.StudentSubmissions.AnyAsync(item => item.SubmissionId == submissionId, ct))
            return Result<ConnectSubmissionDiagramResultDTO>.Failure("Submission not found.", "SUBMISSION_NOT_FOUND");

        // Each answer follows its own question's diagram: the question already knows which diagram is
        // its reference model (connected through PUT /api/questions/{id}/diagram).
        var answers = await _db.SubmissionParsedAnswers
            .Include(answer => answer.Question)
                .ThenInclude(question => question!.Diagram)
                    .ThenInclude(diagram => diagram!.Elements)
            .Where(answer => answer.SubmissionId == submissionId)
            .OrderBy(answer => answer.CreatedAt)
            .ToListAsync(ct);

        answers = answers
            .Where(answer => answer.Question?.Diagram is not null)
            .ToList();

        // An answer holds one element link, so every element of its question's diagram is a candidate.
        var connectedAnswerCount = 0;
        foreach (var answer in answers)
        {
            var diagram = answer.Question!.Diagram!;
            var elementId = DiagramElementMatcher.Match(
                diagram.Elements, answer.ExtractedText, answer.Extracted_Xml, answer.ExtractedImageUrl);
            if (elementId is null || elementId == answer.ElementId)
                continue;

            answer.ElementId = elementId;
            connectedAnswerCount++;
        }

        if (connectedAnswerCount > 0)
            await _db.SaveChangesAsync(ct);

        var questions = answers
            .Select(answer => answer.Question!)
            .DistinctBy(question => question.QuestionId)
            .OrderBy(question => question.Title)
            .ToList();

        var elementsById = answers
            .Select(answer => answer.Question!.Diagram!)
            .DistinctBy(diagram => diagram.DiagramId)
            .SelectMany(diagram => diagram.Elements)
            .GroupBy(element => element.ElementId)
            .ToDictionary(group => group.Key, group => group.First());

        return Result<ConnectSubmissionDiagramResultDTO>.Success(new ConnectSubmissionDiagramResultDTO
        {
            SubmissionId = submissionId,
            ConnectedQuestionCount = questions.Count,
            ConnectedAnswerCount = connectedAnswerCount,
            Questions = questions.Select(question => new ConnectSubmissionQuestionDTO
            {
                QuestionId = question.QuestionId,
                Title = question.Title,
                DiagramId = question.DiagramId,
                DiagramName = question.Diagram?.Name
            }).ToList(),
            Answers = answers.Select(answer =>
            {
                var element = answer.ElementId.HasValue && elementsById.TryGetValue(answer.ElementId.Value, out var found)
                    ? found
                    : null;
                return new ConnectSubmissionAnswerDTO
                {
                    ParsedAnswerId = answer.ParsedAnswerId,
                    QuestionId = answer.QuestionId,
                    DiagramId = answer.Question!.DiagramId,
                    DiagramName = answer.Question.Diagram!.Name,
                    ElementId = answer.ElementId,
                    ElementCode = element?.ElementCode,
                    ElementName = element?.Name
                };
            }).ToList()
        });
    }
}
