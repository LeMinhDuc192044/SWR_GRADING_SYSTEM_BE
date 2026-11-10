using Application.Common;
using Application.DTOs.Grading;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Moq;

namespace Application.UnitTests;

public class SubmissionGradingServiceTests
{
    private sealed class TestDbUnitOfWork : IUnitOfWork
    {
        private readonly TestDbContext _db;
        public TestDbUnitOfWork(TestDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    private static SubmissionGradingService CreateService(TestDbContext db)
    {
        var gemini = new Mock<IGeminiAIService>();
        var storage = new Mock<ISupabaseStorage>();
        return new SubmissionGradingService(db, new TestDbUnitOfWork(db), gemini.Object, storage.Object);
    }

    private static async Task<(
        Guid SubmissionId,
        Guid Monitor1Id,
        Guid Monitor2Id,
        Guid ParsedAnswer1Id,
        Guid ParsedAnswer2Id,
        Guid Round1OfficialHistoryId)> SeedRound1FinalizedAsync(TestDbContext db, string regradeTitle = "[Phúc khảo] Dot 2")
    {
        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = SemesterStatus.Active
        };
        var monitor1 = CreateMonitor("MON01");
        var monitor2 = CreateMonitor("MON02");
        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "EX01",
            FullName = "Examiner",
            Email = "ex01@fpt.edu.vn",
            Birthday = new DateOnly(1990, 1, 1),
            PasswordHash = "hash",
            Role = UserRole.Examiner,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180001",
            FullName = "Nguyen Van A",
            Email = "se180001@fpt.edu.vn",
            Birthday = new DateOnly(2004, 1, 1),
            PasswordHash = "hash",
            Major = "SE",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var paperSet = new PaperSet
        {
            PaperSetId = Guid.NewGuid(),
            PaperSetCode = "PS01",
            Description = "Paper Set",
            UploadedById = examiner.Id,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            Status = PaperSetStatus.Ready,
            TotalQuestions = 2
        };
        var q1 = new Question
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = paperSet.PaperSetId,
            Title = "Q1",
            Content = "Use Case Diagram",
            Point = 5m,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var q2 = new Question
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = paperSet.PaperSetId,
            Title = "Q2",
            Content = "Class Diagram",
            Point = 5m,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = "SWR PE",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationStatus.Ongoing,
            SemesterId = semester.SemesterId
        };
        var examSession = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationCode = "ROOM_101",
            StartDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationSessionStatus.STARTED,
            SemesterId = semester.SemesterId,
            ExaminationId = exam.ExaminationId,
            PaperSetId = paperSet.PaperSetId
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "se180001.docx",
            StudentId = student.Id,
            UploadedById = examiner.Id,
            ExaminationSessionId = examSession.ExaminationSessionId,
            Status = SubmissionStatus.Lecturer_Reviewed,
            CreatedDate = DateTime.UtcNow.AddDays(-3),
            UpdatedDate = DateTime.UtcNow.AddDays(-2)
        };

