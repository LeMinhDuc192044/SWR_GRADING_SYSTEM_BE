using System.IO.Compression;
using System.Text;
using Application.Common.Interfaces;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Application.Services;
using Application.UnitTests.Helpers;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Application.UnitTests;

public class SubmissionBatchUploadTests
{
    private sealed class InMemoryDocumentFile : IDocumentFile
    {
        private readonly byte[] _bytes;

        public InMemoryDocumentFile(string fileName, byte[] bytes, string contentType = "application/zip")
        {
            FileName = fileName;
            _bytes = bytes;
            ContentType = contentType;
        }

        public string FileName { get; }
        public string? ContentType { get; }
        public long Length => _bytes.Length;

        public Stream OpenReadStream() => new MemoryStream(_bytes, writable: false);

        public Task CopyToAsync(Stream target, CancellationToken cancellationToken = default)
            => target.WriteAsync(_bytes, 0, _bytes.Length, cancellationToken);
    }

    private static (Mock<ISupabaseStorage> Storage, Mock<IGeminiAIService> Gemini, List<(string Path, byte[] Bytes)> Uploads)
        CreateMocks(Guid questionId)
    {
        var uploads = new List<(string Path, byte[] Bytes)>();
        var storage = new Mock<ISupabaseStorage>();
        storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, Stream, string, CancellationToken>((path, stream, _, _) =>
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                uploads.Add((path, ms.ToArray()));
            })
            .Returns(Task.CompletedTask);

