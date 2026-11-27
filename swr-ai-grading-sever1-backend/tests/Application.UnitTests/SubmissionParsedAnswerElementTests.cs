using Application.Common.Interfaces;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Moq;

namespace Application.UnitTests;

public class SubmissionParsedAnswerElementTests
{
    private static SubmissionParsedAnswerService CreateService(TestDbContext db) =>
        new(db, new Mock<ISupabaseStorage>().Object, new Mock<IImageUrlProvider>().Object);

    private sealed record Seed(
        TestDbContext Db,
        Guid AnswerId,
        Guid ElementId,
        Guid ForeignElementId,
        Guid NoDiagramAnswerId);

    private static async Task<Seed> SeedAsync()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var diagram = new Diagram { DiagramId = Guid.NewGuid(), Name = "Use case" };
        var otherDiagram = new Diagram { DiagramId = Guid.NewGuid(), Name = "ERD" };
        var element = new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = diagram.DiagramId,
            ElementCode = "UC01",
            Name = "Book appointment"
        };
        var foreignElement = new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = otherDiagram.DiagramId,
            ElementCode = "E01",
            Name = "Customer"
        };

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            Title = "Question 1",
            Content = "Draw the use case diagram.",
            Point = 5m,
            PaperSetId = Guid.NewGuid(),
            DiagramId = diagram.DiagramId
        };
        var questionWithoutDiagram = new Question
        {
            QuestionId = Guid.NewGuid(),
            Title = "Question 2",
            Content = "Explain the actors.",
            Point = 5m,
            PaperSetId = question.PaperSetId
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "submissions/file.docx",
            StudentId = Guid.NewGuid(),
            UploadedById = Guid.NewGuid()
        };

        var answer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = question.QuestionId,
            ExtractedText = "Contains UC01",
            Question = question,
            CreatedAt = DateTime.UtcNow
        };
        var noDiagramAnswer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = questionWithoutDiagram.QuestionId,
            ExtractedText = "Actors: customer, staff",
            Question = questionWithoutDiagram,
            CreatedAt = DateTime.UtcNow
        };

        db.Diagrams.AddRange(diagram, otherDiagram);
        db.DiagramElements.AddRange(element, foreignElement);
        db.Questions.AddRange(question, questionWithoutDiagram);
        db.StudentSubmissions.Add(submission);
        db.SubmissionParsedAnswers.AddRange(answer, noDiagramAnswer);
        await db.SaveChangesAsync();

        return new Seed(db, answer.ParsedAnswerId, element.ElementId, foreignElement.ElementId, noDiagramAnswer.ParsedAnswerId);
    }

    [Fact]
    public async Task AssignElementAsync_ElementOfQuestionDiagram_LinksElement()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await CreateService(seed.Db)
            .AssignElementAsync(seed.AnswerId, seed.ElementId, Guid.NewGuid(), isElevatedRole: true);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(seed.ElementId, result.Data!.ElementId);
        Assert.Equal("UC01", result.Data.ElementCode);
        Assert.Equal("Book appointment", result.Data.ElementName);
    }

    [Fact]
    public async Task AssignElementAsync_ElementOfAnotherDiagram_ReturnsFailure()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await CreateService(seed.Db)
            .AssignElementAsync(seed.AnswerId, seed.ForeignElementId, Guid.NewGuid(), isElevatedRole: true);

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_NOT_IN_QUESTION_DIAGRAM", result.ErrorCode);
    }

    [Fact]
    public async Task AssignElementAsync_QuestionWithoutDiagram_ReturnsFailure()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;

        var result = await CreateService(seed.Db)
            .AssignElementAsync(seed.NoDiagramAnswerId, seed.ElementId, Guid.NewGuid(), isElevatedRole: true);

        Assert.False(result.IsSuccess);
        Assert.Equal("QUESTION_HAS_NO_DIAGRAM", result.ErrorCode);
    }

    [Fact]
    public async Task AssignElementAsync_NullElement_RemovesLink()
    {
        var seed = await SeedAsync();
        using var _ = seed.Db;
        var service = CreateService(seed.Db);

        var linked = await service.AssignElementAsync(seed.AnswerId, seed.ElementId, Guid.NewGuid(), isElevatedRole: true);
        Assert.True(linked.IsSuccess);

        var cleared = await service.AssignElementAsync(seed.AnswerId, null, Guid.NewGuid(), isElevatedRole: true);

        Assert.True(cleared.IsSuccess);
        Assert.Null(cleared.Data!.ElementId);
        Assert.Null(cleared.Data.ElementCode);
    }
}