        var pa1 = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = q1.QuestionId,
            ExtractedText = "Student answer Q1",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        var pa2 = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = q2.QuestionId,
            ExtractedText = "Student answer Q2",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        var session1 = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = examSession.ExaminationSessionId,
            Title = "Cham dot 1",
            CreatedDate = new DateOnly(2026, 11, 2),
            UpdatedDate = new DateOnly(2026, 11, 2),
            Deadline = new DateOnly(2026, 11, 5),
            NumberSubmissions = 1
        };
        var session2 = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = examSession.ExaminationSessionId,
            Title = regradeTitle,
            CreatedDate = new DateOnly(2026, 11, 6),
            UpdatedDate = new DateOnly(2026, 11, 6),
            Deadline = new DateOnly(2026, 11, 10),
            NumberSubmissions = 1
        };

        var assign1 = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            GradingSessionId = session1.GradingSessionId,
            SubmissionId = submission.SubmissionId,
            StaffId = monitor1.Id,
            Status = AssignmentStatus.GRADED,
            AssignedDate = DateTime.UtcNow.AddDays(-3),
            UpdatedDate = DateTime.UtcNow.AddDays(-2)
        };
        var assign2 = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            GradingSessionId = session2.GradingSessionId,
            SubmissionId = submission.SubmissionId,
            StaffId = monitor2.Id,
            Status = AssignmentStatus.IN_PROGRESS,
            AssignedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var aiHistory = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.AI,
            GradingRound = 1,
            TotalScore = 6.0m,
            GeneralFeedback = "AI Round 1 Secret Feedback",
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow.AddDays(-3).AddHours(1),
            Details =
            [
                new SubmissionItemDetail { ParsedAnswerId = pa1.ParsedAnswerId, Score = 3m, Comment = "AI Q1" },
                new SubmissionItemDetail { ParsedAnswerId = pa2.ParsedAnswerId, Score = 3m, Comment = "AI Q2" }
            ]
        };
        var lecturerOfficial = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 2,
            TotalScore = 5.5m,
            GeneralFeedback = "Monitor 1 Secret Feedback",
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Details =
            [
                new SubmissionItemDetail { ParsedAnswerId = pa1.ParsedAnswerId, Score = 2.5m, Comment = "M1 Q1" },
                new SubmissionItemDetail { ParsedAnswerId = pa2.ParsedAnswerId, Score = 3.0m, Comment = "M1 Q2" }
            ]
        };

        db.Semesters.Add(semester);
        db.Staff.AddRange(monitor1, monitor2, examiner);
        db.Students.Add(student);
        db.PaperSets.Add(paperSet);
        db.Questions.AddRange(q1, q2);
        db.Examinations.Add(exam);
        db.ExaminationSessions.Add(examSession);
        db.StudentSubmissions.Add(submission);
        db.SubmissionParsedAnswers.AddRange(pa1, pa2);
        db.GradingSessions.AddRange(session1, session2);
        db.Assignments.AddRange(assign1, assign2);
        db.SubmissionGradingHistories.AddRange(aiHistory, lecturerOfficial);
        await db.SaveChangesAsync();

        return (submission.SubmissionId, monitor1.Id, monitor2.Id, pa1.ParsedAnswerId, pa2.ParsedAnswerId, lecturerOfficial.HistoryId);
    }

    private static Staff CreateMonitor(string code) => new()
    {
        Id = Guid.NewGuid(),
        StaffCode = code,
        FullName = code,
        Email = $"{code.ToLower()}@fpt.edu.vn",
        Birthday = new DateOnly(1988, 1, 1),
        PasswordHash = "hash",
        Role = UserRole.Monitor,
        IsActive = true,
        IsDeleted = false,
        CreatedDay = DateTime.UtcNow,
        UpdatedDay = DateTime.UtcNow
    };

    [Fact]
    public async Task GetHistoriesAsync_BlindRegradeMonitor_HidesRound1ScoresAndShowsParsedAnswers()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (subId, _, monitor2Id, _, _, _) = await SeedRound1FinalizedAsync(db);
        var service = CreateService(db);

        var result = await service.GetHistoriesAsync(subId, monitor2Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Histories); // Ẩn toàn bộ lịch sử điểm & feedback của Round 1!
        Assert.Equal(2, result.Data.ParsedAnswers.Count); // Vẫn thấy đầy đủ bài làm của sinh viên để chấm
    }

    [Fact]
    public async Task ReviewAndFinalize_BlindRegradeBlocksReferencingRound1History()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (subId, _, monitor2Id, pa1Id, _, round1HistoryId) = await SeedRound1FinalizedAsync(db);
        var service = CreateService(db);

        var reviewAttempt = await service.ReviewAsync(subId, new ReviewGradingRequest
        {
            BaseHistoryId = round1HistoryId,
            Items = [new ReviewItemRequest { ParsedAnswerId = pa1Id, Score = 4m }]
        }, monitor2Id);

        Assert.False(reviewAttempt.IsSuccess);
        Assert.Equal("FORBIDDEN", reviewAttempt.ErrorCode);

        var finalizeAttempt = await service.FinalizeAsync(subId, round1HistoryId, monitor2Id);
        Assert.False(finalizeAttempt.IsSuccess);
        Assert.Equal("FORBIDDEN", finalizeAttempt.ErrorCode);
    }

    [Fact]
    public async Task ReviewAndFinalize_BlindRegradeMonitorGradesIndependentlyAndFinalizesSuccessfully()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (subId, monitor1Id, monitor2Id, pa1Id, pa2Id, _) = await SeedRound1FinalizedAsync(db);
        var service = CreateService(db);

        // Monitor 2 chấm mù độc lập (chỉ gửi Q1 = 4.5, không gửi Q2 -> Q2 mặc định 0 chứ không kế thừa 3.0 từ Round 1!)
        var draftResult = await service.ReviewAsync(subId, new ReviewGradingRequest
        {
            GeneralFeedback = "Regrade feedback by Monitor 2",
            Items = [new ReviewItemRequest { ParsedAnswerId = pa1Id, Score = 4.5m, Comment = "Good Q1" }]
        }, monitor2Id);

        Assert.True(draftResult.IsSuccess);
        Assert.Equal(3, draftResult.Data!.GradingRound);
        Assert.Equal(4.5m, draftResult.Data.TotalScore); // 4.5 + 0 = 4.5 (không dính 3.0 từ Round 1)
        Assert.Equal(SubmissionGradingHistoryStatus.DRAFT, draftResult.Data.Status);

        // Monitor 2 cập nhật đủ Q1 = 4.5, Q2 = 4.0 -> 8.5
        var updatedDraft = await service.ReviewAsync(subId, new ReviewGradingRequest
        {
            BaseHistoryId = draftResult.Data.HistoryId,
            GeneralFeedback = "Updated regrade feedback",
            Items = [new ReviewItemRequest { ParsedAnswerId = pa2Id, Score = 4.0m, Comment = "Good Q2" }]
        }, monitor2Id);

        Assert.True(updatedDraft.IsSuccess);
        Assert.Equal(8.5m, updatedDraft.Data!.TotalScore);

        // Monitor 2 chốt điểm Round 2 thành công mặc dù Round 1 đã có bản OFFICIAL!
        var finalized = await service.FinalizeAsync(subId, updatedDraft.Data.HistoryId, monitor2Id);
        Assert.True(finalized.IsSuccess);
        Assert.Equal(SubmissionGradingHistoryStatus.OFFICIAL, finalized.Data!.Status);
        Assert.Equal(8.5m, finalized.Data.TotalScore);

        // Sau khi chốt Round 2 xong, Monitor 2 không thể chấm lại lần nữa
        var secondFinalizeAttempt = await service.ReviewAsync(subId, new ReviewGradingRequest
        {
            Items = [new ReviewItemRequest { ParsedAnswerId = pa1Id, Score = 5m }]
        }, monitor2Id);
        Assert.False(secondFinalizeAttempt.IsSuccess);
        Assert.Equal("ALREADY_FINALIZED", secondFinalizeAttempt.ErrorCode);

        // Monitor 1 cũng vẫn bị khóa vì Assignment 1 đã GRADED
        var monitor1Attempt = await service.FinalizeAsync(subId, updatedDraft.Data.HistoryId, monitor1Id);
        Assert.False(monitor1Attempt.IsSuccess);
        Assert.Equal("ALREADY_FINALIZED", monitor1Attempt.ErrorCode);

        // Monitor 2 gọi GetHistoriesAsync chỉ thấy lịch sử chấm của chính đợt 2 (Rounds 3, 4), vẫn ẩn Round 1 & 2!
        var monitor2Overview = await service.GetHistoriesAsync(subId, monitor2Id);
        Assert.True(monitor2Overview.IsSuccess);
        Assert.All(monitor2Overview.Data!.Histories, h => Assert.True(h.GradingRound >= 3));
        Assert.DoesNotContain(monitor2Overview.Data.Histories, h => h.GradingRound <= 2);
    }
}

