using Application.Common;
using Application.DTOs.Reports;
using Application.Services;
using Application.UnitTests.Helpers;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;

namespace Application.UnitTests;

public class ReportServiceTests
{
    [Fact]
    public async Task GetExaminationOverviewAsync_ExamNotFound_ReturnsNotFoundError()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var service = new ReportService(db);

        var result = await service.GetExaminationOverviewAsync(Guid.NewGuid());

        Assert.False(result.IsSuccess);
        Assert.Equal("EXAM_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task GetExaminationOverviewAsync_NoSubmissions_ReturnsZeroCountsAndNullScores()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var examId = await SeedBaseExamAsync(db);
        var service = new ReportService(db);

        var result = await service.GetExaminationOverviewAsync(examId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.TotalSubmissions);
        Assert.Equal(0, result.Data.FinalizedCount);
        Assert.Null(result.Data.AverageScore);
        Assert.Null(result.Data.HighestScore);
        Assert.Null(result.Data.LowestScore);
        Assert.Equal(0, result.Data.CompletionRate);
    }

    [Fact]
    public async Task GetScoreDistributionAsync_BoundaryScores_BucketedWithoutOverlapOrLoss()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (_, sessionId, staffId) = await SeedExamWithSessionAsync(db);

        decimal[] boundaryScores = [0m, 1.99m, 2.00m, 4.00m, 6.00m, 8.00m, 10.00m];
        for (var i = 0; i < boundaryScores.Length; i++)
        {
            var student = CreateStudent($"SE18000{i}", $"Student {i}");
            db.Students.Add(student);

            var sub = new StudentSubmission
            {
                SubmissionId = Guid.NewGuid(),
                FileSubmissions = $"sub-{i}.docx",
                StudentId = student.Id,
                UploadedById = staffId,
                ExaminationSessionId = sessionId,
                Status = SubmissionStatus.Final,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            db.StudentSubmissions.Add(sub);
            db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
            {
                SubmissionId = sub.SubmissionId,
                EvaluationType = EvaluationType.Lecturer,
                GradingRound = 1,
                TotalScore = boundaryScores[i],
                Status = SubmissionGradingHistoryStatus.OFFICIAL,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();

        var examId = db.ExaminationSessions.First().ExaminationId;
        var service = new ReportService(db);
        var result = await service.GetScoreDistributionAsync(examId);

        Assert.True(result.IsSuccess);
        var dist = result.Data!;
        Assert.Equal(7, dist.TotalGraded);
        Assert.Equal(7, dist.Ranges.Sum(r => r.Count));
        Assert.Equal(2, dist.Ranges[0].Count); // 0, 1.99 -> [0, 2)
        Assert.Equal(1, dist.Ranges[1].Count); // 2.00 -> [2, 4)
        Assert.Equal(1, dist.Ranges[2].Count); // 4.00 -> [4, 6)
        Assert.Equal(1, dist.Ranges[3].Count); // 6.00 -> [6, 8)
        Assert.Equal(2, dist.Ranges[4].Count); // 8.00, 10.00 -> [8, 10]
    }

    [Fact]
    public async Task ExportExaminationScoresAsync_IncludesRegradeAndAbsentStudents_ProducesValidWorkbook()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (examId, sessionId, staffId) = await SeedExamWithSessionAsync(db);

        // SV1: Có phúc khảo thành công Round 1 = 5.0, Round 2 = 8.5 -> Final = 8.5
        var sv1 = CreateStudent("SE180001", "Nguyen Van A");
        // SV2: Không phúc khảo, Round 1 = 7.0 -> Final = 7.0
        var sv2 = CreateStudent("SE180002", "Tran Thi B");
        // SV3: Có trong danh sách phòng thi nhưng vắng / chưa nộp bài
        var sv3 = CreateStudent("SE180003", "Le Van C");
        db.Students.AddRange(sv1, sv2, sv3);

        db.StudentExaminationSessions.AddRange(
            new StudentExaminationSession { StudentId = sv1.Id, ExaminationSessionId = sessionId },
            new StudentExaminationSession { StudentId = sv2.Id, ExaminationSessionId = sessionId },
            new StudentExaminationSession { StudentId = sv3.Id, ExaminationSessionId = sessionId }
        );

        var sub1 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "se180001.docx",
            StudentId = sv1.Id,
            UploadedById = staffId,
            ExaminationSessionId = sessionId,
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow.AddDays(-2),
            UpdatedDate = DateTime.UtcNow
        };
        var sub2 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            FileSubmissions = "se180002.docx",
            StudentId = sv2.Id,
            UploadedById = staffId,
            ExaminationSessionId = sessionId,
            Status = SubmissionStatus.Final,
            CreatedDate = DateTime.UtcNow.AddDays(-2),
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.AddRange(sub1, sub2);

        db.SubmissionGradingHistories.AddRange(
            new SubmissionGradingHistory
            {
                SubmissionId = sub1.SubmissionId,
                EvaluationType = EvaluationType.AI,
                GradingRound = 1,
                TotalScore = 5.0m,
                Status = SubmissionGradingHistoryStatus.DRAFT,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new SubmissionGradingHistory
            {
                SubmissionId = sub1.SubmissionId,
                EvaluationType = EvaluationType.Lecturer,
                GradingRound = 2,
                TotalScore = 5.0m,
                Status = SubmissionGradingHistoryStatus.OFFICIAL,
                CreatedAt = DateTime.UtcNow.AddDays(-2).AddHours(1)
            },
            new SubmissionGradingHistory
            {
                SubmissionId = sub1.SubmissionId,
                EvaluationType = EvaluationType.Lecturer,
                GradingRound = 3,
                TotalScore = 8.5m,
                Status = SubmissionGradingHistoryStatus.OFFICIAL,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new SubmissionGradingHistory
            {
                SubmissionId = sub2.SubmissionId,
                EvaluationType = EvaluationType.Lecturer,
                GradingRound = 1,
                TotalScore = 7.0m,
                Status = SubmissionGradingHistoryStatus.OFFICIAL,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            }
        );

        db.Applications.Add(new Domain.Entities.Application
        {
            StudentId = sv1.Id,
            reason = "Chấm thiếu câu 2",
            Status = ApplicationStatus.COMPLETED,
            CreatedDay = DateTime.UtcNow.AddDays(-1),
            UpdatedDay = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new ReportService(db);

        var overviewResult = await service.GetExaminationOverviewAsync(examId);
        Assert.True(overviewResult.IsSuccess);
        Assert.Equal(2, overviewResult.Data!.TotalSubmissions);
        Assert.Equal(2, overviewResult.Data.FinalizedCount);
        Assert.Equal(7.75m, overviewResult.Data.AverageScore); // (8.5 + 7.0) / 2
        Assert.Equal(8.5m, overviewResult.Data.HighestScore);
        Assert.Equal(7.0m, overviewResult.Data.LowestScore);

        var exportResult = await service.ExportExaminationScoresAsync(examId);
        Assert.True(exportResult.IsSuccess);
        Assert.NotNull(exportResult.Data);
        Assert.EndsWith(".xlsx", exportResult.Data.FileName);
        Assert.NotEmpty(exportResult.Data.FileBytes);

        using var ms = new MemoryStream(exportResult.Data.FileBytes);
        using var wb = new XLWorkbook(ms);
        Assert.True(wb.Worksheets.Contains("BangDiem"));
        Assert.True(wb.Worksheets.Contains("PhoDiem"));

        var sheet = wb.Worksheet("BangDiem");
        // Row 6 = SE180001, Row 7 = SE180002, Row 8 = SE180003
        Assert.Equal("SE180001", sheet.Cell(6, 2).GetString());
        Assert.Equal(5.0, sheet.Cell(6, 7).GetDouble()); // Điểm Lần 1
        Assert.Equal("Đã phúc khảo", sheet.Cell(6, 8).GetString());
        Assert.Equal(8.5, sheet.Cell(6, 9).GetDouble()); // Điểm Phúc khảo
        Assert.Equal(8.5, sheet.Cell(6, 10).GetDouble()); // Điểm Chính thức

        Assert.Equal("SE180002", sheet.Cell(7, 2).GetString());
        Assert.Equal(7.0, sheet.Cell(7, 10).GetDouble());

        Assert.Equal("SE180003", sheet.Cell(8, 2).GetString());
        Assert.Equal("Chưa nộp bài", sheet.Cell(8, 5).GetString());
    }

    private static async Task<Guid> SeedBaseExamAsync(TestDbContext db)
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
        var exam = new Examination
        {
            ExaminationId = Guid.NewGuid(),
            Name = "SWR302 Practical Exam",
            ExaminationType = ExaminationType.PE,
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 15),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationStatus.Ongoing,
            SemesterId = semester.SemesterId
        };
        db.Semesters.Add(semester);
        db.Examinations.Add(exam);
        await db.SaveChangesAsync();
        return exam.ExaminationId;
    }

    private static async Task<(Guid ExamId, Guid SessionId, Guid StaffId)> SeedExamWithSessionAsync(TestDbContext db)
    {
        var examId = await SeedBaseExamAsync(db);
        var exam = db.Examinations.First(e => e.ExaminationId == examId);

        var session = new ExaminationSession
        {
            ExaminationSessionId = Guid.NewGuid(),
            ExaminationCode = "SWR302_R101",
            StartDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow,
            Status = ExaminationSessionStatus.STARTED,
            SemesterId = exam.SemesterId,
            ExaminationId = examId
        };

        var examiner = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "EX001",
            FullName = "Examiner One",
            Email = "ex001@fpt.edu.vn",
            Birthday = new DateOnly(1990, 1, 1),
            PasswordHash = "hash",
            Role = UserRole.Examiner,
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };

        db.ExaminationSessions.Add(session);
        db.Staff.Add(examiner);
        await db.SaveChangesAsync();

        return (examId, session.ExaminationSessionId, examiner.Id);
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

    [Fact]
    public async Task GetScoreDistributionComparisonAsync_ReturnsAiAndHumanBuckets()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (examId, sessionId, staffId) = await SeedExamWithSessionAsync(db);

        // Sub 1: AI = 7.0, Human = 8.0 (OFFICIAL)
        var student1 = CreateStudent("SE180010", "Student 10");
        db.Students.Add(student1);
        var sub1 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student1.Id,
            FileSubmissions = "sub1.docx",
            ExaminationSessionId = sessionId,
            UploadedById = staffId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.Add(sub1);
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub1.SubmissionId,
            EvaluationType = EvaluationType.AI,
            GradingRound = 1,
            TotalScore = 7.0m,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow
        });
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub1.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 1,
            TotalScore = 8.0m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        });

        // Sub 2: AI = 1.5, Human = 2.5 (OFFICIAL)
        var student2 = CreateStudent("SE180011", "Student 11");
        db.Students.Add(student2);
        var sub2 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student2.Id,
            FileSubmissions = "sub2.docx",
            ExaminationSessionId = sessionId,
            UploadedById = staffId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.Add(sub2);
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub2.SubmissionId,
            EvaluationType = EvaluationType.AI,
            GradingRound = 1,
            TotalScore = 1.5m,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow
        });
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub2.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            GradingRound = 1,
            TotalScore = 2.5m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new ReportService(db);
        var result = await service.GetScoreDistributionComparisonAsync(examId, null);

        Assert.True(result.IsSuccess);
        var data = result.Data!;
        Assert.Equal(2, data.TotalOfficialSubmissions);
        Assert.Equal(2, data.TotalAiGradedSubmissions);
        Assert.Equal(2, data.TotalHumanOfficialSubmissions);

        // AI: 1.5 in [0, 2), 7.0 in [6, 8)
        var aiBucket02 = data.Buckets.First(b => b.MinScore == 0m);
        var aiBucket68 = data.Buckets.First(b => b.MinScore == 6m);
        Assert.Equal(1, aiBucket02.AiCount);
        Assert.Equal(1, aiBucket68.AiCount);

        // Human: 2.5 in [2, 4), 8.0 in [8, 10]
        var humanBucket24 = data.Buckets.First(b => b.MinScore == 2m);
        var humanBucket810 = data.Buckets.First(b => b.MinScore == 8m);
        Assert.Equal(1, humanBucket24.HumanCount);
        Assert.Equal(1, humanBucket810.HumanCount);
    }

    [Fact]
    public async Task GetAiHumanScoreStatisticsAsync_CalculatesAccurateDeviations()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (examId, sessionId, staffId) = await SeedExamWithSessionAsync(db);

        // Sub 1: AI = 8.0, Human = 7.5 (diff = +0.5, abs = 0.5)
        var student1 = CreateStudent("SE180020", "Student 20");
        db.Students.Add(student1);
        var sub1 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student1.Id,
            FileSubmissions = "sub1.docx",
            ExaminationSessionId = sessionId,
            UploadedById = staffId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.Add(sub1);
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub1.SubmissionId,
            EvaluationType = EvaluationType.AI,
            TotalScore = 8.0m,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow
        });
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub1.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            TotalScore = 7.5m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        });

        // Sub 2: AI = 6.0, Human = 7.0 (diff = -1.0, abs = 1.0)
        var student2 = CreateStudent("SE180021", "Student 21");
        db.Students.Add(student2);
        var sub2 = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student2.Id,
            FileSubmissions = "sub2.docx",
            ExaminationSessionId = sessionId,
            UploadedById = staffId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.Add(sub2);
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub2.SubmissionId,
            EvaluationType = EvaluationType.AI,
            TotalScore = 6.0m,
            Status = SubmissionGradingHistoryStatus.DRAFT,
            CreatedAt = DateTime.UtcNow
        });
        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub2.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            TotalScore = 7.0m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new ReportService(db);
        var result = await service.GetAiHumanScoreStatisticsAsync(examId, null);

        Assert.True(result.IsSuccess);
        var stats = result.Data!;
        Assert.Equal(2, stats.TotalOfficialSubmissions);
        Assert.Equal(2, stats.BothGradedCount);

        // Human: avg = 7.25, max = 7.5, min = 7.0
        Assert.Equal(7.25m, stats.HumanAverageScore);
        Assert.Equal(7.5m, stats.HumanHighestScore);
        Assert.Equal(7.0m, stats.HumanLowestScore);

        // AI: avg = 7.0, max = 8.0, min = 6.0
        Assert.Equal(7.0m, stats.AiAverageScore);
        Assert.Equal(8.0m, stats.AiHighestScore);
        Assert.Equal(6.0m, stats.AiLowestScore);

        // Diff: (0.5 + -1.0)/2 = -0.25; MAE: (0.5 + 1.0)/2 = 0.75
        Assert.Equal(-0.25m, stats.AverageScoreDifference);
        Assert.Equal(0.75m, stats.MeanAbsoluteError);
        Assert.Equal(1, stats.AiHigherCount);
        Assert.Equal(1, stats.HumanHigherCount);
        Assert.Equal(0, stats.EqualCount);
        // Sub 1 has diff 0.5 <= 0.5 -> 1/2 = 50%
        Assert.Equal(50.0, stats.AcceptableDeviationRate);
    }

    [Fact]
    public async Task GetMonitorPerformanceAsync_ReturnsKPIAndPagedMonitors()
    {
        using var db = TestDbContextFactory.Create(Guid.NewGuid().ToString());
        var (examId, sessionId, staffId) = await SeedExamWithSessionAsync(db);

        var monitor = new Staff
        {
            Id = Guid.NewGuid(),
            StaffCode = "MON01",
            FullName = "Monitor One",
            Email = "mon01@fpt.edu.vn",
            Role = UserRole.Monitor,
            Birthday = new DateOnly(1992, 1, 1),
            PasswordHash = "hash",
            CreatedDay = DateTime.UtcNow,
            UpdatedDay = DateTime.UtcNow
        };
        db.Staff.Add(monitor);

        var gradingSession = new GradingSession
        {
            GradingSessionId = Guid.NewGuid(),
            Title = "Đợt chấm thi 1",
            ExaminationSessionId = sessionId,
            Type = GradingSessionType.Normal_Grading,
            CreatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            UpdatedDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Deadline = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            NumberSubmissions = 2
        };
        db.GradingSessions.Add(gradingSession);

        var student = CreateStudent("SE180030", "Student 30");
        db.Students.Add(student);
        var sub = new StudentSubmission
        {
            SubmissionId = Guid.NewGuid(),
            StudentId = student.Id,
            FileSubmissions = "sub.docx",
            ExaminationSessionId = sessionId,
            UploadedById = staffId,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        db.StudentSubmissions.Add(sub);

        db.Assignments.Add(new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            StaffId = monitor.Id,
            GradingSessionId = gradingSession.GradingSessionId,
            SubmissionId = sub.SubmissionId,
            Status = AssignmentStatus.GRADED,
            AssignedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        });

        db.SubmissionGradingHistories.Add(new SubmissionGradingHistory
        {
            SubmissionId = sub.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            TotalScore = 8.5m,
            Status = SubmissionGradingHistoryStatus.OFFICIAL,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var service = new ReportService(db);
        var result = await service.GetMonitorPerformanceAsync(examId, null, null, new PagedRequest { Page = 1, PageSize = 10 });

        Assert.True(result.IsSuccess);
        var report = result.Data!;
        Assert.Equal(1, report.TotalMonitors);
        Assert.Equal(1, report.TotalAssignments);
        Assert.Equal(1, report.TotalCompletedAssignments);
        Assert.Equal(100.0, report.OverallCompletionRate);

        Assert.Single(report.PagedMonitors.Items);
        var monitorItem = report.PagedMonitors.Items[0];
        Assert.Equal("MON01", monitorItem.StaffCode);
        Assert.Equal("Monitor One", monitorItem.FullName);
        Assert.Equal(1, monitorItem.AssignedCount);
        Assert.Equal(1, monitorItem.CompletedCount);
        Assert.Equal(100.0, monitorItem.CompletionRate);
        Assert.Equal(8.5m, monitorItem.AverageGivenScore);
    }
}

