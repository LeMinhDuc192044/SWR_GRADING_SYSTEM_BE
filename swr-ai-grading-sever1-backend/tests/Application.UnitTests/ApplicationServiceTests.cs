using Application.Common;
using Application.DTOs.Applications;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.UnitTests;

public class ApplicationServiceTests
{
    private static async Task<(TestDbContext Db, Student Student, Student OtherStudent, StudentSubmission Submission, Staff Examiner)>
        SeedAsync(string? dbName = null)
    {
        var db = TestDbContextFactory.Create(dbName ?? Guid.NewGuid().ToString());

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

        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "ST001",
            FullName = "Staff Member",
            Email = "staff@fpt.edu.vn",
            Role = UserRole.Examiner,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = "SWR Final PE",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationId = exam.ExaminationId,
            ExaminationCode = "SWR_ROOM_01",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var submission = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student.Id,
            ExaminationSessionId = session.ExaminationSessionId,
            FileSubmissions = "submission/test.docx",
            Status = SubmissionStatus.Submitted,
            UploadedById = examiner.Id,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        // Điểm chính thức OFFICIAL
        var officialHistory = new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 1,
            TotalScore = 6.5m,
            GeneralFeedback = "Official score published",
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        };

        db.Students.AddRange(student, otherStudent);
        db.Staff.Add(examiner);
        db.Examinations.Add(exam);
        db.ExaminationSessions.Add(session);
        db.StudentSubmissions.Add(submission);
        db.SubmissionGradingHistories.Add(officialHistory);

        await db.SaveChangesAsync();

        return (db, student, otherStudent, submission, examiner);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesPendingApplication()
    {
        var (db, student, _, submission, _) = await SeedAsync();
        var service = new ApplicationService(db);

        var request = new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Em thấy câu 2 em vẽ đúng quan hệ Include nhưng bị trừ 1 điểm."
        };

        var result = await service.CreateAsync(request, student.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(ApplicationStatus.PENDING, result.Data.Status);
        Assert.Equal(student.StudentCode, result.Data.StudentCode);
        Assert.Equal(6.5m, result.Data.OfficialScore);

        var saved = await db.Applications.SingleOrDefaultAsync(a => a.Id == result.Data.Id);
        Assert.NotNull(saved);
        Assert.Equal(ApplicationStatus.PENDING, saved.Status);
    }

    [Fact]
    public async Task CreateAsync_SubmissionBelongsToAnotherStudent_ReturnsForbidden()
    {
        var (db, _, otherStudent, submission, _) = await SeedAsync();
        var service = new ApplicationService(db);

        var request = new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Phúc khảo giùm bạn"
        };

        var result = await service.CreateAsync(request, otherStudent.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_SubmissionNotOfficiallyGraded_ReturnsFailure()
    {
        var (db, student, _, submission, _) = await SeedAsync();

        // Xóa điểm OFFICIAL
        var histories = await db.SubmissionGradingHistories.ToListAsync();
        db.SubmissionGradingHistories.RemoveRange(histories);
        await db.SaveChangesAsync();

        var service = new ApplicationService(db);
        var request = new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Chưa có điểm đã phúc khảo"
        };

        var result = await service.CreateAsync(request, student.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("NOT_OFFICIALLY_GRADED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ApplicationAlreadyExists_ReturnsConflict()
    {
        var (db, student, _, submission, _) = await SeedAsync();
        var service = new ApplicationService(db);

        var request = new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Đơn lần 1"
        };

        await service.CreateAsync(request, student.Id);

        // Nộp lần 2 cho cùng 1 bài thi
        var duplicateResult = await service.CreateAsync(request, student.Id);

        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal("APPLICATION_ALREADY_EXISTS", duplicateResult.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_AdminApproves_UpdatesStatusToApproved()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);

        var createResult = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần chấm lại"
        }, student.Id);

        var appId = createResult.Data!.Id;

        var updateResult = await service.UpdateStatusAsync(appId, new UpdateApplicationStatusRequest
        {
            Status = ApplicationStatus.APPROVED
        }, examiner.Id);

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(ApplicationStatus.APPROVED, updateResult.Data!.Status);

        var updated = await db.Applications.FindAsync(appId);
        Assert.NotNull(updated);
        Assert.Equal(ApplicationStatus.APPROVED, updated.Status);
    }

