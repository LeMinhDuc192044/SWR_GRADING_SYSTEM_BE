using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.UnitTests;

public class RoleBasedMultiScenarioTests
{
    private readonly TestDbContext _dbContext;
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<ISupabaseStorage> _storageMock = new();
    private readonly Mock<IGeminiAIService> _geminiMock = new();
    private readonly Mock<ILogger<SubmissionService>> _loggerMock = new();

    public RoleBasedMultiScenarioTests()
    {
        _dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private async Task<(Guid lecturerId, Guid studentId, Guid diaryId, Guid submissionId)> SetupCompleteHierarchyAsync()
    {
        var lecturerId = Guid.NewGuid();
        var lecturer = new Lecturer
        {
            Id = lecturerId,
            LecturerCode = "LEC_" + Guid.NewGuid().ToString("N")[..6],
            FullName = "Dr. Test Lecturer",
            Email = $"lec_{Guid.NewGuid():N}@fpt.edu.vn",
            Role = AI_Assisted_SWR_Grading_System.Domain.Enums.UserRole.Lecturer,
            IsActive = true
        };
        await _dbContext.Lecturers.AddAsync(lecturer);

        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "SEM_" + Guid.NewGuid().ToString("N")[..4].ToUpper(),
            Name = "Test Semester",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 5, 1),
            Status = SemesterStatus.Active
        };
        await _dbContext.Semesters.AddAsync(semester);

        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            ExaminationCode = "EX_" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            Name = "Test Examination",
            SemesterId = semester.SemesterId,
            StartDate = new DateOnly(2026, 3, 1),
            StartTime = new TimeSpan(8, 0, 0),
            DurationMinutes = 90
        };
        await _dbContext.Examinations.AddAsync(exam);

        var paperSet = new PaperSet
        {
            PaperSetId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            ExaminationId = exam.ExaminationId,
            PaperSetCode = "PS_" + Guid.NewGuid().ToString("N")[..6].ToUpper(),
            TotalQuestions = 3
        };
        await _dbContext.PaperSets.AddAsync(paperSet);

        var diary = new GradingDiary
        {
            GradingDiaryId = Guid.NewGuid(),
            Name = "Sổ chấm test",
            CreateById = lecturerId,
            PaperSetId = paperSet.PaperSetId,
            Content = exam.ExaminationCode
        };
        await _dbContext.GradingDiaries.AddAsync(diary);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE" + Random.Shared.Next(100000, 999999),
            FullName = "Test Student",
            Email = $"student_{Guid.NewGuid():N}@fpt.edu.vn",
            Role = AI_Assisted_SWR_Grading_System.Domain.Enums.UserRole.Student
        };
        await _dbContext.Students.AddAsync(student);

        var studentExam = new StudentExamination
        {
            StudentExaminationId = Guid.NewGuid(),
            StudentId = student.Id,
            ExamId = exam.ExaminationId
        };
        await _dbContext.StudentExaminations.AddAsync(studentExam);

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            DiaryId = diary.GradingDiaryId,
            StudentExaminationId = studentExam.StudentExaminationId,
            FileSubmissions = "test_submission.docx",
            Status = SubmissionStatus.Submitted,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        await _dbContext.StudentSubmissions.AddAsync(submission);
        await _dbContext.SaveChangesAsync();

        return (lecturerId, student.Id, diary.GradingDiaryId, submission.SubmissionId);
    }

    #region Role 1: Examination Officer (Admin) Tests

    [Fact]
    public async Task Admin_CanCreateSemesterAndEnrollStudents()
    {
        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Status = SemesterStatus.Active
        };
        await _dbContext.Semesters.AddAsync(semester);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE170001",
            FullName = "Nguyen Van A",
            Email = "anvse170001@fpt.edu.vn",
            Major = "Software Engineering",
            Role = AI_Assisted_SWR_Grading_System.Domain.Enums.UserRole.Student,
            IsActive = true
        };
        await _dbContext.Students.AddAsync(student);

        var enrollment = new StudentSemester
        {
            StudentId = student.Id,
            SemesterId = semester.SemesterId
        };
        await _dbContext.StudentSemesters.AddAsync(enrollment);
        await _dbContext.SaveChangesAsync();

        var savedEnrollment = await _dbContext.StudentSemesters
            .Include(ss => ss.Student)
            .Include(ss => ss.Semester)
            .FirstOrDefaultAsync(ss => ss.StudentId == student.Id && ss.SemesterId == semester.SemesterId);

        Assert.NotNull(savedEnrollment);
        Assert.Equal("SE170001", savedEnrollment.Student.StudentCode);
        Assert.Equal("FA26", savedEnrollment.Semester.SemesterCode);
    }

    [Fact]
    public async Task Admin_CanCreateExamination_LinkedToSemester()
    {
        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "SP26",
            Name = "Spring 2026",
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 4, 30),
            Status = SemesterStatus.Active
        };
        await _dbContext.Semesters.AddAsync(semester);

        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            ExaminationCode = "EXAM_PRJ301_SP26",
            Name = "PRJ301 Practical Final Exam",
            SemesterId = semester.SemesterId,
            StartDate = new DateOnly(2026, 4, 15),
            StartTime = new TimeSpan(8, 0, 0),
            DurationMinutes = 90,
            Status = ExaminationStatus.Draft
        };
        await _dbContext.Examinations.AddAsync(exam);
        await _dbContext.SaveChangesAsync();

        var savedExam = await _dbContext.Examinations
            .Include(e => e.Semester)
            .FirstOrDefaultAsync(e => e.ExaminationId == exam.ExaminationId);

        Assert.NotNull(savedExam);
        Assert.Equal("EXAM_PRJ301_SP26", savedExam.ExaminationCode);
        Assert.Equal(ExaminationStatus.Draft, savedExam.Status);
    }

    #endregion

    #region Role 2: Lecturer Tests

    [Fact]
    public async Task Lecturer_CanCreateGradingDiary_AndReviewSubmissions()
    {
        var (lecturerId, _, _, submissionId) = await SetupCompleteHierarchyAsync();

        var subService = new SubmissionService(
            new Mock<IGradingDiaryRepository>().Object,
            new Mock<ISubmissionRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            _storageMock.Object,
            _geminiMock.Object,
            _uowMock.Object,
            _dbContext,
            _loggerMock.Object);

        var reviewRequest = new ReviewSubmissionRequest
        {
            LecturerScore = 8.5m,
            Comment = "Bài làm tốt, sơ đồ Use Case đầy đủ các quan hệ include/extend.",
            PickAiScore = false,
            IsFinal = false
        };

        var reviewResult = await subService.ReviewAsync(
            submissionId,
            reviewRequest,
            lecturerId,
            isElevatedRole: false);

        Assert.True(reviewResult.IsSuccess);
        Assert.Equal(8.5m, reviewResult.Data?.LecturerScore);
        Assert.Equal(SubmissionStatus.Lecturer_Reviewed, reviewResult.Data?.Status);

        var finalizeResult = await subService.FinalizeAsync(
            submissionId,
            lecturerId,
            isElevatedRole: false);

        Assert.True(finalizeResult.IsSuccess);
        Assert.Equal(SubmissionStatus.Final, finalizeResult.Data?.Status);
        Assert.True(finalizeResult.Data?.IsFinalScore);
    }

    [Fact]
    public async Task Lecturer_PickAiScore_CorrectlyCopiesAiScoreAndDetails()
    {
        var (lecturerId, _, _, submissionId) = await SetupCompleteHierarchyAsync();

        // Add AI Evaluation to submission
        var aiEvaluation = new SubmissionEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SubmissionId = submissionId,
            EvaluationType = EvaluationType.AI,
            EvaluationRound = 1,
            TotalScore = 9.0m,
            GeneralFeedback = "AI đánh giá: Bài làm rất tốt, sơ đồ thiết kế chính xác.",
            IsFinalScore = false,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.SubmissionEvaluations.AddAsync(aiEvaluation);
        await _dbContext.SaveChangesAsync();

        var subService = new SubmissionService(
            new Mock<IGradingDiaryRepository>().Object,
            new Mock<ISubmissionRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            _storageMock.Object,
            _geminiMock.Object,
            _uowMock.Object,
            _dbContext,
            _loggerMock.Object);

        var pickRequest = new ReviewSubmissionRequest
        {
            PickAiScore = true,
            Comment = "Đồng ý với đánh giá của AI",
            IsFinal = true
        };

        var result = await subService.ReviewAsync(submissionId, pickRequest, lecturerId, isElevatedRole: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(9.0m, result.Data?.LecturerScore);
        Assert.Equal(SubmissionStatus.Final, result.Data?.Status);
        Assert.True(result.Data?.IsFinalScore);
    }

    #endregion

    #region Role 3: Student RBAC Verification Tests

    [Fact]
    public async Task Student_CannotReviewSubmission_Forbidden()
    {
        var (_, studentId, _, submissionId) = await SetupCompleteHierarchyAsync();

        var subService = new SubmissionService(
            new Mock<IGradingDiaryRepository>().Object,
            new Mock<ISubmissionRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            _storageMock.Object,
            _geminiMock.Object,
            _uowMock.Object,
            _dbContext,
            _loggerMock.Object);

        // Student attempts to grade or finalize score
        var forbiddenResult = await subService.ReviewAsync(
            submissionId,
            new ReviewSubmissionRequest { LecturerScore = 10.0m },
            studentId,
            isElevatedRole: false);

        Assert.False(forbiddenResult.IsSuccess);
        Assert.Equal("FORBIDDEN", forbiddenResult.ErrorCode);
    }

    [Fact]
    public async Task AuditHistory_TracksFullLifecycle()
    {
        var (lecturerId, _, _, submissionId) = await SetupCompleteHierarchyAsync();

        var aiEval = new SubmissionEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SubmissionId = submissionId,
            EvaluationType = EvaluationType.AI,
            EvaluationRound = 1,
            TotalScore = 7.5m,
            GeneralFeedback = "AI: Khá",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };
        await _dbContext.SubmissionEvaluations.AddAsync(aiEval);

        var lecturerEval = new SubmissionEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SubmissionId = submissionId,
            EvaluationType = EvaluationType.Lecturer,
            EvaluationRound = 1,
            TotalScore = 8.0m,
            GeneralFeedback = "GV duyệt: 8.0",
            IsFinalScore = true,
            CreatedAt = DateTime.UtcNow
        };
        await _dbContext.SubmissionEvaluations.AddAsync(lecturerEval);

        var sub = await _dbContext.StudentSubmissions.FindAsync(submissionId);
        sub!.Status = SubmissionStatus.Final;
        await _dbContext.SaveChangesAsync();

        var subService = new SubmissionService(
            new Mock<IGradingDiaryRepository>().Object,
            new Mock<ISubmissionRepository>().Object,
            new Mock<IPaperSetRepository>().Object,
            _storageMock.Object,
            _geminiMock.Object,
            _uowMock.Object,
            _dbContext,
            _loggerMock.Object);

        var historyResult = await subService.GetHistoryAsync(
            submissionId,
            lecturerId,
            isElevatedRole: false);

        Assert.True(historyResult.IsSuccess);
        Assert.NotNull(historyResult.Data);
        Assert.Equal(8.0m, historyResult.Data.LecturerScore);
    }

    #endregion
}
