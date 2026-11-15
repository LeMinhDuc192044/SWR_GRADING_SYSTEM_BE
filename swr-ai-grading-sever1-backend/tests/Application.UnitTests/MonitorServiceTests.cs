using Application.DTOs.Monitors;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.UnitTests;

public class MonitorServiceTests
{
    private static async Task<(TestDbContext Db, Staff Monitor1, Staff Monitor2, Staff InactiveMonitor, Staff DeletedMonitor, Staff Examiner, Staff Admin)>
        SeedAsync()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var m1 = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON01",
            FullName = "Tran Thi Monitor",
            Email = "mon01@fpt.edu.vn",
            Role = UserRole.Monitor,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var m2 = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON02",
            FullName = "Le Van Monitor",
            Email = "mon02@fpt.edu.vn",
            Role = UserRole.Monitor,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var inactiveMonitor = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON03",
            FullName = "Pham Inactive Monitor",
            Email = "mon03@fpt.edu.vn",
            Role = UserRole.Monitor,
            IsActive = false,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var deletedMonitor = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON04",
            FullName = "Deleted Monitor",
            Email = "mon04@fpt.edu.vn",
            Role = UserRole.Monitor,
            IsActive = false,
            IsDeleted = true,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "EX01",
            FullName = "Nguyen Examiner",
            Email = "ex01@fpt.edu.vn",
            Role = UserRole.Examiner,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var admin = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "ADM01",
            FullName = "Admin User",
            Email = "admin@fpt.edu.vn",
            Role = UserRole.Admin,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        db.Staff.AddRange(m1, m2, inactiveMonitor, deletedMonitor, examiner, admin);

        // Gán 2 assignment cho m1
        db.Assignments.AddRange(
            new Assignment
            {
                AssignmentId = Guid.NewGuid(),
                StaffId = m1.Id,
                SubmissionId = Guid.NewGuid(),
                GradingSessionId = Guid.NewGuid(),
                Status = AssignmentStatus.IN_PROGRESS
            },
            new Assignment
            {
                AssignmentId = Guid.NewGuid(),
                StaffId = m1.Id,
                SubmissionId = Guid.NewGuid(),
                GradingSessionId = Guid.NewGuid(),
                Status = AssignmentStatus.COMPLETED
            }
        );

        await db.SaveChangesAsync();

        return (db, m1, m2, inactiveMonitor, deletedMonitor, examiner, admin);
    }

    private static MonitorService CreateService(TestDbContext db)
    {
        var userRepoMock = new Mock<IUserRepository>();
        var authServiceMock = new Mock<IAuthService>();
        var loggerMock = new Mock<ILogger<MonitorService>>();

        return new MonitorService(
            userRepoMock.Object,
            authServiceMock.Object,
            loggerMock.Object,
            db);
    }

    [Fact]
    public async Task GetPagedMonitorsAsync_NoFilter_ReturnsOnlyActiveMonitorsAndExcludesOtherRolesAndDeleted()
    {
        var (db, m1, m2, inactiveMonitor, deletedMonitor, examiner, admin) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedMonitorsAsync(new MonitorFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(3, result.Data.TotalCount); // m1, m2, inactiveMonitor (chưa xóa)
        Assert.DoesNotContain(result.Data.Items, s => s.Id == deletedMonitor.Id);
        Assert.DoesNotContain(result.Data.Items, s => s.Id == examiner.Id);
        Assert.DoesNotContain(result.Data.Items, s => s.Id == admin.Id);
        Assert.Contains(result.Data.Items, s => s.Id == m1.Id);
        Assert.Contains(result.Data.Items, s => s.Id == m2.Id);
    }

    [Fact]
    public async Task GetPagedMonitorsAsync_FilterByIsActive_ReturnsMatchingMonitorsOnly()
    {
        var (db, m1, m2, inactiveMonitor, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var activeResult = await service.GetPagedMonitorsAsync(new MonitorFilterRequest { IsActive = true });
        var inactiveResult = await service.GetPagedMonitorsAsync(new MonitorFilterRequest { IsActive = false });

        Assert.True(activeResult.IsSuccess);
        Assert.Equal(2, activeResult.Data!.TotalCount);
        Assert.Contains(activeResult.Data.Items, s => s.Id == m1.Id);
        Assert.Contains(activeResult.Data.Items, s => s.Id == m2.Id);
        Assert.DoesNotContain(activeResult.Data.Items, s => s.Id == inactiveMonitor.Id);

        Assert.True(inactiveResult.IsSuccess);
        Assert.Single(inactiveResult.Data!.Items);
        Assert.Equal(inactiveMonitor.Id, inactiveResult.Data.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedMonitorsAsync_SearchKeyword_ReturnsMatchingMonitor()
    {
        var (db, m1, m2, _, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedMonitorsAsync(new MonitorFilterRequest
        {
            Search = "MON01"
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(m1.Id, result.Data.Items[0].Id);
        Assert.Equal("Tran Thi Monitor", result.Data.Items[0].FullName);
    }

    [Fact]
    public async Task GetPagedMonitorsAsync_Pagination_ReturnsCorrectChunksAndFlags()
    {
        var (db, _, _, _, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var page1 = await service.GetPagedMonitorsAsync(new MonitorFilterRequest { Page = 1, PageSize = 2 });
        var page2 = await service.GetPagedMonitorsAsync(new MonitorFilterRequest { Page = 2, PageSize = 2 });

        Assert.True(page1.IsSuccess);
        Assert.True(page2.IsSuccess);
        Assert.Equal(2, page1.Data!.Items.Count);
        Assert.Single(page2.Data!.Items);
        Assert.Equal(3, page1.Data.TotalCount);
        Assert.Equal(2, page1.Data.TotalPages);
        Assert.True(page1.Data.HasNext);
        Assert.False(page1.Data.HasPrevious);
        Assert.False(page2.Data.HasNext);
        Assert.True(page2.Data.HasPrevious);
    }

    [Fact]
    public async Task GetPagedMonitorsAsync_AssignedCount_ReturnsCorrectCount()
    {
        var (db, m1, m2, _, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedMonitorsAsync(new MonitorFilterRequest());

        Assert.True(result.IsSuccess);
        var monitor1Dto = result.Data!.Items.First(x => x.Id == m1.Id);
        var monitor2Dto = result.Data!.Items.First(x => x.Id == m2.Id);

        Assert.Equal(2, monitor1Dto.AssignedCount);
        Assert.Equal(0, monitor2Dto.AssignedCount);
    }
}
