using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Reports;
using Application.Interfaces;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class ReportService : IReportService
{
    private readonly IApplicationDbContext _dbContext;

    public ReportService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Thống kê tổng quan kỳ thi thực hành (PE).
    /// </summary>
    public async Task<Result<ExaminationOverviewDto>> GetExaminationOverviewAsync(Guid examinationId, CancellationToken ct = default)
    {
        var exam = await _dbContext.Examinations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);

        if (exam is null)
        {
            return Result<ExaminationOverviewDto>.Failure("Không tìm thấy kỳ thi.", "EXAM_NOT_FOUND");
        }

        // Lấy tất cả bài nộp thuộc kỳ thi này
        var submissions = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.StudentExamination.ExamId == examinationId || s.GradingDiary.PaperSet.ExaminationId == examinationId)
            .ToListAsync(ct);

        var totalSubmissions = submissions.Count;
        var aiGradedCount = submissions.Count(s => s.Status >= SubmissionStatus.AI_Graded);
        var lecturerReviewedCount = submissions.Count(s => s.Status >= SubmissionStatus.Lecturer_Reviewed);
        var finalizedCount = submissions.Count(s => s.Status == SubmissionStatus.Final);

        // Lấy danh sách điểm hiệu lực (ưu tiên điểm GV, sau đó điểm AI)
        var validScores = submissions
            .Select(s => s.LecturerScore ?? s.AiScore)
            .Where(score => score.HasValue)
            .Select(score => score!.Value)
            .ToList();

        decimal? averageScore = validScores.Count > 0 ? Math.Round(validScores.Average(), 2) : null;
        decimal? highestScore = validScores.Count > 0 ? validScores.Max() : null;
        decimal? lowestScore = validScores.Count > 0 ? validScores.Min() : null;

        var completionRate = totalSubmissions > 0
            ? Math.Round((double)finalizedCount / totalSubmissions * 100, 2)
            : 0;

        var dto = new ExaminationOverviewDto
        {
            ExaminationId = exam.ExaminationId,
            ExaminationCode = exam.ExaminationCode,
            ExaminationName = exam.Name,
            TotalSubmissions = totalSubmissions,
            AiGradedCount = aiGradedCount,
            LecturerReviewedCount = lecturerReviewedCount,
            FinalizedCount = finalizedCount,
            AverageScore = averageScore,
            HighestScore = highestScore,
            LowestScore = lowestScore,
            CompletionRate = completionRate
        };

        return Result<ExaminationOverviewDto>.Success(dto);
    }

    /// <summary>
    /// Phổ điểm bài thi thực hành theo các khoảng 0-2, 2-4, 4-6, 6-8, 8-10.
    /// </summary>
    public async Task<Result<ScoreDistributionDto>> GetScoreDistributionAsync(Guid examinationId, CancellationToken ct = default)
    {
        var exam = await _dbContext.Examinations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);

        if (exam is null)
        {
            return Result<ScoreDistributionDto>.Failure("Không tìm thấy kỳ thi.", "EXAM_NOT_FOUND");
        }

        var scores = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.StudentExamination.ExamId == examinationId || s.GradingDiary.PaperSet.ExaminationId == examinationId)
            .Select(s => s.LecturerScore ?? s.AiScore)
            .Where(s => s.HasValue)
            .Select(s => s!.Value)
            .ToListAsync(ct);

        var total = scores.Count;

        var rangesDef = new[]
        {
            new { Label = "0 - <2đ", Min = 0.0m, Max = 2.0m, IncludeUpper = false },
            new { Label = "2 - <4đ", Min = 2.0m, Max = 4.0m, IncludeUpper = false },
            new { Label = "4 - <6đ", Min = 4.0m, Max = 6.0m, IncludeUpper = false },
            new { Label = "6 - <8đ", Min = 6.0m, Max = 8.0m, IncludeUpper = false },
            new { Label = "8 - 10đ", Min = 8.0m, Max = 10.0m, IncludeUpper = true }
        };

        var rangeDtos = rangesDef.Select(r =>
        {
            var count = scores.Count(s =>
                r.IncludeUpper
                    ? (s >= r.Min && s <= r.Max)
                    : (s >= r.Min && s < r.Max));

            var percentage = total > 0 ? Math.Round((double)count / total * 100, 2) : 0;

            return new ScoreRangeDto
            {
                Label = r.Label,
                MinScore = r.Min,
                MaxScore = r.Max,
                Count = count,
                Percentage = percentage
            };
        }).ToList();

        var dto = new ScoreDistributionDto
        {
            ExaminationId = exam.ExaminationId,
            ExaminationCode = exam.ExaminationCode,
            TotalGraded = total,
            Ranges = rangeDtos
        };

        return Result<ScoreDistributionDto>.Success(dto);
    }

    /// <summary>
    /// Xuất toàn bộ bảng điểm của kỳ thi ra file Excel (.xlsx).
    /// </summary>
    public async Task<Result<ExportReportFileDto>> ExportExaminationScoresAsync(Guid examinationId, CancellationToken ct = default)
    {
        var exam = await _dbContext.Examinations
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);

        if (exam is null)
        {
            return Result<ExportReportFileDto>.Failure("Không tìm thấy kỳ thi.", "EXAM_NOT_FOUND");
        }

        var submissions = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Include(s => s.GradingDiary)
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Where(s => s.StudentExamination.ExamId == examinationId || s.GradingDiary.PaperSet.ExaminationId == examinationId)
            .OrderBy(s => s.StudentExamination.Student.StudentCode)
            .ToListAsync(ct);

        using var memoryStream = new MemoryStream();
        using (var spreadsheetDocument = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
        {
            var workbookPart = spreadsheetDocument.AddWorkbookPart();
            workbookPart.Workbook = new Workbook();

            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData();
            worksheetPart.Worksheet = new Worksheet(sheetData);

            var sheets = spreadsheetDocument.WorkbookPart!.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet
            {
                Id = spreadsheetDocument.WorkbookPart.GetIdOfPart(worksheetPart),
                SheetId = 1,
                Name = "BangDiem"
            });

            // Tiêu đề cột
            var headers = new[]
            {
                "STT", "MSSV", "Họ và tên", "Tên file bài làm", "Sổ chấm thi",
                "Điểm AI", "Điểm Giảng viên", "Điểm Chốt (Final)", "Trạng thái", "Nhận xét của GV"
            };

            var headerRow = new Row();
            foreach (var header in headers)
            {
                headerRow.Append(CreateCell(header));
            }
            sheetData.Append(headerRow);

            // Dữ liệu từng bài nộp
            int stt = 1;
            foreach (var sub in submissions)
            {
                var row = new Row();
                row.Append(CreateCell(stt++.ToString()));
                row.Append(CreateCell(sub.StudentExamination?.Student?.StudentCode ?? string.Empty));
                row.Append(CreateCell(sub.StudentExamination?.Student?.FullName ?? string.Empty));
                row.Append(CreateCell(sub.SubmissionFile));
                row.Append(CreateCell(sub.GradingDiary?.Name ?? string.Empty));
                row.Append(CreateCell(sub.AiScore?.ToString("0.00") ?? ""));
                row.Append(CreateCell(sub.LecturerScore?.ToString("0.00") ?? ""));
                row.Append(CreateCell(sub.Status == SubmissionStatus.Final ? (sub.LecturerScore?.ToString("0.00") ?? "") : "Chưa chốt"));
                row.Append(CreateCell(sub.Status.ToString()));
                row.Append(CreateCell(sub.Comment ?? string.Empty));
                sheetData.Append(row);
            }

            workbookPart.Workbook.Save();
        }

        var fileName = $"BangDiem_{exam.ExaminationCode}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return Result<ExportReportFileDto>.Success(new ExportReportFileDto
        {
            FileBytes = memoryStream.ToArray(),
            FileName = fileName,
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        });
    }

    private static Cell CreateCell(string text)
    {
        return new Cell
        {
            DataType = CellValues.String,
            CellValue = new CellValue(text ?? string.Empty)
        };
    }
}
