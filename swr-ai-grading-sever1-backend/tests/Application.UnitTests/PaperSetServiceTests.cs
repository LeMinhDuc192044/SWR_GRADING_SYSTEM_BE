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

public class PaperSetServiceTests
{
    private readonly Mock<IPaperSetRepository> _paperSetRepoMock = new();
    private readonly Mock<IExaminationRepository> _examinationRepoMock = new();
    private readonly Mock<ISemesterRepository> _semesterRepoMock = new();
    private readonly Mock<ISupabaseStorage> _storageMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly IApplicationDbContext _dbContext;

    public PaperSetServiceTests()
    {
        _dbContext = TestDbContextFactory.Create(Guid.NewGuid().ToString());

        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _storageMock.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _storageMock.Setup(s => s.GetMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SupabaseFileMetadata
            {
                FileName = "test_file.docx",
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileSize = 1024
            });

        _paperSetRepoMock.Setup(r => r.IsCodeExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    private PaperSetService CreateService() => new(
        _paperSetRepoMock.Object,
        _examinationRepoMock.Object,
        _semesterRepoMock.Object,
        _storageMock.Object,
        _unitOfWorkMock.Object,
        _userRepoMock.Object,
        _dbContext);

    private static MaterialFileUpload CreateMockFile(
        string fileName,
        PaperSetFileType type,
        string content = "Dummy file content")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new MaterialFileUpload
        {
            FileName = fileName,
            FileType = type,
            Content = new MemoryStream(bytes),
            ContentType = "application/octet-stream",
            Length = bytes.Length
        };
    }

    #region Upload Paper Set (CreateAsync) - Role and Validation Tests

    [Fact]
    public async Task CreateAsync_ValidLecturerAndFiles_ReturnsSuccess()
    {
        // Arrange: Role = Lecturer
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                Name = "Fall 2026",
                SemesterCode = "FA26",
                Status = SemesterStatus.Active
            });

        _userRepoMock.Setup(u => u.GetLecturerByIdAsync(lecturerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lecturer
            {
                Id = lecturerId,
                FullName = "Dr. Lecturer Nguyen",
                LecturerCode = "LEC001"
            });

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Question 1", Content = "Context diagram explanation", Point = 5.0m, QuestionNumber = 1 },
            new() { Title = "Question 2", Content = "Use case specification", Point = 5.0m, QuestionNumber = 2 }
        };

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Exam_Question.docx", PaperSetFileType.Question)
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Midterm Exam Paper", questions, files, lecturerId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        var created = result.Data[0];
        Assert.Equal("Midterm Exam Paper", created.Description);
        Assert.Equal(2, created.TotalQuestions);
        Assert.Equal(PaperSetStatus.Ready, created.Status);
        Assert.Equal(lecturerId, created.CreateById);
        _paperSetRepoMock.Verify(r => r.AddAsync(It.IsAny<PaperSet>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonLecturerRole_Student_ReturnsLecturerRequired()
    {
        // Arrange: Role = Student (User has no Lecturer record in DB)
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Name = "Fall 2026", Status = SemesterStatus.Active });

        _userRepoMock.Setup(u => u.GetLecturerByIdAsync(studentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lecturer?)null);

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Question content", Point = 10m }
        };
        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Exam.docx", PaperSetFileType.Question)
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Student Upload Attempt", questions, files, studentUserId);

