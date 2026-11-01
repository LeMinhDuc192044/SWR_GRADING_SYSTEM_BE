using System.Linq.Expressions;
using Application.Common;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.UnitTests;

public class ExaminationServiceTests
{
    private readonly Mock<IExaminationRepository> _examinationRepoMock = new();
    private readonly Mock<ISemesterRepository> _semesterRepoMock = new();
    private readonly Mock<IPaperSetRepository> _paperSetRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    public ExaminationServiceTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _examinationRepoMock.Setup(e => e.IsCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private ExaminationService CreateService() => new(
        _examinationRepoMock.Object,
        _semesterRepoMock.Object,
        _paperSetRepoMock.Object,
        _unitOfWorkMock.Object);

    #region Create Examination (CreateAsync) - Scenarios and Boundary Tests

    [Fact]
    public async Task CreateAsync_ValidAdminRequest_ReturnsSuccessAndMarksPaperSetUsed()
    {
        // Arrange
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var paperSetId = Guid.NewGuid();

        var semester = new Semester
        {
            SemesterId = semesterId,
            SemesterCode = "FA26",
            Name = "Fall 2026",
            StartDate = new DateOnly(2026, 9, 1),
            EndDate = new DateOnly(2026, 12, 31)
        };

        var paperSet = new PaperSet
        {
            PaperSetId = paperSetId,
            PaperSetCode = "PS000001",
            Description = "Midterm Paper Set",
            SemesterId = semesterId,
            Status = PaperSetStatus.Ready,
            IsDeleted = false
        };

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(semester);

        _paperSetRepoMock.Setup(p => p.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paperSet);

        _paperSetRepoMock.Setup(p => p.FindAsync(It.IsAny<Expression<Func<PaperSet, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaperSet> { paperSet });

        var request = new CreateExaminationRequest
        {
            Name = "Software Requirements Practical Exam",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 10, 15),
            StartTime = new TimeSpan(8, 30, 0),
            DurationMinutes = 90,
            BeforeTimeMinutes = 15,
            Note = "Room 301, Bring student ID",
            Status = ExaminationStatus.Draft,
            SemesterId = semesterId,
            PaperSetId = paperSetId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Software Requirements Practical Exam", result.Data.Name);
        Assert.Equal(90, result.Data.DurationMinutes);
        Assert.Equal(15, result.Data.BeforeTimeMinutes);
        Assert.Equal(semesterId, result.Data.SemesterId);
        Assert.Equal(paperSetId, result.Data.PaperSetId);

        // Verify PaperSet status was updated to Used
        Assert.Equal(PaperSetStatus.Used, paperSet.Status);
        Assert.NotNull(paperSet.ExaminationId);
        _paperSetRepoMock.Verify(p => p.Update(paperSet), Times.Once);
        _examinationRepoMock.Verify(e => e.AddAsync(It.IsAny<Examination>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsInvalidExamination()
    {
        // Arrange: Validation rule - Name must not be blank
        var service = CreateService();
        var request = new CreateExaminationRequest
        {
            Name = "   ",
            ExaminationType = ExaminationType.PE,
            DurationMinutes = 90,
            BeforeTimeMinutes = 10
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_EXAMINATION", result.ErrorCode);
        Assert.Equal("Examination name is required.", result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public async Task CreateAsync_ZeroOrNegativeDuration_ReturnsInvalidExamination(int duration)
    {
        // Arrange: Boundary testing - Duration must be > 0
        var service = CreateService();
        var request = new CreateExaminationRequest
        {
            Name = "Practical Exam",
            ExaminationType = ExaminationType.PE,
            DurationMinutes = duration,
            BeforeTimeMinutes = 10
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_EXAMINATION", result.ErrorCode);
        Assert.Equal("Duration must be greater than 0.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_NegativeBeforeTime_ReturnsInvalidExamination()
    {
        // Arrange: Boundary testing - BeforeTimeMinutes cannot be negative
        var service = CreateService();
        var request = new CreateExaminationRequest
        {
            Name = "Practical Exam",
            ExaminationType = ExaminationType.PE,
            DurationMinutes = 90,
            BeforeTimeMinutes = -5
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_EXAMINATION", result.ErrorCode);
        Assert.Equal("Before-time minutes cannot be negative.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_SemesterNotFound_ReturnsSemesterNotFound()
    {
        // Arrange
        var service = CreateService();
        var nonExistentSemesterId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(nonExistentSemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Semester?)null);

        var request = new CreateExaminationRequest
        {
            Name = "Practical Exam",
            ExaminationType = ExaminationType.PE,
            DurationMinutes = 90,
            BeforeTimeMinutes = 15,
            SemesterId = nonExistentSemesterId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SEMESTER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DateBeforeSemesterStartDate_ReturnsOutOfSemester()
    {
        // Arrange: Boundary testing - StartDate is before semester start
        var service = CreateService();
        var semesterId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        var request = new CreateExaminationRequest
        {
            Name = "Early Exam",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 8, 25), // 7 days prior to semester start
            DurationMinutes = 60,
            BeforeTimeMinutes = 10,
            SemesterId = semesterId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("EXAMINATION_DATE_OUT_OF_SEMESTER", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_DateAfterSemesterEndDate_ReturnsOutOfSemester()
    {
        // Arrange: Boundary testing - StartDate is after semester end
        var service = CreateService();
        var semesterId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        var request = new CreateExaminationRequest
        {
            Name = "Late Exam",
            ExaminationType = ExaminationType.RE,
            StartDate = new DateOnly(2027, 1, 10), // after semester end
            DurationMinutes = 90,
            BeforeTimeMinutes = 10,
            SemesterId = semesterId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("EXAMINATION_DATE_OUT_OF_SEMESTER", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_NonExistentPaperSet_ReturnsPaperSetNotFound()
    {
        // Arrange: Paper set does not exist
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var nonExistentPaperSetId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        _paperSetRepoMock.Setup(p => p.GetByIdAsync(nonExistentPaperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaperSet?)null);

        var request = new CreateExaminationRequest
        {
            Name = "Practical Exam",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 15),
            DurationMinutes = 90,
            BeforeTimeMinutes = 10,
            SemesterId = semesterId,
            PaperSetId = nonExistentPaperSetId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_PaperSetFromDifferentSemester_ReturnsMismatch()
    {
        // Arrange: Exam is in semester A, but paper set is registered under semester B
        var service = CreateService();
        var semesterA = Guid.NewGuid();
        var semesterB = Guid.NewGuid();
        var paperSetId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterA,
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        _paperSetRepoMock.Setup(p => p.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperSet
            {
                PaperSetId = paperSetId,
                SemesterId = semesterB, // Different semester
                IsDeleted = false
            });

        var request = new CreateExaminationRequest
        {
            Name = "Practical Exam",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 15),
            DurationMinutes = 90,
            BeforeTimeMinutes = 10,
            SemesterId = semesterA,
            PaperSetId = paperSetId
        };

        // Act
        var result = await service.CreateAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_SEMESTER_MISMATCH", result.ErrorCode);
    }

    #endregion
}
