using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.UnitTests;

public class SubmissionGradingDiagramReferenceTests
{
    private sealed class TestDbUnitOfWork : IUnitOfWork
    {
        private readonly TestDbContext _db;
        public TestDbUnitOfWork(TestDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    private sealed record Seed(Guid SubmissionId, Guid MonitorId, Guid QuestionId);

    private static async Task<Seed> SeedAsync(TestDbContext db)
    {
        var diagram = new Diagram
        {
            DiagramId = Guid.NewGuid(),
            Name = "Use case",
            Description = "Reference use case model"
        };
        var required = new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = diagram.DiagramId,
            ElementCode = "UC01",
            Name = "Book appointment",
            Description = "Student books an appointment",
            IsRequirement = true,
            SoftwareType = "Draw.io"
        };
        var optional = new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = diagram.DiagramId,
            ElementCode = "UC02",
            Name = "Cancel booking"
        };
        required.FileTemplateUrl = DiagramElementImagePath.Build(required.ElementId, ".png");

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = Guid.NewGuid(),
            Title = "Q1",
            Content = "Draw the use case diagram.",
            Point = 10m,
            DiagramId = diagram.DiagramId,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var monitor = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON01",
            FullName = "Monitor",
            Email = "mon01@fpt.edu.vn",
            Birthday = new DateOnly(1988, 1, 1),
            PasswordHash = "hash",
            Role = UserRole.Monitor,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var gradingSession = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = Guid.NewGuid(),
            Title = "Cham dot 1",
            CreatedDate = new DateOnly(2026, 11, 2),
            UpdatedDate = new DateOnly(2026, 11, 2),
            Deadline = new DateOnly(2026, 11, 10),
            NumberSubmissions = 1
        };
        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "submissions/file.docx",
            StudentId = Guid.NewGuid(),
            UploadedById = monitor.Id,
            Status = SubmissionStatus.Submitted,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        var answer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = question.QuestionId,
            ElementId = required.ElementId,
            ExtractedText = "Actor books an appointment (UC01).",
            CreatedAt = DateTime.UtcNow
        };
        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            GradingSessionId = gradingSession.GradingSessionId,
            SubmissionId = submission.SubmissionId,
            StaffId = monitor.Id,
            Status = AssignmentStatus.IN_PROGRESS,
            AssignedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        db.Diagrams.Add(diagram);
        db.DiagramElements.AddRange(required, optional);
        db.Questions.Add(question);
        db.Staff.Add(monitor);
        db.GradingSessions.Add(gradingSession);
        db.StudentSubmissions.Add(submission);
        db.SubmissionParsedAnswers.Add(answer);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        return new Seed(submission.SubmissionId, monitor.Id, question.QuestionId);
    }

    [Fact]
    public async Task GradeWithAiAsync_SendsDiagramElementsAndMatchedElementImage()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var seed = await SeedAsync(db);

        IReadOnlyList<(Question Question, SubmissionParsedAnswer ParsedAnswer)>? sentPairs = null;
        List<DocxImageDto>? sentImages = null;

        var gemini = new Mock<IGeminiAIService>();
        gemini.Setup(g => g.GradeSubmissionAnswersAsync(
                It.IsAny<IReadOnlyList<(Question Question, SubmissionParsedAnswer ParsedAnswer)>>(),
                It.IsAny<string>(),
                It.IsAny<List<DocxImageDto>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<(Question, SubmissionParsedAnswer)>, string, List<DocxImageDto>?, CancellationToken>(
                (pairs, _, images, _) =>
                {
                    sentPairs = pairs;
                    sentImages = images;
                })
            .ReturnsAsync(new GradingResultDto
            {
                TotalScore = 6m,
                CriteriaScores =
                [
                    new CriterionScoreDto { QuestionId = seed.QuestionId, ActualScore = 6m, Comment = "" }
                ]
            });

        var storage = new Mock<ISupabaseStorage>();
        storage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileDownload
            {
                Content = new MemoryStream([1, 2, 3]),
                ContentType = "image/png",
                FileName = "image.png"
            });

        var service = new SubmissionGradingService(
            db, new TestDbUnitOfWork(db), gemini.Object, storage.Object, new Mock<IImageUrlProvider>().Object);

        var result = await service.GradeWithAiAsync(seed.SubmissionId, seed.MonitorId);

        Assert.True(result.IsSuccess, result.Error);
        Assert.NotNull(sentPairs);
        var pair = Assert.Single(sentPairs!);

        // The reference diagram and all of its elements travel with the question.
        Assert.NotNull(pair.Question.Diagram);
        Assert.Equal("Use case", pair.Question.Diagram!.Name);
        Assert.Equal(2, pair.Question.Diagram.Elements.Count);
        Assert.Contains(pair.Question.Diagram.Elements, element => element.IsRequired && element.ElementCode == "UC01");

        // The element the answer was connected to travels with the answer.
        Assert.NotNull(pair.ParsedAnswer.Element);
        Assert.Equal("UC01", pair.ParsedAnswer.Element!.ElementCode);

        // The matched element's uploaded image is sent as a labelled reference image.
        Assert.NotNull(sentImages);
        var referenceImage = Assert.Single(sentImages!);
        Assert.Equal(
            "[HÌNH ẢNH MẪU - SƠ ĐỒ Use case > Phần tử UC01 Book appointment] Câu Q1 (question_id: " +
            $"{seed.QuestionId})",
            referenceImage.Label);
    }
}
