using Application.DTOs.Users;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Application.UnitTests;

public class UserServiceTests
{
    private static async Task<(TestDbContext Db, Staff Admin, Staff Examiner, Staff Monitor, Staff Manager, Staff DeletedStaff)>
        SeedAsync()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var admin = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "ADM01",
            FullName = "Administrator User",
            Email = "admin@fpt.edu.vn",
            Role = UserRole.Admin,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "EX01",
            FullName = "Examiner Le",
            Email = "examiner@fpt.edu.vn",
            Role = UserRole.Examiner,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var monitor = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON01",
            FullName = "Monitor Tran",
            Email = "monitor@fpt.edu.vn",
            Role = UserRole.Monitor,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var manager = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MGR01",
            FullName = "Manager Nguyen",
            Email = "manager@fpt.edu.vn",
            Role = UserRole.Manager,
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        var deletedStaff = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "DEL01",
            FullName = "Deleted User",
            Email = "del@fpt.edu.vn",
            Role = UserRole.Examiner,
            IsActive = false,
            IsDeleted = true,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        db.Staff.AddRange(admin, examiner, monitor, manager, deletedStaff);
        await db.SaveChangesAsync();

        return (db, admin, examiner, monitor, manager, deletedStaff);
    }

    [Fact]
    public async Task GetPagedStaffAsync_NoFilter_ReturnsAllActiveStaffAndExcludesDeleted()
    {
        var (db, admin, examiner, monitor, manager, deletedStaff) = await SeedAsync();
        var service = new UserService(db);

        var result = await service.GetPagedStaffAsync(new StaffFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Data.TotalCount);
        Assert.DoesNotContain(result.Data.Items, s => s.Id == deletedStaff.Id);
        Assert.Contains(result.Data.Items, s => s.Id == admin.Id);
        Assert.Contains(result.Data.Items, s => s.Id == examiner.Id);
        Assert.Contains(result.Data.Items, s => s.Id == monitor.Id);
        Assert.Contains(result.Data.Items, s => s.Id == manager.Id);
    }

    [Fact]
    public async Task GetPagedStaffAsync_FilterByRole_ReturnsOnlyMatchingRole()
    {
        var (db, _, examiner, _, _, _) = await SeedAsync();
        var service = new UserService(db);

        var result = await service.GetPagedStaffAsync(new StaffFilterRequest
        {
            Role = UserRole.Examiner
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(examiner.Id, result.Data.Items[0].Id);
        Assert.Equal(UserRole.Examiner, result.Data.Items[0].Role);
    }

    [Fact]
    public async Task GetPagedStaffAsync_SearchKeyword_ReturnsMatchingStaff()
    {
        var (db, _, _, monitor, _, _) = await SeedAsync();
        var service = new UserService(db);

        var result = await service.GetPagedStaffAsync(new StaffFilterRequest
        {
            Search = "MON01"
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal(monitor.Id, result.Data.Items[0].Id);
        Assert.Equal("Monitor Tran", result.Data.Items[0].FullName);
    }

    [Fact]
    public async Task GetPagedStaffAsync_Pagination_ReturnsCorrectPages()
    {
        var (db, _, _, _, _, _) = await SeedAsync();
        var service = new UserService(db);

        var page1 = await service.GetPagedStaffAsync(new StaffFilterRequest { Page = 1, PageSize = 2 });
        var page2 = await service.GetPagedStaffAsync(new StaffFilterRequest { Page = 2, PageSize = 2 });

        Assert.True(page1.IsSuccess);
        Assert.True(page2.IsSuccess);
        Assert.Equal(2, page1.Data!.Items.Count);
        Assert.Equal(2, page2.Data!.Items.Count);
        Assert.Equal(4, page1.Data.TotalCount);
        Assert.Equal(2, page1.Data.TotalPages);
        Assert.True(page1.Data.HasNext);
        Assert.False(page2.Data.HasNext);
    }
}
