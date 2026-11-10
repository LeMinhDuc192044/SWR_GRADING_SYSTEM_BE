using Application.Common;
using Application.DTOs.Assignments;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests;

public class AssignmentServiceTests
{
    private sealed class TestDbUnitOfWork : IUnitOfWork
    {
        private readonly TestDbContext _db;
        public TestDbUnitOfWork(TestDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    private static async Task<(
        Guid SessionRound1Id,
        Guid SessionRegradeId,
        Guid Monitor1Id,
        Guid Monitor2Id,
        Guid Monitor3Id,
        Guid Sub1Id,
        Guid Sub2Id)> SeedScenarioAsync(TestDbContext db)
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
        var monitor1 = CreateMonitor("MON01", "Monitor One");
        var monitor2 = CreateMonitor("MON02", "Monitor Two");
        var monitor3 = CreateMonitor("MON03", "Monitor Three");
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
        var sv1 = CreateStudent("SE180001", "Student One");
        var sv2 = CreateStudent("SE180002", "Student Two");

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
            ExaminationId = exam.ExaminationId
        };

        var round1 = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = examSession.ExaminationSessionId,
            Title = "Cham dot 1",
            CreatedDate = new DateOnly(2026, 11, 2),
            UpdatedDate = new DateOnly(2026, 11, 2),
            Deadline = new DateOnly(2026, 11, 5),
            NumberSubmissions = 2
        };
        var regradeSession = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            ExaminationSessionId = examSession.ExaminationSessionId,
            Title = "[Phúc khảo] Dot phuc khao",
            CreatedDate = new DateOnly(2026, 11, 6),
            UpdatedDate = new DateOnly(2026, 11, 6),
            Deadline = new DateOnly(2026, 11, 10),
            NumberSubmissions = 2
        };

        var sub1 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "sub1.docx",
            StudentId = sv1.Id,
            UploadedById = examiner.Id,
            ExaminationSessionId = examSession.ExaminationSessionId,
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow.AddDays(-5),
            UpdatedDate = DateTime.UtcNow.AddDays(-5)
        };
        var sub2 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "sub2.docx",
            StudentId = sv2.Id,
            UploadedById = examiner.Id,
            ExaminationSessionId = examSession.ExaminationSessionId,
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow.AddDays(-4),
            UpdatedDate = DateTime.UtcNow.AddDays(-4)
        };

        // Monitor 1 da cham Sub 1 trong Round 1
        var round1Assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            GradingSessionId = round1.GradingSessionId,
            SubmissionId = sub1.SubmissionId,
            StaffId = monitor1.Id,
            Status = AssignmentStatus.GRADED,
            AssignedDate = DateTime.UtcNow.AddDays(-3),
            UpdatedDate = DateTime.UtcNow.AddDays(-2)
        };

        db.Semesters.Add(semester);
        db.Staff.AddRange(monitor1, monitor2, monitor3, examiner);
        db.Students.AddRange(sv1, sv2);
        db.Examinations.Add(exam);
        db.ExaminationSessions.Add(examSession);
        db.GradingSessions.AddRange(round1, regradeSession);
        db.StudentSubmissions.AddRange(sub1, sub2);
        db.Assignments.Add(round1Assignment);
        await db.SaveChangesAsync();

        return (round1.GradingSessionId, regradeSession.GradingSessionId, monitor1.Id, monitor2.Id, monitor3.Id, sub1.SubmissionId, sub2.SubmissionId);
    }

    private static Staff CreateMonitor(string code, string name) => new()
    {
        Id = Guid.NewGuid(),
        StaffCode = code,
        FullName = name,
        Email = $"{code.ToLower()}@fpt.edu.vn",
        Birthday = new DateOnly(1988, 1, 1),
        PasswordHash = "hash",
        Role = UserRole.Monitor,
        IsActive = true,
        IsDeleted = false,
        CreatedDay = DateTime.UtcNow,
        UpdatedDay = DateTime.UtcNow
    };

    private static Student CreateStudent(string code, string name) => new()
    {
        Id = Guid.NewGuid(),
        StudentCode = code,
        FullName = name,
        Email = $"{code.ToLower()}@fpt.edu.vn",
        Birthday = new DateOnly(2004, 1, 1),
        PasswordHash = "hash",
        Major = "SE",
        CreatedDay = DateTime.UtcNow,
        UpdatedDay = DateTime.UtcNow
    };

    [Fact]
    public async Task CreateAsync_AutoSelect_SkipsSubmissionsPreviouslyAssignedToSameMonitor()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (_, regradeSessionId, monitor1Id, _, _, sub1Id, sub2Id) = await SeedScenarioAsync(db);
        var service = new AssignmentService(db, new TestDbUnitOfWork(db));

        // Yêu cầu gán 2 bài cho Monitor 1 ở session mới -> chỉ được nhận Sub2 vì Monitor 1 đã chấm Sub1 ở Round 1
        var result = await service.CreateAsync(regradeSessionId, new CreateAssignmentRequest
        {
            StaffId = monitor1Id,
            NumberSubmissions = 2
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(sub2Id, result.Data[0].SubmissionId);
        Assert.DoesNotContain(result.Data, a => a.SubmissionId == sub1Id);
    }

    [Fact]
    public async Task CreateAsync_ExplicitSelectionWithPreviousMonitor_ReturnsDuplicateMonitorFailure()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (_, regradeSessionId, monitor1Id, _, _, sub1Id, _) = await SeedScenarioAsync(db);
        var service = new AssignmentService(db, new TestDbUnitOfWork(db));

        var result = await service.CreateAsync(regradeSessionId, new CreateAssignmentRequest
        {
            StaffId = monitor1Id,
            SubmissionIds = [sub1Id]
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("MONITOR_DUPLICATE_SUBMISSION", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ExplicitSelectionWithDifferentMonitor_AssignsExactRegradeSubmission()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (_, regradeSessionId, _, monitor2Id, _, sub1Id, _) = await SeedScenarioAsync(db);
        var service = new AssignmentService(db, new TestDbUnitOfWork(db));

        var result = await service.CreateAsync(regradeSessionId, new CreateAssignmentRequest
        {
            StaffId = monitor2Id,
            SubmissionIds = [sub1Id]
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(sub1Id, result.Data[0].SubmissionId);
        Assert.Equal(monitor2Id, result.Data[0].StaffId);
    }

    [Fact]
    public async Task UpdateAsync_ReassignToRound1Monitor_ReturnsDuplicateMonitorFailure()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (_, regradeSessionId, monitor1Id, monitor2Id, monitor3Id, sub1Id, _) = await SeedScenarioAsync(db);
        var service = new AssignmentService(db, new TestDbUnitOfWork(db));

        // Assign Sub1 cho Monitor 2 ở đợt phúc khảo
        var created = await service.CreateAsync(regradeSessionId, new CreateAssignmentRequest
        {
            StaffId = monitor2Id,
            SubmissionIds = [sub1Id]
        });
        var assignmentId = created.Data![0].AssignmentId;

        // Cố tình đổi sang Monitor 1 (người đã chấm Sub1 ở Round 1) -> bị chặn!
        var duplicateAttempt = await service.UpdateAsync(regradeSessionId, assignmentId, new UpdateAssignmentRequest
        {
            StaffId = monitor1Id
        });
        Assert.False(duplicateAttempt.IsSuccess);
        Assert.Equal("MONITOR_DUPLICATE_SUBMISSION", duplicateAttempt.ErrorCode);

        // Đổi sang Monitor 3 (chưa từng chấm Sub1) -> thành công!
        var validReassign = await service.UpdateAsync(regradeSessionId, assignmentId, new UpdateAssignmentRequest
        {
            StaffId = monitor3Id
        });
        Assert.True(validReassign.IsSuccess);
        Assert.Equal(monitor3Id, validReassign.Data!.StaffId);
    }
}
