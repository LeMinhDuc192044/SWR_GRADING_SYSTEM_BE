using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;

namespace Application.UnitTests;

public class SubmissionDiagramConnectTests
{
    private sealed record Seed(
        TestDbContext Db,
        Guid UseCaseElementId,
        Guid ErdElementId,
        Guid SubmissionId,
        Guid UseCaseQuestionId,
        Guid ErdQuestionId,
        Guid NoDiagramQuestionId,
        Guid UseCaseAnswerId,
        Guid ErdAnswerId,
        Guid NoDiagramAnswerId);

    /// <summary>
    /// Two questions have their own diagrams (use case, ERD) and one question has none. Each answer
    /// must be matched against its own question's diagram only.
    /// </summary>
    private static async Task<Seed> SeedAsync(bool preassignUseCaseAnswerToErdElement = false)
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var useCase = new Diagram { DiagramId = Guid.NewGuid(), Name = "Use case" };
        var erd = new Diagram { DiagramId = Guid.NewGuid(), Name = "ERD" };

        var useCaseElement = new DiagramElement
        {
            ElementId = Guid.NewGuid(), DiagramId = useCase.DiagramId, ElementCode = "UC01", Name = "Book appointment"
        };
        var useCaseOtherElement = new DiagramElement
        {
            ElementId = Guid.NewGuid(), DiagramId = useCase.DiagramId, ElementCode = "UC02", Name = "Cancel booking"
        };
        var erdElement = new DiagramElement
        {
            ElementId = Guid.NewGuid(), DiagramId = erd.DiagramId, ElementCode = "ERD01", Name = "Customer table"
        };

        var paperSetId = Guid.NewGuid();
        var useCaseQuestion = new Question
        {
            QuestionId = Guid.NewGuid(), Title = "Question 1", Content = "Draw the use case diagram.",
            Point = 4m, PaperSetId = paperSetId, DiagramId = useCase.DiagramId
        };
        var erdQuestion = new Question
        {
            QuestionId = Guid.NewGuid(), Title = "Question 2", Content = "Draw the ERD.",
            Point = 3m, PaperSetId = paperSetId, DiagramId = erd.DiagramId
        };
        var noDiagramQuestion = new Question
        {
            QuestionId = Guid.NewGuid(), Title = "Question 3", Content = "Explain the actors.",
            Point = 3m, PaperSetId = paperSetId
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(), FileSubmissions = "submissions/file.docx",
            StudentId = Guid.NewGuid(), UploadedById = Guid.NewGuid()
        };

        var useCaseAnswer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(), SubmissionId = submission.SubmissionId,
            QuestionId = useCaseQuestion.QuestionId, ExtractedText = "Actor student uses UC01.",
            ElementId = preassignUseCaseAnswerToErdElement ? erdElement.ElementId : null,
            CreatedAt = DateTime.UtcNow
        };
        var erdAnswer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(), SubmissionId = submission.SubmissionId,
            QuestionId = erdQuestion.QuestionId, ExtractedText = "ERD01 Customer table holds bookings.",
            CreatedAt = DateTime.UtcNow.AddMinutes(1)
        };
        var noDiagramAnswer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(), SubmissionId = submission.SubmissionId,
            QuestionId = noDiagramQuestion.QuestionId, ExtractedText = "The main actor is the student.",
            CreatedAt = DateTime.UtcNow.AddMinutes(2)
        };

        db.Diagrams.AddRange(useCase, erd);
        db.DiagramElements.AddRange(useCaseElement, useCaseOtherElement, erdElement);
        db.Questions.AddRange(useCaseQuestion, erdQuestion, noDiagramQuestion);
        db.StudentSubmissions.Add(submission);
        db.SubmissionParsedAnswers.AddRange(useCaseAnswer, erdAnswer, noDiagramAnswer);
        await db.SaveChangesAsync();

        return new Seed(db, useCaseElement.ElementId, erdElement.ElementId,
            submission.SubmissionId, useCaseQuestion.QuestionId, erdQuestion.QuestionId,
            noDiagramQuestion.QuestionId, useCaseAnswer.ParsedAnswerId, erdAnswer.ParsedAnswerId,
            noDiagramAnswer.ParsedAnswerId);
    }

    [Fact]
    public async Task ConnectSubmissionAsync_UsesEachQuestionDiagramForItsAnswers()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await new SubmissionDiagramService(seed.Db)
            .ConnectSubmissionAsync(seed.SubmissionId);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Data!.ConnectedQuestionCount);
        Assert.Equal(2, result.Data.ConnectedAnswerCount);
        Assert.Equal(2, result.Data.Questions.Count);

        var useCaseAnswer = result.Data.Answers.Single(answer => answer.ParsedAnswerId == seed.UseCaseAnswerId);
        Assert.Equal(seed.UseCaseQuestionId, useCaseAnswer.QuestionId);
        Assert.Equal("Use case", useCaseAnswer.DiagramName);
        Assert.Equal(seed.UseCaseElementId, useCaseAnswer.ElementId);
        Assert.Equal("UC01", useCaseAnswer.ElementCode);

        var erdAnswer = result.Data.Answers.Single(answer => answer.ParsedAnswerId == seed.ErdAnswerId);
        Assert.Equal("ERD", erdAnswer.DiagramName);
        Assert.Equal(seed.ErdElementId, erdAnswer.ElementId);
        Assert.Equal("ERD01", erdAnswer.ElementCode);
    }

    [Fact]
    public async Task ConnectSubmissionAsync_SkipsAnswersOfQuestionsWithoutDiagram()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await new SubmissionDiagramService(seed.Db)
            .ConnectSubmissionAsync(seed.SubmissionId);

        Assert.True(result.IsSuccess, result.Error);
        Assert.DoesNotContain(result.Data!.Answers, answer => answer.ParsedAnswerId == seed.NoDiagramAnswerId);
        Assert.DoesNotContain(result.Data.Questions, question => question.QuestionId == seed.NoDiagramQuestionId);

        var stored = await seed.Db.SubmissionParsedAnswers.FindAsync(seed.NoDiagramAnswerId);
        Assert.Null(stored!.ElementId);
    }

    [Fact]
    public async Task ConnectSubmissionAsync_RematchesAnExistingElementFromAnotherDiagram()
    {
        var seed = await SeedAsync(preassignUseCaseAnswerToErdElement: true);
        using var _ = seed.Db;

        var result = await new SubmissionDiagramService(seed.Db)
            .ConnectSubmissionAsync(seed.SubmissionId);

        Assert.True(result.IsSuccess, result.Error);
        var useCaseAnswer = result.Data!.Answers.Single(answer => answer.ParsedAnswerId == seed.UseCaseAnswerId);
        Assert.Equal(seed.UseCaseElementId, useCaseAnswer.ElementId);
        Assert.Equal("UC01", useCaseAnswer.ElementCode);
    }

    [Fact]
    public async Task ConnectSubmissionAsync_UnknownSubmission_ReturnsSubmissionNotFound()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await new SubmissionDiagramService(seed.Db)
            .ConnectSubmissionAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("SUBMISSION_NOT_FOUND", result.ErrorCode);
    }
}