    [Fact]
    public async Task GetMyApplicationsAsync_ReturnsOnlyStudentApplications()
    {
        var (db, student, otherStudent, submission, _) = await SeedAsync();
        var service = new ApplicationService(db);

        await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Đơn của SV1"
        }, student.Id);

        var myApps = await service.GetMyApplicationsAsync(new PagedRequest(), student.Id);
        var otherApps = await service.GetMyApplicationsAsync(new PagedRequest(), otherStudent.Id);

        Assert.True(myApps.IsSuccess);
        Assert.Single(myApps.Data!.Items);
        Assert.Equal("Đơn của SV1", myApps.Data!.Items[0].Reason);
        Assert.Equal(1, myApps.Data!.TotalCount);

        Assert.True(otherApps.IsSuccess);
        Assert.Empty(otherApps.Data!.Items);
        Assert.Equal(0, otherApps.Data!.TotalCount);
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ReturnsPagedApplications()
    {
        var (db, student, _, submission, _) = await SeedAsync();
        var service = new ApplicationService(db);

        await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Đơn của SV1"
        }, student.Id);

        var pagedApps = await service.GetAllAsync(new PagedRequest { Page = 1, PageSize = 10 });

        Assert.True(pagedApps.IsSuccess);
        Assert.Equal(1, pagedApps.Data!.TotalCount);
        Assert.Single(pagedApps.Data!.Items);
        Assert.Equal("Nguyen Van A", pagedApps.Data!.Items[0].StudentName);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidEnumStatus_ReturnsInvalidStatus()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);

        var createResult = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần phúc khảo"
        }, student.Id);

        var appId = createResult.Data!.Id;

        var result = await service.UpdateStatusAsync(appId, new UpdateApplicationStatusRequest
        {
            Status = (ApplicationStatus)99
        }, examiner.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransitionRejectedToPending_ReturnsError()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);

        var createResult = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần phúc khảo"
        }, student.Id);

        var appId = createResult.Data!.Id;

        // Reject first
        var rejectResult = await service.UpdateStatusAsync(appId, new UpdateApplicationStatusRequest
        {
            Status = ApplicationStatus.REJECTED
        }, examiner.Id);
        Assert.True(rejectResult.IsSuccess);

        // Try to revert to PENDING
        var revertResult = await service.UpdateStatusAsync(appId, new UpdateApplicationStatusRequest
        {
            Status = ApplicationStatus.PENDING
        }, examiner.Id);

        Assert.False(revertResult.IsSuccess);
        Assert.Equal("INVALID_STATUS_TRANSITION", revertResult.ErrorCode);
    }

    [Fact]
    public async Task UpdateStatusAsync_ApprovalKeepsOfficialScoreVisible()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);

        var createResult = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần phúc khảo"
        }, student.Id);

        var appId = createResult.Data!.Id;

        var approveResult = await service.UpdateStatusAsync(appId, new UpdateApplicationStatusRequest
        {
            Status = ApplicationStatus.APPROVED
        }, examiner.Id);

        Assert.True(approveResult.IsSuccess);
        Assert.NotNull(approveResult.Data!.OfficialScore);
        Assert.Equal(6.5m, approveResult.Data.OfficialScore);

        // Official history in DB must still be OFFICIAL
        var officialHistory = await db.SubmissionGradingHistories
            .FirstOrDefaultAsync(h => h.SubmissionId == submission.SubmissionId && h.Status == SubmissionGradingHistoryStatus.OFFICIAL);
        Assert.NotNull(officialHistory);
    }

    [Fact]
    public async Task CreateAsync_AfterRejection_AllowsResubmissionAndResetsToPending()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);

        // 1. Submit appeal
        var firstCreate = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Lý do ban đầu chưa rõ ràng"
        }, student.Id);
        Assert.True(firstCreate.IsSuccess);

        // 2. Reject appeal
        var rejectResult = await service.UpdateStatusAsync(firstCreate.Data!.Id, new UpdateApplicationStatusRequest
        {
            Status = ApplicationStatus.REJECTED
        }, examiner.Id);
        Assert.True(rejectResult.IsSuccess);

        // 3. Resubmit appeal with new reason
        var secondCreate = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Bổ sung bằng chứng và lý do cụ thể"
        }, student.Id);

        Assert.True(secondCreate.IsSuccess);
        Assert.Equal(firstCreate.Data.Id, secondCreate.Data!.Id); // Reused record
        Assert.Equal(ApplicationStatus.PENDING, secondCreate.Data.Status);
        Assert.Equal("Bổ sung bằng chứng và lý do cụ thể", secondCreate.Data.Reason);
    }

    [Fact]
    public async Task UpdateStatusAsync_Approve_SetsApprovedAtThatLaterTransitionsKeep()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);
        var created = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần phúc khảo"
        }, student.Id);

        var approved = await service.UpdateStatusAsync(created.Data!.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.APPROVED }, examiner.Id);
        Assert.True(approved.IsSuccess);
        var approvedAt = (await db.Applications.AsNoTracking().SingleAsync(a => a.Id == created.Data.Id)).ApprovedAt;
        Assert.NotNull(approvedAt);

        await Task.Delay(20);
        var inProgress = await service.UpdateStatusAsync(created.Data.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.IN_PROGRESS }, examiner.Id);
        Assert.True(inProgress.IsSuccess);

        var stored = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == created.Data.Id);
        Assert.Equal(approvedAt, stored.ApprovedAt);       // the regrade cycle start does not move
        Assert.True(stored.UpdatedDay > stored.ApprovedAt!.Value);
    }

    [Fact]
    public async Task CreateAsync_ResubmissionRacingAnApproval_ReturnsConflictAndKeepsApproval()
    {
        var dbName = Guid.NewGuid().ToString();
        var (db, student, _, submission, examiner) = await SeedAsync(dbName);
        var service = new ApplicationService(db);
        var created = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Lý do ban đầu"
        }, student.Id);
        await service.UpdateStatusAsync(created.Data!.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.REJECTED }, examiner.Id);

        // Another request (separate DbContext) changes the stored status while this context
        // still holds the application as REJECTED.
        using (var other = TestDbContextFactory.Create(dbName))
        {
            var app = await other.Applications.SingleAsync(a => a.Id == created.Data.Id);
            app.Status = ApplicationStatus.APPROVED;
            await other.SaveChangesAsync();
        }

        var resubmit = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Nộp lại"
        }, student.Id);

        Assert.False(resubmit.IsSuccess);
        Assert.Equal("APPLICATION_STATUS_CONFLICT", resubmit.ErrorCode);
        using var check = TestDbContextFactory.Create(dbName);
        Assert.Equal(ApplicationStatus.APPROVED, (await check.Applications.SingleAsync(a => a.Id == created.Data.Id)).Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_CompleteBeforeRegradeFinalized_IsRejectedUntilNewOfficialGrade()
    {
        var (db, student, _, submission, examiner) = await SeedAsync();
        var service = new ApplicationService(db);
        var created = await service.CreateAsync(new CreateApplicationRequest
        {
            SubmissionId = submission.SubmissionId,
            Reason = "Cần phúc khảo"
        }, student.Id);
        await service.UpdateStatusAsync(created.Data!.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.APPROVED }, examiner.Id);

        var tooEarly = await service.UpdateStatusAsync(created.Data.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.COMPLETED }, examiner.Id);
        Assert.False(tooEarly.IsSuccess);
        Assert.Equal("REGRADE_NOT_FINALIZED", tooEarly.ErrorCode);
        Assert.Equal(ApplicationStatus.APPROVED,
            (await db.Applications.AsNoTracking().SingleAsync(a => a.Id == created.Data.Id)).Status);

        // The regrade is finalized: the old official grade is demoted and a newer one is official.
        var oldOfficial = await db.SubmissionGradingHistories
            .SingleAsync(h => h.SubmissionId == submission.SubmissionId && h.Status == SubmissionGradingHistoryStatus.OFFICIAL);
        oldOfficial.Status = SubmissionGradingHistoryStatus.DRAFT;
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            HistoryId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 2,
            TotalScore = 7.5m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow.AddSeconds(1)
        });
        await db.SaveChangesAsync();

        var completed = await service.UpdateStatusAsync(created.Data.Id,
            new UpdateApplicationStatusRequest { Status = ApplicationStatus.COMPLETED }, examiner.Id);
        Assert.True(completed.IsSuccess);
        Assert.Equal(ApplicationStatus.COMPLETED, completed.Data!.Status);
    }
}
