using System.Text;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.PaperSets;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using Domain.Entities;
using Domain.Enums;
using Moq;
using Xunit;

namespace Application.UnitTests;

/// <summary>
/// SCRUM-116: Unit tests for updating paper sets and adding supplementary files.
/// </summary>
public class PaperSetUpdateAndAddFilesTests
{
    private readonly Mock<IPaperSetRepository> _paperSetRepoMock = new();
    private readonly Mock<IExaminationRepository> _examinationRepoMock = new();
    private readonly Mock<ISemesterRepository> _semesterRepoMock = new();
    private readonly Mock<ISupabaseStorage> _storageMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly IApplicationDbContext _dbContext;

    public PaperSetUpdateAndAddFilesTests()
    {
        _dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    private PaperSetService CreateService() => new(
        _paperSetRepoMock.Object,
        _examinationRepoMock.Object,
        _semesterRepoMock.Object,
        _storageMock.Object,
        _unitOfWorkMock.Object,
        _userRepoMock.Object,
        _dbContext);

    [Fact]
    public async Task AddFilesAsync_EmptyFiles_ReturnsFilesRequired()
    {
        var service = CreateService();
        var paperSetId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperSet { PaperSetId = paperSetId });

        var emptyFiles = new List<MaterialFileUpload>();

        var result = await service.AddFilesAsync(paperSetId, emptyFiles);

        Assert.False(result.IsSuccess);
        Assert.Equal("FILES_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task AddFilesAsync_NonExistentPaperSet_ReturnsNotFound()
    {
        var service = CreateService();
        var nonExistentId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaperSet?)null);

        var files = new List<MaterialFileUpload>
        {
            new()
            {
                FileName = "template.docx",
                FileType = PaperSetFileType.AnswerTemplate,
                Content = new MemoryStream(Encoding.UTF8.GetBytes("fake image bytes")),
                Length = 16
            }
        };

        var result = await service.AddFilesAsync(nonExistentId, files);

        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_TrimsDescription_WhenWhitespacePresent()
    {
        var service = CreateService();
        var paperSetId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();

        var existingPaperSet = new PaperSet
        {
            PaperSetId = paperSetId,
            PaperSetCode = "PS_TRIM_01",
            Description = "Old",
            SemesterId = semesterId,
            Status = PaperSetStatus.Ready
        };

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPaperSet);

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Name = "Fall 2026" });

        var request = new UpdatePaperSetRequest
        {
            Description = "   New Trimmed Description Text   "
        };

        var result = await service.UpdateAsync(paperSetId, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Trimmed Description Text", existingPaperSet.Description);
    }
}