        // Assert: Access Denied for Student
        Assert.False(result.IsSuccess);
        Assert.Equal("LECTURER_REQUIRED", result.ErrorCode);
        Assert.Equal("Only a lecturer can create paper sets.", result.Error);
        _paperSetRepoMock.Verify(r => r.AddAsync(It.IsAny<PaperSet>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_AdminWithoutLecturerProfile_ReturnsLecturerRequired()
    {
        // Arrange: Role = Admin (Admin without Lecturer profile)
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Name = "Fall 2026", Status = SemesterStatus.Active });

        _userRepoMock.Setup(u => u.GetLecturerByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lecturer?)null);

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Admin question", Point = 10m }
        };
        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("AdminExam.docx", PaperSetFileType.Question)
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Admin Upload Attempt", questions, files, adminUserId);

        // Assert: System requires Lecturer profile to author exam paper sets
        Assert.False(result.IsSuccess);
        Assert.Equal("LECTURER_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_EmptyFiles_ReturnsFilesRequired()
    {
        // Arrange
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        // Act: No files attached
        var result = await service.CreateAsync(semesterId, "Empty Files", questions, new List<MaterialFileUpload>(), lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("FILES_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_InvalidQuestionFileExtension_ReturnsInvalidFileType()
    {
        // Arrange: Invalid extension .pdf for question file
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Question.pdf", PaperSetFileType.Question)
        };

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Invalid Ext", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_InvalidRubricExtension_ReturnsInvalidFileType()
    {
        // Arrange: Rubric must be .xls/.xlsx, here uploading .csv
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Rubric.csv", PaperSetFileType.AnswerRubric)
        };

        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Invalid Rubric Ext", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_FILE_TYPE", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_NonExistentSemester_ReturnsSemesterNotFound()
    {
        // Arrange: Semester doesn't exist
        var service = CreateService();
        var nonExistentSemesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(nonExistentSemesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Semester?)null);

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Question.docx", PaperSetFileType.Question)
        };
        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        // Act
        var result = await service.CreateAsync(nonExistentSemesterId, "Desc", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SEMESTER_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ClosedSemester_ReturnsSemesterClosed()
    {
        // Arrange: Semester is Closed
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester
            {
                SemesterId = semesterId,
                Name = "Spring 2025",
                Status = SemesterStatus.Closed
            });

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Question.docx", PaperSetFileType.Question)
        };
        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = 10m }
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Desc", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SEMESTER_CLOSED", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_NegativeQuestionPoint_ReturnsInvalidQuestion()
    {
        // Arrange: Boundary testing - negative points
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Question.docx", PaperSetFileType.Question)
        };
        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "Q1", Content = "Content", Point = -1.5m }
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Negative Point", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUESTION", result.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_EmptyQuestionTitle_ReturnsInvalidQuestion()
    {
        // Arrange: Boundary testing - empty title
        var service = CreateService();
        var semesterId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();

        var files = new List<MaterialFileUpload>
        {
            CreateMockFile("Question.docx", PaperSetFileType.Question)
        };
        var questions = new List<CreateQuestionInput>
        {
            new() { Title = "   ", Content = "Content with blank title", Point = 5m }
        };

        // Act
        var result = await service.CreateAsync(semesterId, "Blank Title", questions, files, lecturerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_QUESTION", result.ErrorCode);
    }

    #endregion

    #region Update Paper Set (UpdateAsync) - Scenarios and Edge Cases

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesDescriptionAndQuestions()
    {
        // Arrange
        var service = CreateService();
        var paperSetId = Guid.NewGuid();
        var semesterId = Guid.NewGuid();

        var existingPaperSet = new PaperSet
        {
            PaperSetId = paperSetId,
            PaperSetCode = "PS000123",
            Description = "Old Description",
            TotalQuestions = 2,
            SemesterId = semesterId,
            Status = PaperSetStatus.Ready
        };

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingPaperSet);

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterId, Name = "Fall 2026" });

        var request = new UpdatePaperSetRequest
        {
            Description = "Updated New Description",
            TotalQuestions = 4,
            Status = PaperSetStatus.Ready
        };

        // Act
        var result = await service.UpdateAsync(paperSetId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Updated New Description", existingPaperSet.Description);
        Assert.Equal(4, existingPaperSet.TotalQuestions);
        _paperSetRepoMock.Verify(r => r.Update(existingPaperSet), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_NonExistentPaperSet_ReturnsPaperSetNotFound()
    {
        // Arrange
        var service = CreateService();
        var nonExistentId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaperSet?)null);

        var request = new UpdatePaperSetRequest { Description = "Update Attempt" };

        // Act
        var result = await service.UpdateAsync(nonExistentId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PAPER_SET_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_NegativeTotalQuestions_ReturnsInvalidTotalQuestions()
    {
        // Arrange: Boundary testing - TotalQuestions = -1
        var service = CreateService();
        var paperSetId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperSet { PaperSetId = paperSetId });

        var request = new UpdatePaperSetRequest { TotalQuestions = -1 };

        // Act
        var result = await service.UpdateAsync(paperSetId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_TOTAL_QUESTIONS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_InvalidStatusValue_ReturnsInvalidStatus()
    {
        // Arrange: Undefined enum value
        var service = CreateService();
        var paperSetId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperSet { PaperSetId = paperSetId });

        var request = new UpdatePaperSetRequest { Status = (PaperSetStatus)999 };

        // Act
        var result = await service.UpdateAsync(paperSetId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_STATUS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_ExamFromDifferentSemester_ReturnsMismatch()
    {
        // Arrange
        var service = CreateService();
        var paperSetId = Guid.NewGuid();
        var semesterA = Guid.NewGuid();
        var semesterB = Guid.NewGuid();
        var examId = Guid.NewGuid();

        _paperSetRepoMock.Setup(r => r.GetByIdAsync(paperSetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaperSet { PaperSetId = paperSetId, SemesterId = semesterA });

        _semesterRepoMock.Setup(s => s.GetByIdAsync(semesterA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Semester { SemesterId = semesterA });

        // Examination belongs to Semester B
        _examinationRepoMock.Setup(e => e.GetByIdAsync(examId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Examination { ExaminationId = examId, SemesterId = semesterB });

        var request = new UpdatePaperSetRequest { ExaminationId = examId };

        // Act
        var result = await service.UpdateAsync(paperSetId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("EXAMINATION_SEMESTER_MISMATCH", result.ErrorCode);
    }

    #endregion
}
