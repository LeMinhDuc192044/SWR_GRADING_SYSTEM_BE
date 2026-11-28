using Application.Common.Interfaces;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests;

public class ExaminationSessionServiceTests
{
    private sealed class TestDbUnitOfWork : IUnitOfWork
    {
        private readonly TestDbContext _db;
        public TestDbUnitOfWork(TestDbContext db) => _db = db;
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
    }

    private static ExaminationSessionService CreateService(TestDbContext db) => new(
        db,
        new TestDbUnitOfWork(db),
        new Mock<IStudentCodeExcelReader>().Object);

    // Seeds a semester with one examination that already has a session at 08:00 on 02/11/2026.
    private static async Task<Guid> SeedExaminationAsync(
        TestDbContext db,
        string semesterCode = "FA26",
        string examinationName = "SWR PE",
        bool withFirstSession = true)
    {
        var semester = new Semester
        {
            SemesterId = Guid.NewGuid(),
            SemesterCode = semesterCode,
            Name = $"Semester {semesterCode}",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = SemesterStatus.Active
        };
        var examination = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = examinationName,
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 5),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationStatus.Ongoing,
            SemesterId = semester.SemesterId
        };

        db.Semesters.Add(semester);
        db.Examinations.Add(examination);
        if (withFirstSession)
        {
            db.ExaminationSessions.Add(new ExaminationSession
            {
                ExaminationSessionId = Guid.NewGuid(),
                ExaminationCode = "ROOM_101",
                StartDate = new DateOnly(2026, 11, 2),
                StartTime = new TimeOnly(8, 0),
                CreatedDay = DateTime.UtcNow,
                UpdatedDay = DateTime.UtcNow,
                Status = ExaminationSessionStatus.SETUP,
                SemesterId = semester.SemesterId,
                ExaminationId = examination.ExaminationId
            });
        }
        await db.SaveChangesAsync();

        return examination.ExaminationId;
    }

    private static CreateExaminationSessionRequest SessionRequest(DateOnly startDate, TimeOnly startTime, string code = "ROOM_102") => new()
    {
        ExaminationCode = code,
        StartDate = startDate,
        StartTime = startTime,
        Status = ExaminationSessionStatus.SETUP
    };

    [Fact]
    public async Task CreateAsync_SecondSessionSameDayWithEnoughGap_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);

        var result = await CreateService(db)
            .CreateAsync(examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(10, 0)));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, await db.ExaminationSessions.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_SessionExactlyNinetyMinutesApart_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);

        var result = await CreateService(db)
            .CreateAsync(examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(9, 30)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_SessionNinetyMinutesBeforeExistingOne_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);

        var result = await CreateService(db)
            .CreateAsync(examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(6, 30)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_SessionWithinNinetyMinutesOfAnother_ReturnsConflict()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);

        // 08:00 -> 09:00 is only 60 minutes apart.
        var result = await CreateService(db)
            .CreateAsync(examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(9, 0)));

        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_TIME_CONFLICT", result.ErrorCode);
        Assert.Equal(1, await db.ExaminationSessions.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_SameTimeOnAnotherDay_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);

        var result = await CreateService(db)
            .CreateAsync(examinationId, SessionRequest(new DateOnly(2026, 11, 3), new TimeOnly(8, 0)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateAsync_SameTimeInAnotherExamination_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        await SeedExaminationAsync(db);
        var otherExaminationId = await SeedExaminationAsync(db, "FA27", "SWR RE", withFirstSession: false);

        var result = await CreateService(db)
            .CreateAsync(otherExaminationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(8, 0)));

        Assert.True(result.IsSuccess, $"{result.ErrorCode}: {result.Error}");
    }

    [Fact]
    public async Task UpdateAsync_MovingSessionWithinNinetyMinutesOfAnother_ReturnsConflict()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);
        var service = CreateService(db);

        // 10:30 is far enough from the seeded 08:00 session.
        var created = await service.CreateAsync(
            examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(10, 30)));
        Assert.True(created.IsSuccess);

        // Moving it to 09:00 leaves only 60 minutes to the 08:00 session.
        var result = await service.UpdateAsync(
            examinationId,
            created.Data!.ExaminationSessionId,
            new UpdateExaminationSessionRequest { StartTime = new TimeOnly(9, 0) });

        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_TIME_CONFLICT", result.ErrorCode);

        var stored = await db.ExaminationSessions.SingleAsync(s => s.ExaminationSessionId == created.Data!.ExaminationSessionId);
        Assert.Equal(new TimeOnly(10, 30), stored.StartTime);
    }

    [Fact]
    public async Task UpdateAsync_MovingSessionNinetyMinutesAway_Succeeds()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examinationId = await SeedExaminationAsync(db);
        var service = CreateService(db);

        // 10:30 is far enough from the seeded 08:00 session.
        var created = await service.CreateAsync(
            examinationId, SessionRequest(new DateOnly(2026, 11, 2), new TimeOnly(10, 30)));
        Assert.True(created.IsSuccess);

        var result = await service.UpdateAsync(
            examinationId,
            created.Data!.ExaminationSessionId,
            new UpdateExaminationSessionRequest { StartTime = new TimeOnly(9, 30) });

        Assert.True(result.IsSuccess);
        Assert.Equal(new TimeOnly(9, 30), result.Data!.StartTime);
    }
}
