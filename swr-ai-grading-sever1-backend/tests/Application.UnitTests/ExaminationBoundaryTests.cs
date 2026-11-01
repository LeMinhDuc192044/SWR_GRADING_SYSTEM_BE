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

/// <summary>
/// SCRUM-116: Unit tests for Examination creation boundary constraints and validation rules.
/// </summary>
public class ExaminationBoundaryTests
{
    private readonly Mock<IExaminationRepository> _examinationRepoMock = new();
    private readonly Mock<ISemesterRepository> _semesterRepoMock = new();
    private readonly Mock<IPaperSetRepository> _paperSetRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();

    public ExaminationBoundaryTests()
    {
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private ExaminationService CreateService() => new(
        _examinationRepoMock.Object,
        _semesterRepoMock.Object,
        _paperSetRepoMock.Object,
        _unitOfWorkMock.Object);

    [Fact]
    public async Task CreateAsync_PaperSetIsDeleted_ReturnsPaperSetNotFound()
    {
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var paperSetId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        var deletedPaperSet = new PaperSet
        {
            PaperSetId = paperSetId,
            SemesterId = semesterId,
            IsDeleted = true,
            Status = PaperSetStatus.Archived
        };

        _paperSetRepoMock.Setup(p => p.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedPaperSet);

        var request = new CreateExaminationRequest
        {
            Name = "Deleted Paper Exam",
            DurationMinutes = 90,
            StartDate = new DateOnly(2026, 10, 15),
            SemesterId = semesterId,
            PaperSetId = paperSetId
        };

        var result = await service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_PaperSetSemesterMismatch_ReturnsMismatch()
    {
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var otherSemesterId = Guid.NewGuid();
        var paperSetId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        var mismatchedPaperSet = new PaperSet
        {
            PaperSetId = paperSetId,
            SemesterId = otherSemesterId, // Different semester
            IsDeleted = false,
            Status = PaperSetStatus.Ready
        };

        _paperSetRepoMock.Setup(p => p.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mismatchedPaperSet);

        var request = new CreateExaminationRequest
        {
            Name = "Mismatch Paper Exam",
            DurationMinutes = 90,
            StartDate = new DateOnly(2026, 10, 15),
            SemesterId = semesterId,
            PaperSetId = paperSetId
        };

        var result = await service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_SEMESTER_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ExamDateOutOfSemester_ReturnsOutOfSemester()
    {
        var service = CreateService();
        var semesterId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                Name = "Fall 2026",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2026, 12, 31)
            });

        var request = new CreateExaminationRequest
        {
            Name = "Out of Range Exam",
            DurationMinutes = 90,
            StartDate = new DateOnly(2027, 2, 1), // Outside semester dates
            SemesterId = semesterId
        };

        var result = await service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("EXAMINATION_DATE_OUT_OF_SEMESTER", result.ErrorCode);
    }
}
