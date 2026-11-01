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
/// SCRUM-116: Unit tests for boundary conditions and file upload validations for PaperSet.
/// </summary>
public class PaperSetBoundaryTests
{
    private readonly Mock<IPaperSetRepository> _paperSetRepoMock = new();
    private readonly Mock<IExaminationRepository> _examinationRepoMock = new();
    private readonly Mock<ISemesterRepository> _semesterRepoMock = new();
    private readonly Mock<ISupabaseStorage> _storageMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly IApplicationDbContext _dbContext;

    public PaperSetBoundaryTests()
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

    private static MaterialFileUpload CreateMockFile(string fileName, PaperSetFileType type, byte[] data)
    {
        return new MaterialFileUpload
        {
            FileName = fileName,
            FileType = type,
            Content = new MemoryStream(data),
            ContentType = "application/octet-stream",
            Length = data.Length
        };
    }

    [Theory]
    [InlineData("malicious_script.exe")]
    [InlineData("payload.bat")]
    [InlineData("shell.sh")]
    [InlineData("trojan.dll")]
    public async Task CreateAsync_UnsupportedFileExtension_ReturnsInvalidFileType(string maliciousFilename)
    {
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Status = SemesterStatus.Active });

        _userRepoMock.Setup(u => u.GetLecturerByIdAsync(lecturerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lecturer { Id = lecturerId, FullName = "Dr. Lecturer" });

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile(maliciousFilename, PaperSetFileType.Question, Encoding.UTF8.GetBytes("malicious content"))
        };

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        var result = await service.CreateAsync(semesterId, "Security test paper", questions, files, lecturerId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_InvalidRubricExtension_Txt_ReturnsInvalidFileType()
    {
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Status = SemesterStatus.Active });

        _userRepoMock.Setup(u => u.GetLecturerByIdAsync(lecturerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lecturer { Id = lecturerId, FullName = "Dr. Lecturer" });

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("rubric.txt", PaperSetFileType.AnswerRubric, Encoding.UTF8.GetBytes("not excel rubric"))
        };

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        var result = await service.CreateAsync(semesterId, "Invalid rubric format", questions, files, lecturerId);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
    }
}
