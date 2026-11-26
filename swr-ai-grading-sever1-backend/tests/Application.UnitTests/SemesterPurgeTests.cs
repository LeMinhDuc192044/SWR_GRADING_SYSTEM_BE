using Application.Common.Interfaces;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests;

public class SemesterPurgeTests
{
    [Fact]
    public async Task PurgeAsync_DeletesSemesterAndEverythingCreatedUnderIt()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "FA24",
            Name = "Fall 2024",
            StartDate = new DateOnly(2024, 9, 1),
            EndDate = new DateOnly(2024, 12, 31),
            Status = SemesterStatus.Active,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var examination = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            Name = "SWR Final",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var paperSet = new PaperSet
        {
            PaperSetId = Guid.NewGuid(),
            PaperSetCode = "PS1",
            ExaminationId = examination.ExaminationId,
            FileQuestionDocs = "paper-sets/PS1/EQ/questions.docx",
            TotalQuestions = 1,
            UploadedById = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = paperSet.PaperSetId,
            Title = "Q1",
            Content = "Draw the diagram",
            Point = 1,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            ExaminationId = examination.ExaminationId,
            PaperSetId = paperSet.PaperSetId,
            ExaminationCode = "SWR-01",
            StartDate = new DateOnly(2024, 10, 1),
            StartTime = new TimeOnly(8, 0),
            Status = ExaminationSessionStatus.STARTED,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180001",
            FullName = "Nguyen Van A",
            Email = "se180001@fpt.edu.vn",
            Major = "SE",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "submissions/s1/answer.zip",
            StudentId = student.Id,
            ExaminationSessionId = session.ExaminationSessionId,
            UploadedById = Guid.NewGuid(),
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var parsedAnswer = new SubmissionParsedAnswer
        {
            ParsedAnswerId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            QuestionId = question.QuestionId,
            ExtractedImageUrl = "submissions/s1/images/diagram.png",
            CreatedAt = DateTime.UtcNow
        };

        var history = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 1,
            TotalScore = 8,
            CreatedAt = DateTime.UtcNow
        };

        var detail = new SubmissionItemDetail
        {
            DetailId = Guid.NewGuid(),
            EvaluationId = history.HistoryId,
            ParsedAnswerId = parsedAnswer.ParsedAnswerId,
            Score = 1,
            Comment = "ok"
        };

