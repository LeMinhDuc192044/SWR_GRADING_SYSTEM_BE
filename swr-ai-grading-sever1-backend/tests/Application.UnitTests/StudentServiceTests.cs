using Application.DTOs.Students;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.UnitTests;

public class StudentServiceTests
{
    private static async Task<(TestDbContext Db, Student Student1, Student Student2, Student Student3, Semester Sem1, Examination Exam1, ExaminationSession Sess1)>
        SeedAsync()
    {
        var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        var sem1 = new Semester
        {
            SemesterId = Guid.NewGuid(),
            Name = "Fall 2026",
            SemesterCode = "FA26",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };
        var sem2 = new Semester
        {
            SemesterId = Guid.NewGuid(),
            Name = "Spring 2027",
            SemesterCode = "SP27",
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2027, 4, 30)
        };
        db.Semesters.AddRange(sem1, sem2);

        var exam1 = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = "Architecture Final",
            Status = ExaminationStatus.Ongoing,
            SemesterId = sem1.SemesterId,
            Semester = sem1
        };
        db.Examinations.Add(exam1);

        var sess1 = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationCode = "SES_01",
            StartDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            Status = ExaminationSessionStatus.ENDED,
            SemesterId = sem1.SemesterId,
            Semester = sem1,
            ExaminationId = exam1.ExaminationId,
            Examination = exam1
        };
        db.ExaminationSessions.Add(sess1);

        var s1 = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180001",
            FullName = "Nguyen Van A",
            Email = "a@fpt.edu.vn",
            Major = "SE",
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var s2 = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180002",
            FullName = "Tran Thi B",
            Email = "b@fpt.edu.vn",
            Major = "IA",
            IsActive = true,
            IsDeleted = false,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var s3Deleted = new Student
        {
            Id = Guid.NewGuid(),
            StudentCode = "SE180003",
            FullName = "Le Van Deleted",
            Email = "deleted@fpt.edu.vn",
            Major = "SE",
            IsActive = false,
            IsDeleted = true,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        db.Students.AddRange(s1, s2, s3Deleted);

        // Liên kết s1 với sem1 và sess1
        db.StudentSemesters.Add(new StudentSemester { StudentId = s1.Id, SemesterId = sem1.SemesterId });
        db.StudentExaminationSessions.Add(new StudentExaminationSession { StudentId = s1.Id, ExaminationSessionId = sess1.ExaminationSessionId });

        // Liên kết s2 với sem2
        db.StudentSemesters.Add(new StudentSemester { StudentId = s2.Id, SemesterId = sem2.SemesterId });

        await db.SaveChangesAsync();

        return (db, s1, s2, s3Deleted, sem1, exam1, sess1);
    }

    private static StudentService CreateService(TestDbContext db)
    {
        var userRepoMock = new Mock<IUserRepository>();
        var authServiceMock = new Mock<IAuthService>();
        var emailServiceMock = new Mock<IEmailService>();
        var loggerMock = new Mock<ILogger<StudentService>>();

        return new StudentService(
            userRepoMock.Object,
            authServiceMock.Object,
            emailServiceMock.Object,
            loggerMock.Object,
            db);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_NoFilter_ReturnsOnlyNonDeletedStudents()
    {
        var (db, s1, s2, s3Deleted, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedStudentsAsync(new StudentFilterRequest());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.TotalCount);
        Assert.DoesNotContain(result.Data.Items, s => s.Id == s3Deleted.Id);
        Assert.Contains(result.Data.Items, s => s.Id == s1.Id);
        Assert.Contains(result.Data.Items, s => s.Id == s2.Id);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_FilterBySemesterId_ReturnsEnrolledStudent()
    {
        var (db, s1, s2, _, sem1, _, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedStudentsAsync(new StudentFilterRequest
        {
            SemesterId = sem1.SemesterId
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.TotalCount);
        Assert.Equal(s1.Id, result.Data.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_FilterByExaminationSessionId_ReturnsSessionStudent()
    {
        var (db, s1, _, _, _, _, sess1) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedStudentsAsync(new StudentFilterRequest
        {
            ExaminationSessionId = sess1.ExaminationSessionId
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(s1.Id, result.Data.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_FilterByExaminationId_ReturnsExamStudent()
    {
        var (db, s1, _, _, _, exam1, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedStudentsAsync(new StudentFilterRequest
        {
            ExaminationId = exam1.ExaminationId
        });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(s1.Id, result.Data.Items[0].Id);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_SearchKeyword_ReturnsMatchingStudents()
    {
        var (db, s1, s2, _, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var result = await service.GetPagedStudentsAsync(new StudentFilterRequest
        {
            Search = "SE180001"
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Items);
        Assert.Equal("SE180001", result.Data.Items[0].StudentCode);
    }

    [Fact]
    public async Task GetPagedStudentsAsync_Pagination_ReturnsCorrectPagedChunk()
    {
        var (db, _, _, _, _, _, _) = await SeedAsync();
        var service = CreateService(db);

        var page1 = await service.GetPagedStudentsAsync(new StudentFilterRequest { Page = 1, PageSize = 1 });
        var page2 = await service.GetPagedStudentsAsync(new StudentFilterRequest { Page = 2, PageSize = 1 });

        Assert.True(page1.IsSuccess);
        Assert.True(page2.IsSuccess);
        Assert.Single(page1.Data!.Items);
        Assert.Single(page2.Data!.Items);
        Assert.NotEqual(page1.Data.Items[0].Id, page2.Data.Items[0].Id);
        Assert.Equal(2, page1.Data.TotalPages);
        Assert.True(page1.Data.HasNext);
        Assert.False(page1.Data.HasPrevious);
        Assert.False(page2.Data.HasNext);
        Assert.True(page2.Data.HasPrevious);
    }
}
