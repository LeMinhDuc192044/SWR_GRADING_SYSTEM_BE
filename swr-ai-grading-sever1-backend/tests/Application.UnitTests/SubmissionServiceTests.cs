using Application.Common;
using Application.Common.Interfaces;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.UnitTests;

public class SubmissionServiceTests
{
    private static (TestDbContext Db, Student Student, Student OtherStudent, ExaminationSession Session, StudentSubmission Submission)
        Seed()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

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

        var otherStudent = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180002",
            FullName = "Tran Thi B",
            Email = "se180002@fpt.edu.vn",
            Major = "SE",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = "FA26",
            Name = "Fall 2026",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            SemesterId = semester.SemesterId,
            Semester = semester,
            Name = "SWR Final Exam",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationId = exam.ExaminationId,
            Examination = exam,
            ExaminationCode = "ROOM_101",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "ST001",
            FullName = "Examiner Staff",
            Email = "examiner@fpt.edu.vn",
            Role = UserRole.Examiner,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student.Id,
            Student = student,
            ExaminationSessionId = session.ExaminationSessionId,
            ExaminationSession = session,
            UploadedById = staff.Id,
            UploadedBy = staff,
            FileSubmissions = "submissions/SE180001_exam.docx",
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        var draftHistory = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.AI,
            GradingRound = 1,
            TotalScore = 7.0m,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30)
        };

        var officialHistory = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 1,
            TotalScore = 8.5m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        db.Students.AddRange(student, otherStudent);
        db.Staff.Add(staff);
        db.Semesters.Add(semester);
        db.Examinations.Add(exam);
        db.ExaminationSessions.Add(session);
        db.StudentSubmissions.Add(submission);
        db.SubmissionGradingHistories.AddRange(draftHistory, officialHistory);
        db.SaveChanges();

        return (db, student, otherStudent, session, submission);
    }

    private static SubmissionService CreateService(TestDbContext db)
    {
        var storage = new Mock<ISupabaseStorage>();
        var gemini = new Mock<IGeminiAIService>();
        var imageUrls = new Mock<IImageUrlProvider>();
        return new SubmissionService(db, storage.Object, gemini.Object, imageUrls.Object);
    }

    [Fact]
    public async Task GetPagedListAsync_ProjectsStudentCodeNameAndExaminationSessionId()
    {
        var (db, student, _, session, _) = Seed();
        var service = CreateService(db);

        var result = await service.GetPagedListAsync(new PagedRequest(), Guid.NewGuid(), isElevatedRole: true);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        var item = result.Data.Items[0];
        Assert.Equal("SE180001", item.StudentCode);
        Assert.Equal("Nguyen Van A", item.StudentName);
        Assert.Equal(session.ExaminationSessionId, item.StudentExaminationId);
    }

    [Fact]
    public async Task GetByStudentAsync_OnlyReturnsOfficialGradingHistories_HidesDrafts()
    {
        var (db, student, _, _, _) = Seed();
        var service = CreateService(db);

        var result = await service.GetByStudentAsync(student.Id);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        var subDto = result.Data![0];
        Assert.Single(subDto.GradingHistories);
        Assert.Equal(SubmissionGradingHistoryStatus.OFFICIAL, subDto.GradingHistories[0].Status);
        Assert.Equal(8.5m, subDto.GradingHistories[0].TotalScore);
    }

    [Fact]
    public async Task GetByIdAsync_NonElevatedStudentRequestingOtherStudent_ReturnsForbidden()
    {
        var (db, _, otherStudent, _, submission) = Seed();
        var service = CreateService(db);

        // otherStudent tries to view student's submission without elevated role
        var result = await service.GetByIdAsync(submission.SubmissionId, otherStudent.Id, isElevatedRole: false);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsRealTimelineEvents()
    {
        var (db, student, _, _, submission) = Seed();
        var service = CreateService(db);

        // Student (non-elevated) sees submitted and official lecturer grade, but DRAFT AI score is excluded
        var result = await service.GetHistoryAsync(submission.SubmissionId, student.Id, isElevatedRole: false);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data!.Timeline.Count);
        Assert.Contains(result.Data.Timeline, t => t.Stage == "Submitted");
        Assert.Contains(result.Data.Timeline, t => t.Stage == "LecturerGrading" && t.Title.Contains("8.5"));
        Assert.DoesNotContain(result.Data.Timeline, t => t.Stage == "AIGrading");

        // Elevated staff sees all timeline events including draft AI score
        var elevatedResult = await service.GetHistoryAsync(submission.SubmissionId, Guid.NewGuid(), isElevatedRole: true);
        Assert.True(elevatedResult.IsSuccess);
        Assert.Equal(3, elevatedResult.Data!.Timeline.Count);
        Assert.Contains(elevatedResult.Data.Timeline, t => t.Stage == "AIGrading" && t.Title.Contains("7"));
    }
}