        var gradingSession = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = session.ExaminationSessionId,
            Title = "SWR grading",
            CreatedDate = new DateOnly(2024, 10, 2),
            UpdatedDate = new DateOnly(2024, 10, 2)
        };

        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            StaffId = Guid.NewGuid(),
            GradingSessionId = gradingSession.GradingSessionId,
            SubmissionId = submission.SubmissionId,
            AssignedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var application = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            SubmissionId = submission.SubmissionId,
            reason = "Please regrade",
            Status = ApplicationStatus.PENDING,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        db.Semesters.Add(semester);
        db.Examinations.Add(examination);
        db.PaperSets.Add(paperSet);
        db.Questions.Add(question);
        db.ExaminationSessions.Add(session);
        db.Students.Add(student);
        db.StudentSubmissions.Add(submission);
        db.SubmissionParsedAnswers.Add(parsedAnswer);
        db.SubmissionGradingHistories.Add(history);
        db.SubmissionItemDetails.Add(detail);
        db.GradingSessions.Add(gradingSession);
        db.Assignments.Add(assignment);
        db.Applications.Add(application);
        db.StudentSemesters.Add(new StudentSemester
        {
            StudentId = student.Id,
            SemesterId = semester.SemesterId,
            StaffId = Guid.NewGuid()
        });
        db.StudentExaminationSessions.Add(new StudentExaminationSession
        {
            StudentId = student.Id,
            ExaminationSessionId = session.ExaminationSessionId
        });
        await db.SaveChangesAsync();

        var semesterRepository = new Mock<ISemesterRepository>();
        semesterRepository
            .Setup(repository => repository.GetByIdAsync(semester.SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(semester);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => db.SaveChangesAsync(token));

        var storage = new Mock<ISupabaseStorage>();
        storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SemesterService(
            semesterRepository.Object,
            new Mock<IExaminationRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            new Mock<IUserRepository>().Object,
            db,
            unitOfWork.Object,
            storage.Object);

        var result = await service.PurgeAsync(semester.SemesterId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.Examinations);
        Assert.Equal(1, result.Data.ExaminationSessions);
        Assert.Equal(1, result.Data.PaperSetsReleased);
        Assert.Equal(1, result.Data.GradingSessions);
        Assert.Equal(1, result.Data.Assignments);
        Assert.Equal(1, result.Data.Submissions);
        Assert.Equal(1, result.Data.ParsedAnswers);
        Assert.Equal(1, result.Data.GradingHistories);
        Assert.Equal(1, result.Data.Applications);
        Assert.Equal(1, result.Data.StudentSemesters);
        Assert.Equal(2, result.Data.StoredFilesDeleted);
        Assert.Equal(0, result.Data.StoredFilesFailed);

        // The semester and its paper sets survive; the paper set returns to Ready and can be reused.
        Assert.Single(await db.Semesters.ToListAsync());
        Assert.Empty(await db.Examinations.ToListAsync());
        Assert.Empty(await db.ExaminationSessions.ToListAsync());
        var remainingPaperSet = Assert.Single(await db.PaperSets.ToListAsync());
        Assert.Null(remainingPaperSet.ExaminationId);
        Assert.Equal(PaperSetStatus.Ready, remainingPaperSet.Status);
        Assert.Single(await db.Questions.ToListAsync());
        Assert.Empty(await db.GradingSessions.ToListAsync());
        Assert.Empty(await db.Assignments.ToListAsync());
        Assert.Empty(await db.StudentSubmissions.ToListAsync());
        Assert.Empty(await db.SubmissionParsedAnswers.ToListAsync());
        Assert.Empty(await db.SubmissionGradingHistories.ToListAsync());
        Assert.Empty(await db.SubmissionItemDetails.ToListAsync());
        Assert.Empty(await db.Applications.ToListAsync());
        Assert.Empty(await db.StudentSemesters.ToListAsync());
        Assert.Empty(await db.StudentExaminationSessions.ToListAsync());

        // Paper set files are kept because the paper set itself is kept.
        storage.Verify(s => s.DeleteAsync("paper-sets/PS1/EQ/questions.docx", It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(s => s.DeleteAsync("submissions/s1/answer.zip", It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(s => s.DeleteAsync("submissions/s1/images/diagram.png", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PurgeAsync_ReleasesPaperSetUsedOnlyByAnExaminationSession()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "SU24",
            Name = "Summer 2024",
            StartDate = new DateOnly(2024, 6, 1),
            EndDate = new DateOnly(2024, 8, 31),
            Status = SemesterStatus.Active,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var examination = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            Name = "SWR Midterm",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        // Not linked to the examination, only to the session that uses it.
        var paperSet = new PaperSet
        {
            PaperSetId = Guid.NewGuid(),
            PaperSetCode = "PS2",
            ExaminationId = null,
            Status = PaperSetStatus.Used,
            FileQuestionDocs = "paper-sets/PS2/EQ/questions.docx",
            TotalQuestions = 1,
            UploadedById = Guid.NewGuid(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            ExaminationId = examination.ExaminationId,
            PaperSetId = paperSet.PaperSetId,
            ExaminationCode = "SWR-02",
            StartDate = new DateOnly(2024, 7, 1),
            StartTime = new TimeOnly(8, 0),
            Status = ExaminationSessionStatus.STARTED,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        db.Semesters.Add(semester);
        db.Examinations.Add(examination);
        db.PaperSets.Add(paperSet);
        db.ExaminationSessions.Add(session);
        await db.SaveChangesAsync();

        var semesterRepository = new Mock<ISemesterRepository>();
        semesterRepository
            .Setup(repository => repository.GetByIdAsync(semester.SemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(semester);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => db.SaveChangesAsync(token));

        var storage = new Mock<ISupabaseStorage>();
        storage
            .Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new SemesterService(
            semesterRepository.Object,
            new Mock<IExaminationRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            new Mock<IUserRepository>().Object,
            db,
            unitOfWork.Object,
            storage.Object);

        var result = await service.PurgeAsync(semester.SemesterId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.PaperSetsReleased);
        Assert.Single(await db.Semesters.ToListAsync());
        Assert.Empty(await db.ExaminationSessions.ToListAsync());

        var remainingPaperSet = Assert.Single(await db.PaperSets.ToListAsync());
        Assert.Null(remainingPaperSet.ExaminationId);
        Assert.Equal(PaperSetStatus.Ready, remainingPaperSet.Status);

        storage.Verify(s => s.DeleteAsync("paper-sets/PS2/EQ/questions.docx", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PurgeAsync_ReturnsNotFoundForUnknownSemester()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var semesterRepository = new Mock<ISemesterRepository>();
        semesterRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Semester?)null);

        var service = new SemesterService(
            semesterRepository.Object,
            new Mock<IExaminationRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            new Mock<IUserRepository>().Object,
            db,
            new Mock<IUnitOfWork>().Object,
            new Mock<ISupabaseStorage>().Object);

        var result = await service.PurgeAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("SEMESTER_NOT_FOUND", result.ErrorCode);
        Assert.Empty(await db.Semesters.ToListAsync());
    }
}
