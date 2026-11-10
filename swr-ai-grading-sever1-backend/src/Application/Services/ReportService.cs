using System.Text.RegularExpressions;
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Reports;
using Application.Interfaces;
using ClosedXML.Excel;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;

    public ReportService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ExaminationOverviewDto>> GetExaminationOverviewAsync(
        Guid examinationId,
        CancellationToken ct = default)
    {
        var context = await LoadReportContextAsync(examinationId, ct);
        if (context is null)
            return Result<ExaminationOverviewDto>.Failure("Examination not found.", "EXAM_NOT_FOUND");

        var submissionScores = context.Submissions
            .Select(ResolveScoreSnapshot)
            .ToList();

        var finalScores = submissionScores
            .Where(s => s.FinalOfficialScore.HasValue)
            .Select(s => s.FinalOfficialScore!.Value)
            .ToList();

        var totalSubmissions = context.Submissions.Count;
        var finalizedCount = finalScores.Count;

        var overview = new ExaminationOverviewDto
        {
            ExaminationId = context.Examination.ExaminationId,
            ExaminationCode = context.ExaminationCode,
            ExaminationName = context.Examination.Name,
            TotalSubmissions = totalSubmissions,
            AiGradedCount = submissionScores.Count(s => s.HasAiGrade),
            LecturerReviewedCount = submissionScores.Count(s => s.HasLecturerReview),
            FinalizedCount = finalizedCount,
            AverageScore = finalScores.Count > 0
                ? Math.Round(finalScores.Average(), 2, MidpointRounding.AwayFromZero)
                : null,
            HighestScore = finalScores.Count > 0 ? finalScores.Max() : null,
            LowestScore = finalScores.Count > 0 ? finalScores.Min() : null,
            CompletionRate = totalSubmissions > 0
                ? Math.Round((double)finalizedCount * 100.0 / totalSubmissions, 2, MidpointRounding.AwayFromZero)
                : 0
        };

        return Result<ExaminationOverviewDto>.Success(overview);
    }

    public async Task<Result<ScoreDistributionDto>> GetScoreDistributionAsync(
        Guid examinationId,
        CancellationToken ct = default)
    {
        var context = await LoadReportContextAsync(examinationId, ct);
        if (context is null)
            return Result<ScoreDistributionDto>.Failure("Examination not found.", "EXAM_NOT_FOUND");

        var finalScores = context.Submissions
            .Select(ResolveScoreSnapshot)
            .Where(s => s.FinalOfficialScore.HasValue)
            .Select(s => s.FinalOfficialScore!.Value)
            .ToList();

        var totalGraded = finalScores.Count;
        var buckets = BuildScoreBuckets(finalScores, totalGraded);

        return Result<ScoreDistributionDto>.Success(new ScoreDistributionDto
        {
            ExaminationId = context.Examination.ExaminationId,
            ExaminationCode = context.ExaminationCode,
            TotalGraded = totalGraded,
            Ranges = buckets
        });
    }

    public async Task<Result<ExportReportFileDto>> ExportExaminationScoresAsync(
        Guid examinationId,
        CancellationToken ct = default)
    {
        var context = await LoadReportContextAsync(examinationId, ct);
        if (context is null)
            return Result<ExportReportFileDto>.Failure("Examination not found.", "EXAM_NOT_FOUND");

        var rows = BuildExportRows(context);
        var finalizedScores = rows
            .Where(r => r.FinalOfficialScore.HasValue)
            .Select(r => r.FinalOfficialScore!.Value)
            .ToList();

        var distribution = BuildScoreBuckets(finalizedScores, finalizedScores.Count);
        var generatedAtLocal = VietnamTime.LocalNow;

        using var workbook = new XLWorkbook();
        BuildGradeSheet(workbook, context, rows, finalizedScores, generatedAtLocal);
        BuildDistributionSheet(workbook, context, rows, finalizedScores, distribution);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var safeName = SanitizeFileName(
            !string.IsNullOrWhiteSpace(context.Examination.Name)
                ? context.Examination.Name
                : context.ExaminationCode);

        return Result<ExportReportFileDto>.Success(new ExportReportFileDto
        {
            FileBytes = stream.ToArray(),
            FileName = $"BangDiem_{safeName}_{generatedAtLocal:yyyyMMdd_HHmm}.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
    }

    private async Task<ExaminationReportContext?> LoadReportContextAsync(Guid examinationId, CancellationToken ct)
    {
        var examination = await _db.Examinations
            .AsNoTracking()
            .Include(e => e.Semester)
            .FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);

        if (examination is null)
            return null;

        var examSessions = await _db.ExaminationSessions
            .AsNoTracking()
            .Where(es => es.ExaminationId == examinationId)
            .OrderBy(es => es.StartDate)
            .ThenBy(es => es.StartTime)
            .ThenBy(es => es.ExaminationCode)
            .ToListAsync(ct);

        var examSessionIds = examSessions.Select(es => es.ExaminationSessionId).ToList();

        var submissions = examSessionIds.Count == 0
            ? []
            : await _db.StudentSubmissions
                .AsNoTracking()
                .Include(s => s.Student)
                .Include(s => s.ExaminationSession)
                .Include(s => s.SubmissionGradingHistories)
                .Where(s => s.ExaminationSessionId.HasValue && examSessionIds.Contains(s.ExaminationSessionId.Value))
                .ToListAsync(ct);

        var roster = examSessionIds.Count == 0
            ? []
            : await _db.StudentExaminationSessions
                .AsNoTracking()
                .Include(ses => ses.Student)
                .Include(ses => ses.ExaminationSession)
                .Where(ses => examSessionIds.Contains(ses.ExaminationSessionId))
                .ToListAsync(ct);

        var studentIds = submissions.Select(s => s.StudentId)
            .Concat(roster.Select(r => r.StudentId))
            .Distinct()
            .ToList();

        var examStartUtc = DateTime.SpecifyKind(
            examination.StartDate.ToDateTime(TimeOnly.MinValue),
            DateTimeKind.Utc);

        var applications = studentIds.Count == 0
            ? []
            : await _db.Applications
                .AsNoTracking()
                .Where(a => studentIds.Contains(a.StudentId) && a.CreatedDay >= examStartUtc)
                .OrderByDescending(a => a.CreatedDay)
                .ToListAsync(ct);

        var latestApplicationByStudent = applications
            .GroupBy(a => a.StudentId)
            .ToDictionary(g => g.Key, g => g.First());

        var examCode = !string.IsNullOrWhiteSpace(examination.Semester?.SemesterCode)
            ? $"{examination.Semester.SemesterCode}-{examination.ExaminationType}"
            : examination.ExaminationType.ToString();

        return new ExaminationReportContext(
            examination,
            examCode,
            examSessions,
            submissions,
            roster,
            latestApplicationByStudent);
    }

    private static List<GradeReportRow> BuildExportRows(ExaminationReportContext context)
    {
        var sessionById = context.ExamSessions.ToDictionary(s => s.ExaminationSessionId);
        var submissionByKey = context.Submissions
            .GroupBy(s => (s.StudentId, s.ExaminationSessionId))
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedDate).First());

        var rows = new List<GradeReportRow>();
        var coveredKeys = new HashSet<(Guid StudentId, Guid? SessionId)>();

        foreach (var entry in context.Roster)
        {
            var key = (entry.StudentId, (Guid?)entry.ExaminationSessionId);
            coveredKeys.Add(key);

            submissionByKey.TryGetValue(key, out var submission);
            context.LatestApplicationByStudent.TryGetValue(entry.StudentId, out var application);

            rows.Add(CreateRow(entry.Student, entry.ExaminationSession, submission, application));
        }

        // Bao gồm các bài nộp mà sinh viên không nằm sẵn trong bảng student_examination_sessions
        foreach (var submission in context.Submissions)
        {
            var key = (submission.StudentId, submission.ExaminationSessionId);
            if (!coveredKeys.Add(key))
                continue;

            ExaminationSession? session = null;
            if (submission.ExaminationSessionId.HasValue)
                sessionById.TryGetValue(submission.ExaminationSessionId.Value, out session);

            context.LatestApplicationByStudent.TryGetValue(submission.StudentId, out var application);
            rows.Add(CreateRow(submission.Student, session, submission, application));
        }

        return rows
            .OrderBy(r => r.SessionCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.StudentCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static GradeReportRow CreateRow(
        Student? student,
        ExaminationSession? session,
        StudentSubmission? submission,
        Domain.Entities.Application? application)
    {
        if (submission is null)
        {
            return new GradeReportRow
            {
                StudentCode = student?.StudentCode ?? string.Empty,
                FullName = student?.FullName ?? string.Empty,
                SessionCode = session?.ExaminationCode ?? string.Empty,
                SubmissionStatus = "Chưa nộp bài",
                RegradeStatus = FormatApplicationStatus(application?.Status, hasRegradeRound: false),
                Notes = application is not null ? $"Đơn PK: {application.Status}" : "Vắng / Chưa có bài nộp"
            };
        }

        var scores = ResolveScoreSnapshot(submission);
        var regradeStatus = FormatApplicationStatus(application?.Status, scores.RegradeScore.HasValue);

        var notes = new List<string>();
        if (scores.HasPendingRegrade)
            notes.Add("Có điểm chấm lại đang chờ chốt");
        if (application?.Status == ApplicationStatus.REJECTED)
            notes.Add("Đơn phúc khảo bị từ chối");
        if (!scores.FinalOfficialScore.HasValue)
            notes.Add("Chưa chốt điểm chính thức");

        return new GradeReportRow
        {
            StudentCode = student?.StudentCode ?? string.Empty,
            FullName = student?.FullName ?? string.Empty,
            SessionCode = session?.ExaminationCode ?? submission.ExaminationSession?.ExaminationCode ?? string.Empty,
            SubmissionStatus = FormatSubmissionStatus(submission.Status),
            AiScore = scores.AiScore,
            Round1Score = scores.Round1OfficialScore,
            RegradeStatus = regradeStatus,
            RegradeScore = scores.RegradeScore,
            FinalOfficialScore = scores.FinalOfficialScore,
            Notes = string.Join("; ", notes)
        };
    }

    private static ScoreSnapshot ResolveScoreSnapshot(StudentSubmission submission)
    {
        var histories = submission.SubmissionGradingHistories
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToList();

        var latestAi = histories
            .Where(h => h.EvaluationType == EvaluationType.AI)
            .OrderByDescending(h => h.GradingRound)
            .ThenByDescending(h => h.CreatedAt)
            .FirstOrDefault();

        var lecturerHistories = histories
            .Where(h => h.EvaluationType == EvaluationType.Lecturer)
            .ToList();

        var officialHistories = histories
            .Where(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)
            .OrderBy(h => h.GradingRound)
            .ThenBy(h => h.CreatedAt)
            .ToList();

        var firstOfficial = officialHistories.FirstOrDefault();
        var latestOfficial = officialHistories.LastOrDefault();

        decimal? regradeScore = null;
        var hasPendingRegrade = false;

        if (officialHistories.Count >= 2)
        {
            regradeScore = latestOfficial?.TotalScore;
        }
        else if (firstOfficial is not null)
        {
            var laterLecturerDraft = lecturerHistories
                .Where(h => h.HistoryId != firstOfficial.HistoryId
                            && (h.GradingRound > firstOfficial.GradingRound || h.CreatedAt > firstOfficial.CreatedAt))
                .OrderByDescending(h => h.GradingRound)
                .ThenByDescending(h => h.CreatedAt)
                .FirstOrDefault();

            if (laterLecturerDraft is not null)
            {
                regradeScore = laterLecturerDraft.TotalScore;
                hasPendingRegrade = true;
            }
        }

        var hasAiGrade = latestAi is not null || submission.Status >= SubmissionStatus.AI_Graded;
        var hasLecturerReview = lecturerHistories.Count > 0 || submission.Status >= SubmissionStatus.Lecturer_Reviewed;

        return new ScoreSnapshot(
            AiScore: latestAi?.TotalScore,
            Round1OfficialScore: firstOfficial?.TotalScore,
            RegradeScore: regradeScore,
            FinalOfficialScore: latestOfficial?.TotalScore,
            HasAiGrade: hasAiGrade,
            HasLecturerReview: hasLecturerReview,
            HasPendingRegrade: hasPendingRegrade);
    }

    private static List<ScoreRangeDto> BuildScoreBuckets(IReadOnlyList<decimal> scores, int totalGraded)
    {
        var definitions = new (string Label, decimal Min, decimal Max, bool UpperInclusive)[]
        {
            ("0 - 2đ", 0m, 2m, false),
            ("2 - 4đ", 2m, 4m, false),
            ("4 - 6đ", 4m, 6m, false),
            ("6 - 8đ", 6m, 8m, false),
            ("8 - 10đ", 8m, 10m, true)
        };

        return definitions.Select(def =>
        {
            var count = scores.Count(score =>
                def.UpperInclusive
                    ? score >= def.Min && score <= def.Max
                    : score >= def.Min && score < def.Max);

            return new ScoreRangeDto
            {
                Label = def.Label,
                MinScore = def.Min,
                MaxScore = def.Max,
                Count = count,
                Percentage = totalGraded > 0
                    ? Math.Round((double)count * 100.0 / totalGraded, 2, MidpointRounding.AwayFromZero)
                    : 0
            };
        }).ToList();
    }

    private static void BuildGradeSheet(
        XLWorkbook workbook,
        ExaminationReportContext context,
        IReadOnlyList<GradeReportRow> rows,
        IReadOnlyList<decimal> finalizedScores,
        DateTime generatedAtLocal)
    {
        var ws = workbook.Worksheets.Add("BangDiem");

        ws.Cell(1, 1).Value = "FPT UNIVERSITY — BẢNG ĐIỂM TỔNG HỢP KỲ THI THỰC HÀNH";
        ws.Range(1, 1, 1, 11).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var semesterCode = context.Examination.Semester?.SemesterCode ?? "N/A";
        ws.Cell(2, 1).Value =
            $"Kỳ thi: {context.Examination.Name} | Mã: {context.ExaminationCode} | Học kỳ: {semesterCode} | Ngày xuất: {generatedAtLocal:dd/MM/yyyy HH:mm}";
        ws.Range(2, 1, 2, 11).Merge();
        ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Cell(2, 1).Style.Font.Italic = true;

        var avgText = finalizedScores.Count > 0
            ? Math.Round(finalizedScores.Average(), 2, MidpointRounding.AwayFromZero).ToString("0.00")
            : "—";
        ws.Cell(3, 1).Value =
            $"Tổng SV: {rows.Count} | Đã nộp bài: {context.Submissions.Count} | Đã chốt điểm: {finalizedScores.Count} | Điểm TB: {avgText}";
        ws.Range(3, 1, 3, 11).Merge();
        ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        const int headerRow = 5;
        var headers = new[]
        {
            "STT", "MSSV", "Họ và Tên", "Ca / Phòng thi",
            "Trạng thái bài nộp", "Điểm AI", "Điểm Lần 1",
            "Trạng thái Phúc khảo", "Điểm Phúc khảo", "Điểm Chính thức", "Ghi chú"
        };

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = ws.Cell(headerRow, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        ws.Row(headerRow).Height = 24;

        var currentRow = headerRow + 1;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            ws.Cell(currentRow, 1).Value = i + 1;
            ws.Cell(currentRow, 2).Value = row.StudentCode;
            ws.Cell(currentRow, 3).Value = row.FullName;
            ws.Cell(currentRow, 4).Value = row.SessionCode;
            ws.Cell(currentRow, 5).Value = row.SubmissionStatus;
            SetScoreCell(ws.Cell(currentRow, 6), row.AiScore);
            SetScoreCell(ws.Cell(currentRow, 7), row.Round1Score);
            ws.Cell(currentRow, 8).Value = row.RegradeStatus;
            SetScoreCell(ws.Cell(currentRow, 9), row.RegradeScore);
            SetScoreCell(ws.Cell(currentRow, 10), row.FinalOfficialScore, bold: true);
            ws.Cell(currentRow, 11).Value = row.Notes;

            ws.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(currentRow, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            for (var col = 1; col <= 11; col++)
                ws.Cell(currentRow, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            currentRow++;
        }

        ws.SheetView.FreezeRows(headerRow);
        ApplySafeColumnWidths(ws, [8, 15, 28, 18, 20, 12, 14, 22, 16, 17, 30]);
    }

    private static void BuildDistributionSheet(
        XLWorkbook workbook,
        ExaminationReportContext context,
        IReadOnlyList<GradeReportRow> rows,
        IReadOnlyList<decimal> finalizedScores,
        IReadOnlyList<ScoreRangeDto> distribution)
    {
        var ws = workbook.Worksheets.Add("PhoDiem");

        ws.Cell(1, 1).Value = $"THỐNG KÊ PHỔ ĐIỂM — {context.Examination.Name}";
        ws.Range(1, 1, 1, 3).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;

        ws.Cell(3, 1).Value = "Chỉ tiêu";
        ws.Cell(3, 2).Value = "Giá trị";
        ws.Cell(3, 1).Style.Font.Bold = true;
        ws.Cell(3, 2).Style.Font.Bold = true;

        var summaryItems = new (string Label, string Value)[]
        {
            ("Tổng sinh viên", rows.Count.ToString()),
            ("Tổng bài đã nộp", context.Submissions.Count.ToString()),
            ("Số bài đã chốt điểm", finalizedScores.Count.ToString()),
            ("Điểm trung bình", finalizedScores.Count > 0 ? Math.Round(finalizedScores.Average(), 2).ToString("0.00") : "—"),
            ("Điểm cao nhất", finalizedScores.Count > 0 ? finalizedScores.Max().ToString("0.00") : "—"),
            ("Điểm thấp nhất", finalizedScores.Count > 0 ? finalizedScores.Min().ToString("0.00") : "—")
        };

        var r = 4;
        foreach (var (label, value) in summaryItems)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 2).Value = value;
            ws.Cell(r, 1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Cell(r, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            r++;
        }

        r += 1;
        ws.Cell(r, 1).Value = "Khoảng điểm";
        ws.Cell(r, 2).Value = "Số lượng bài";
        ws.Cell(r, 3).Value = "Tỷ lệ (%)";
        for (var c = 1; c <= 3; c++)
        {
            ws.Cell(r, c).Style.Font.Bold = true;
            ws.Cell(r, c).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            ws.Cell(r, c).Style.Font.FontColor = XLColor.White;
            ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }
        r++;

        foreach (var bucket in distribution)
        {
            ws.Cell(r, 1).Value = bucket.Label;
            ws.Cell(r, 2).Value = bucket.Count;
            ws.Cell(r, 3).Value = bucket.Percentage;
            ws.Cell(r, 3).Style.NumberFormat.Format = "0.00";
            for (var c = 1; c <= 3; c++)
                ws.Cell(r, c).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            r++;
        }

        ApplySafeColumnWidths(ws, [25, 16, 16]);
    }

    private static void SetScoreCell(IXLCell cell, decimal? score, bool bold = false)
    {
        if (score.HasValue)
        {
            cell.Value = score.Value;
            cell.Style.NumberFormat.Format = "0.00";
        }
        else
        {
            cell.Value = "—";
        }

        cell.Style.Font.Bold = bold;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static void ApplySafeColumnWidths(IXLWorksheet ws, double[] widths)
    {
        for (var i = 0; i < widths.Length; i++)
            ws.Column(i + 1).Width = widths[i];
    }

    private static string FormatSubmissionStatus(SubmissionStatus status) => status switch
    {
        SubmissionStatus.Submitted => "Đã nộp",
        SubmissionStatus.AI_Graded => "AI đã chấm",
        SubmissionStatus.Lecturer_Reviewed => "Giảng viên đã chấm",
        SubmissionStatus.Final => "Đã chốt điểm",
        _ => status.ToString()
    };

    private static string FormatApplicationStatus(ApplicationStatus? status, bool hasRegradeRound)
    {
        if (status is not null)
        {
            return status.Value switch
            {
                ApplicationStatus.PENDING => "Chờ duyệt PK",
                ApplicationStatus.APPROVED => "Đã duyệt PK",
                ApplicationStatus.REJECTED => "Từ chối PK",
                ApplicationStatus.IN_PROGRESS => "Đang phúc khảo",
                ApplicationStatus.COMPLETED => "Đã phúc khảo",
                _ => status.Value.ToString()
            };
        }

        return hasRegradeRound ? "Đã chấm lại" : "Không PK";
    }

    private static string SanitizeFileName(string name)
    {
        var sanitized = Regex.Replace(name.Trim(), @"[^A-Za-z0-9_-]+", "_").Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "Report" : sanitized;
    }

    private sealed record ExaminationReportContext(
        Examination Examination,
        string ExaminationCode,
        IReadOnlyList<ExaminationSession> ExamSessions,
        IReadOnlyList<StudentSubmission> Submissions,
        IReadOnlyList<StudentExaminationSession> Roster,
        IReadOnlyDictionary<Guid, Domain.Entities.Application> LatestApplicationByStudent);

    private sealed record ScoreSnapshot(
        decimal? AiScore,
        decimal? Round1OfficialScore,
        decimal? RegradeScore,
        decimal? FinalOfficialScore,
        bool HasAiGrade,
        bool HasLecturerReview,
        bool HasPendingRegrade);

    private sealed class GradeReportRow
    {
        public string StudentCode { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string SessionCode { get; init; } = string.Empty;
        public string SubmissionStatus { get; init; } = string.Empty;
        public decimal? AiScore { get; init; }
        public decimal? Round1Score { get; init; }
        public string RegradeStatus { get; init; } = string.Empty;
        public decimal? RegradeScore { get; init; }
        public decimal? FinalOfficialScore { get; init; }
        public string Notes { get; init; } = string.Empty;
    }
}