        var gemini = new Mock<IGeminiAIService>();
        gemini.Setup(g => g.ExtractContentFromDocxAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DocxExtractedContentDto { Text = "Answer content", Images = [] });
        gemini.Setup(g => g.ExtractAnswersForQuestionsAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Question>>(),
                It.IsAny<List<DocxImageDto>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExtractedQuestionAnswerDto
                {
                    QuestionId = questionId,
                    ExtractedText = "Text answer for Q1",
                    ImageIndexes = []
                }
            ]);

        return (storage, gemini, uploads);
    }

    private static async Task<(Guid SessionId, Guid ExaminerId, Guid QuestionId, Student Sv1, Student Sv2, Student Outsider)>
        SeedExamRoomAsync(TestDbContext db, bool enrollStudentsInRoom = true)
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
        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "EX100",
            FullName = "Examiner",
            Email = "ex100@fpt.edu.vn",
            Birthday = new DateOnly(1990, 1, 1),
            PasswordHash = "hash",
            Role = UserRole.Examiner,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var paperSet = new PaperSet
        {
            PaperSetId = Guid.NewGuid(),
            PaperSetCode = "PS001",
            Description = "SWR Paper",
            UploadedById = examiner.Id,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow,
            Status = PaperSetStatus.Ready,
            TotalQuestions = 1
        };
        var question = new Question
        {
            QuestionId = Guid.NewGuid(),
            PaperSetId = paperSet.PaperSetId,
            Title = "Q1",
            Content = "Draw Use Case Diagram",
            Point = 10m,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = "SWR PE",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 2),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationStatus.Ongoing,
            SemesterId = semester.SemesterId
        };
        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationCode = "ROOM_101",
            StartDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationSessionStatus.STARTED,
            SemesterId = semester.SemesterId,
            ExaminationId = exam.ExaminationId,
            PaperSetId = paperSet.PaperSetId
        };

        var sv1 = CreateStudent("SE180001", "Nguyen Van A");
        var sv2 = CreateStudent("SE180002", "Tran Thi B");
        var outsider = CreateStudent("SE189999", "Outsider Student");

        db.Semesters.Add(semester);
        db.Staff.Add(examiner);
        db.PaperSets.Add(paperSet);
        db.Questions.Add(question);
        db.Examinations.Add(exam);
        db.ExaminationSessions.Add(session);
        db.Students.AddRange(sv1, sv2, outsider);

        if (enrollStudentsInRoom)
        {
            db.StudentExaminationSessions.AddRange(
                new StudentExaminationSession { StudentId = sv1.Id, ExaminationSessionId = session.ExaminationSessionId },
                new StudentExaminationSession { StudentId = sv2.Id, ExaminationSessionId = session.ExaminationSessionId }
            );
        }

        await db.SaveChangesAsync();
        return (session.ExaminationSessionId, examiner.Id, question.QuestionId, sv1, sv2, outsider);
    }

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

    private static byte[] BuildZip(params (string EntryPath, byte[] Data)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, data) in entries)
            {
                var entry = zip.CreateEntry(path);
                using var target = entry.Open();
                target.Write(data, 0, data.Length);
            }
        }
        return ms.ToArray();
    }

    private static byte[] CreateRealDocxBytes(string text)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        }
        return ms.ToArray();
    }

    [Fact]
    public async Task UploadSubmissionsAsync_MasterZipWithFlatDocxAndInvalidEntry_ImportsValidAndRejectsUnknownWithoutFallback()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, sv2, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, uploads) = CreateMocks(questionId);

        var masterZip = BuildZip(
            ("PhongThi101/SE180001_NguyenVanA.docx", CreateRealDocxBytes("Bai lam SV1")),
            ("PhongThi101/SE180002_TranThiB.docx", CreateRealDocxBytes("Bai lam SV2")),
            ("PhongThi101/UnknownStudent.docx", CreateRealDocxBytes("Bai lam khong co ma")),
            ("PhongThi101/SE189999_Outsider.docx", CreateRealDocxBytes("Sinh vien ngoai phong thi"))
        );

        var service = new SubmissionService(db, storage.Object, gemini.Object);
        var result = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("PhongThi101.zip", masterZip)],
            examinerId,
            sessionId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(4, result.Data.TotalUploaded);
        Assert.Equal(2, result.Data.TotalSucceeded);

        var created = await db.StudentSubmissions.ToListAsync();
        Assert.Equal(2, created.Count);
        Assert.Contains(created, s => s.StudentId == sv1.Id);
        Assert.Contains(created, s => s.StudentId == sv2.Id);
        // Hai file lỗi (không mã SV và SV ngoài phòng thi) đều báo fail rõ ràng thay vì gán bừa cho students.First()
        Assert.Equal(2, result.Data.Submissions.Count(s => !s.IsSuccess));
        // Mỗi sinh viên được lưu file .docx riêng chứ không upload chung file ZIP cả phòng
        Assert.All(uploads.Where(u => u.Path.EndsWith(".docx")), u => Assert.True(u.Bytes.Length > 0));
    }

    [Fact]
    public async Task UploadSubmissionsAsync_MasterZipWithStudentFoldersAndDiagrams_GroupsSubfoldersAndIgnoresMacOsGarbage()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, sv2, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var masterZip = BuildZip(
            ("__MACOSX/._BaiLam.docx", Encoding.UTF8.GetBytes("macOS junk")),
            ("RoomA/SE180001_An/BaiLam.docx", CreateRealDocxBytes("Bai lam An")),
            ("RoomA/SE180001_An/Q1.drawio", Encoding.UTF8.GetBytes("<mxfile>an-diagram</mxfile>")),
            ("RoomA/SE180002_Binh/Word/BaiThi.docx", CreateRealDocxBytes("Bai lam Binh")),
            ("RoomA/SE180002_Binh/Diagrams/Q1.puml", Encoding.UTF8.GetBytes("@startuml\nAlice -> Bob\n@enduml"))
        );

        var service = new SubmissionService(db, storage.Object, gemini.Object);
        var result = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("RoomA.zip", masterZip)],
            examinerId,
            sessionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalUploaded);
        Assert.Equal(2, result.Data.TotalSucceeded);

        var parsed = await db.SubmissionParsedAnswers.Include(p => p.Submission).ToListAsync();
        var sv1Answer = parsed.Single(p => p.Submission.StudentId == sv1.Id);
        var sv2Answer = parsed.Single(p => p.Submission.StudentId == sv2.Id);

        Assert.Equal("<mxfile>an-diagram</mxfile>", sv1Answer.Extracted_Xml);
        Assert.Contains("<plantuml question=\"Q1\">", sv2Answer.Extracted_Xml);
    }

    [Fact]
    public async Task UploadSubmissionsAsync_MasterZipWithNestedStudentZips_ExtractsAllNestedSubmissions()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, sv2, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var sv1Zip = BuildZip(
            ("BaiLam.docx", CreateRealDocxBytes("SV1 docx")),
            ("Q1.drawio", Encoding.UTF8.GetBytes("<mxfile>sv1</mxfile>")));
        var sv2Zip = BuildZip(
            ("BaiLam.docx", CreateRealDocxBytes("SV2 docx")));

        var masterZip = BuildZip(
            ("SE180001.zip", sv1Zip),
            ("SE180002.zip", sv2Zip));

        var service = new SubmissionService(db, storage.Object, gemini.Object);
        var result = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("BatchNested.zip", masterZip)],
            examinerId,
            sessionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.TotalUploaded);
        Assert.Equal(2, result.Data.TotalSucceeded);
        Assert.Contains(await db.StudentSubmissions.ToListAsync(), s => s.StudentId == sv1.Id);
        Assert.Contains(await db.StudentSubmissions.ToListAsync(), s => s.StudentId == sv2.Id);
    }

    [Fact]
    public async Task UploadSubmissionsAsync_DocxWithoutCodeInFilename_ResolvesStudentFromDocxBody()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, _, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var docxBytes = CreateRealDocxBytes("Student Name: Nguyen Van A - Roll Number: SE180001\nAnswer 1...");
        var service = new SubmissionService(db, storage.Object, gemini.Object);

        var result = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("BaiThiCuoiKy.docx", docxBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")],
            examinerId,
            sessionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.TotalUploaded);
        Assert.Equal(1, result.Data.TotalSucceeded);
        Assert.Equal(sv1.Id, result.Data.Submissions[0].StudentId);
    }

    [Fact]
    public async Task UploadSubmissionsAsync_ZipSlipPathTraversal_FailsSafelyWithoutCrashing()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, _, _, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var evilZip = BuildZip(("../../evil/SE180001.docx", CreateRealDocxBytes("malicious")));
        var service = new SubmissionService(db, storage.Object, gemini.Object);

        var result = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("evil.zip", evilZip)],
            examinerId,
            sessionId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.TotalUploaded);
        Assert.Equal(0, result.Data.TotalSucceeded);
        Assert.False(result.Data.Submissions[0].IsSuccess);
        Assert.Empty(await db.StudentSubmissions.ToListAsync());
    }

    [Fact]
    public async Task UploadSubmissionsAsync_ReuploadBeforeGraded_ReplacesPreviousSubmissionAndCleansOldAnswers()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, _, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var service = new SubmissionService(db, storage.Object, gemini.Object);

        var initialUpload = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("SE180001_v1.docx", CreateRealDocxBytes("Version 1"))],
            examinerId,
            sessionId);

        Assert.True(initialUpload.IsSuccess);
        Assert.Equal(1, initialUpload.Data!.TotalSucceeded);
        Assert.False(initialUpload.Data.Submissions[0].IsReplaced);

        var sub1 = await db.StudentSubmissions.SingleAsync();
        var initialSubmissionId = sub1.SubmissionId;

        var secondUpload = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("SE180001_v2.docx", CreateRealDocxBytes("Version 2"))],
            examinerId,
            sessionId);

        Assert.True(secondUpload.IsSuccess);
        Assert.Equal(1, secondUpload.Data!.TotalSucceeded);
        Assert.True(secondUpload.Data.Submissions[0].IsReplaced);

        var submissions = await db.StudentSubmissions.ToListAsync();
        Assert.Single(submissions);
        Assert.Equal(initialSubmissionId, submissions[0].SubmissionId);

        var answers = await db.SubmissionParsedAnswers.Where(p => p.SubmissionId == initialSubmissionId).ToListAsync();
        Assert.Single(answers);
    }

    [Fact]
    public async Task UploadSubmissionsAsync_ReuploadAfterGraded_RejectsReplacementWithError()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (sessionId, examinerId, questionId, sv1, _, _) = await SeedExamRoomAsync(db);
        var (storage, gemini, _) = CreateMocks(questionId);

        var service = new SubmissionService(db, storage.Object, gemini.Object);

        await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("SE180001.docx", CreateRealDocxBytes("Bai lam"))],
            examinerId,
            sessionId);

        var submission = await db.StudentSubmissions.SingleAsync();

        db.Assignments.Add(new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            StaffId = examinerId,
            Status = AssignmentStatus.COMPLETED,
            AssignedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var secondUpload = await service.UploadSubmissionsAsync(
            [new InMemoryDocumentFile("SE180001_retry.docx", CreateRealDocxBytes("Nop de khi da cham"))],
            examinerId,
            sessionId);

        Assert.True(secondUpload.IsSuccess);
        Assert.Equal(1, secondUpload.Data!.TotalUploaded);
        Assert.Equal(0, secondUpload.Data.TotalSucceeded);
        Assert.False(secondUpload.Data.Submissions[0].IsSuccess);
        Assert.Contains("cannot be replaced", secondUpload.Data.Submissions[0].ErrorMessage);
    }
}

