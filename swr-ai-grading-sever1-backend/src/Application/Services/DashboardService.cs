using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Dashboard;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _dbContext;

    public DashboardService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<LecturerDashboardDto>> GetLecturerDashboardAsync(
        Guid lecturerId,
        Guid? semesterId = null,
        CancellationToken ct = default)
    {
        // 1. Base query for lecturer submissions
        var query = _dbContext.StudentSubmissions
            .AsNoTracking()
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.PaperSet)
            .Include(s => s.Evaluations)
            .Where(s => s.GradingDiary.CreateById == lecturerId);

        if (semesterId.HasValue && semesterId.Value != Guid.Empty)
        {
            query = query.Where(s => s.GradingDiary.PaperSet.SemesterId == semesterId.Value);
        }

        var submissionsData = await query
            .Select(s => new
            {
                s.SubmissionId,
                s.Status,
                Score = s.Evaluations
                    .Where(e => e.IsFinalScore)
                    .Select(e => e.TotalScore)
                    .FirstOrDefault()
                    ?? s.Evaluations
                        .Where(e => e.EvaluationType == EvaluationType.Lecturer)
                        .OrderByDescending(e => e.EvaluationRound)
                        .Select(e => e.TotalScore)
                        .FirstOrDefault()
                    ?? s.Evaluations
                        .Where(e => e.EvaluationType == EvaluationType.AI)
                        .OrderByDescending(e => e.EvaluationRound)
                        .Select(e => e.TotalScore)
                        .FirstOrDefault()
            })
            .ToListAsync(ct);

        var totalSubmissions = submissionsData.Count;
        var gradedSubmissions = submissionsData.Count(s =>
            s.Status == SubmissionStatus.Lecturer_Reviewed ||
            s.Status == SubmissionStatus.Final);

        var ungradedSubmissions = totalSubmissions - gradedSubmissions;
        var gradedPercentage = totalSubmissions > 0
            ? Math.Round((decimal)gradedSubmissions * 100 / totalSubmissions, 1)
            : 0;

        var scoredItems = submissionsData
            .Where(s => s.Score.HasValue)
            .Select(s => s.Score!.Value)
            .ToList();

        decimal? avgScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Average(), 1) : null;
        decimal? maxScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Max(), 1) : null;
        decimal? minScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Min(), 1) : null;

        var kpi = new LecturerDashboardKpiDto
        {
            TotalSubmissions = totalSubmissions,
            GradedSubmissions = gradedSubmissions,
            GradedPercentage = gradedPercentage,
            UngradedSubmissions = ungradedSubmissions,
            AverageScore = avgScore,
            HighestScore = maxScore,
            LowestScore = minScore
        };

        // 2. 10-Bucket Histogram
        var distribution = BuildScoreDistribution(scoredItems);

        // 3. Grading Progress (Donut Chart)
        var statusCounts = submissionsData
            .GroupBy(s => s.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        int countSubmitted = statusCounts.GetValueOrDefault(SubmissionStatus.Submitted, 0);
        int countAiGraded = statusCounts.GetValueOrDefault(SubmissionStatus.AI_Graded, 0);
        int countReviewed = statusCounts.GetValueOrDefault(SubmissionStatus.Lecturer_Reviewed, 0);
        int countFinal = statusCounts.GetValueOrDefault(SubmissionStatus.Final, 0);

        var progressList = new List<GradingProgressItemDto>
        {
            new()
            {
                StatusKey = "Submitted",
                StatusName = "Đã nộp (chưa chấm)",
                Count = countSubmitted,
                Percentage = totalSubmissions > 0 ? Math.Round((decimal)countSubmitted * 100 / totalSubmissions, 1) : 0,
                ColorHex = "#BDBDBD" // Xám/bạc theo mockup
            },
            new()
            {
                StatusKey = "AI_Graded",
                StatusName = "AI đã chấm",
                Count = countAiGraded,
                Percentage = totalSubmissions > 0 ? Math.Round((decimal)countAiGraded * 100 / totalSubmissions, 1) : 0,
                ColorHex = "#E65100" // Cam đậm theo mockup
            },
            new()
            {
                StatusKey = "Lecturer_Reviewed",
                StatusName = "GV đã duyệt",
                Count = countReviewed,
                Percentage = totalSubmissions > 0 ? Math.Round((decimal)countReviewed * 100 / totalSubmissions, 1) : 0,
                ColorHex = "#880E4F" // Đỏ mận đậm theo mockup
            },
            new()
            {
                StatusKey = "Final",
                StatusName = "Hoàn tất",
                Count = countFinal,
                Percentage = totalSubmissions > 0 ? Math.Round((decimal)countFinal * 100 / totalSubmissions, 1) : 0,
                ColorHex = "#2E7D32" // Xanh lá theo mockup
            }
        };

        // 4. Semester Comparison (Grouped Bar Chart across semesters)
        var semesterComparison = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.GradingDiary.CreateById == lecturerId)
            .GroupBy(s => new
            {
                s.GradingDiary.PaperSet.SemesterId,
                s.GradingDiary.PaperSet.Semester.SemesterCode,
                s.GradingDiary.PaperSet.Semester.Name
            })
            .Select(g => new SemesterComparisonItemDto
            {
                SemesterId = g.Key.SemesterId,
                SemesterCode = g.Key.SemesterCode,
                SemesterName = g.Key.Name,
                TotalSubmissions = g.Count(),
                GradedSubmissions = g.Count(x =>
                    x.Status == SubmissionStatus.Lecturer_Reviewed ||
                    x.Status == SubmissionStatus.Final)
            })
            .OrderBy(s => s.SemesterCode)
            .ToListAsync(ct);

        return Result<LecturerDashboardDto>.Success(new LecturerDashboardDto
        {
            Kpi = kpi,
            ScoreDistribution = distribution,
            GradingProgress = progressList,
            SemesterComparison = semesterComparison
        });
    }

    public async Task<Result<IReadOnlyList<AdminSemesterStatisticsDto>>> GetAdminSemestersStatisticsAsync(
        CancellationToken ct = default)
    {
        var semesters = await _dbContext.Semesters
            .AsNoTracking()
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);

        var result = new List<AdminSemesterStatisticsDto>();

        foreach (var semester in semesters)
        {
            var examCount = await _dbContext.Examinations
                .AsNoTracking()
                .CountAsync(e => e.SemesterId == semester.SemesterId, ct);

            var submissionsData = await _dbContext.StudentSubmissions
                .AsNoTracking()
                .Where(s => s.GradingDiary.PaperSet.SemesterId == semester.SemesterId)
                .Select(s => new
                {
                    s.SubmissionId,
                    s.Status,
                    Score = s.Evaluations
                        .Where(e => e.IsFinalScore)
                        .Select(e => e.TotalScore)
                        .FirstOrDefault()
                        ?? s.Evaluations
                            .Where(e => e.EvaluationType == EvaluationType.Lecturer)
                            .OrderByDescending(e => e.EvaluationRound)
                            .Select(e => e.TotalScore)
                            .FirstOrDefault()
                        ?? s.Evaluations
                            .Where(e => e.EvaluationType == EvaluationType.AI)
                            .OrderByDescending(e => e.EvaluationRound)
                            .Select(e => e.TotalScore)
                            .FirstOrDefault()
                })
                .ToListAsync(ct);

            var total = submissionsData.Count;
            var graded = submissionsData.Count(s =>
                s.Status == SubmissionStatus.Lecturer_Reviewed ||
                s.Status == SubmissionStatus.Final);
            var ungraded = total - graded;
            var gradedPercentage = total > 0 ? Math.Round((decimal)graded * 100 / total, 1) : 0;

            var scoredItems = submissionsData
                .Where(s => s.Score.HasValue)
                .Select(s => s.Score!.Value)
                .ToList();

            decimal? avgScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Average(), 1) : null;
            decimal? maxScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Max(), 1) : null;
            decimal? minScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Min(), 1) : null;

            int passCount = scoredItems.Count(s => s >= 5.0m);
            int failCount = scoredItems.Count(s => s < 5.0m);
            decimal passRate = scoredItems.Count > 0 ? Math.Round((decimal)passCount * 100 / scoredItems.Count, 1) : 0;
            decimal failRate = scoredItems.Count > 0 ? Math.Round((decimal)failCount * 100 / scoredItems.Count, 1) : 0;

            var distribution = BuildScoreDistribution(scoredItems);

            result.Add(new AdminSemesterStatisticsDto
            {
                SemesterId = semester.SemesterId,
                SemesterCode = semester.SemesterCode,
                SemesterName = semester.Name,
                Status = semester.Status.ToString(),
                TotalExaminations = examCount,
                TotalSubmissions = total,
                GradedSubmissions = graded,
                UngradedSubmissions = ungraded,
                GradedPercentage = gradedPercentage,
                AverageScore = avgScore,
                HighestScore = maxScore,
                LowestScore = minScore,
                PassRate = passRate,
                FailRate = failRate,
                ScoreDistribution = distribution
            });
        }

        return Result<IReadOnlyList<AdminSemesterStatisticsDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<AdminLecturerPerformanceDto>>> GetAdminLecturersPerformanceAsync(
        Guid? semesterId = null,
        CancellationToken ct = default)
    {
        var lecturers = await _dbContext.Lecturers
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.IsActive)
            .OrderBy(l => l.FullName)
            .ToListAsync(ct);

        var result = new List<AdminLecturerPerformanceDto>();

        foreach (var lecturer in lecturers)
        {
            var diariesQuery = _dbContext.GradingDiaries
                .AsNoTracking()
                .Include(d => d.PaperSet)
                .Where(d => d.CreateById == lecturer.Id);

            if (semesterId.HasValue && semesterId.Value != Guid.Empty)
            {
                diariesQuery = diariesQuery.Where(d => d.PaperSet.SemesterId == semesterId.Value);
            }

            var diaryIds = await diariesQuery.Select(d => d.GradingDiaryId).ToListAsync(ct);
            var totalDiaries = diaryIds.Count;

            var submissionsData = await _dbContext.StudentSubmissions
                .AsNoTracking()
                .Where(s => diaryIds.Contains(s.DiaryId))
                .Select(s => new
                {
                    s.SubmissionId,
                    s.Status,
                    Score = s.Evaluations
                        .Where(e => e.IsFinalScore)
                        .Select(e => e.TotalScore)
                        .FirstOrDefault()
                        ?? s.Evaluations
                            .Where(e => e.EvaluationType == EvaluationType.Lecturer)
                            .OrderByDescending(e => e.EvaluationRound)
                            .Select(e => e.TotalScore)
                            .FirstOrDefault()
                })
                .ToListAsync(ct);

            var totalSubmissions = submissionsData.Count;
            var gradedSubmissions = submissionsData.Count(s =>
                s.Status == SubmissionStatus.Lecturer_Reviewed ||
                s.Status == SubmissionStatus.Final);
            var ungradedSubmissions = totalSubmissions - gradedSubmissions;

            var completionRate = totalSubmissions > 0
                ? Math.Round((decimal)gradedSubmissions * 100 / totalSubmissions, 1)
                : 0;

            var scoredItems = submissionsData
                .Where(s => s.Score.HasValue)
                .Select(s => s.Score!.Value)
                .ToList();

            decimal? avgScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Average(), 1) : null;

            result.Add(new AdminLecturerPerformanceDto
            {
                LecturerId = lecturer.Id,
                LecturerCode = lecturer.LecturerCode,
                FullName = lecturer.FullName,
                Email = lecturer.Email,
                Subject = lecturer.Subject,
                TotalDiaries = totalDiaries,
                TotalSubmissions = totalSubmissions,
                GradedSubmissions = gradedSubmissions,
                UngradedSubmissions = ungradedSubmissions,
                CompletionRate = completionRate,
                AverageScore = avgScore
            });
        }

        return Result<IReadOnlyList<AdminLecturerPerformanceDto>>.Success(result);
    }

    private static List<ScoreDistributionBucketDto> BuildScoreDistribution(List<decimal> scores)
    {
        var ranges = new (string Range, decimal Min, decimal Max, bool IsBelowPass)[]
        {
            ("0–1", 0.0m, 1.0m, true),
            ("1–2", 1.0m, 2.0m, true),
            ("2–3", 2.0m, 3.0m, true),
            ("3–4", 3.0m, 4.0m, true),
            ("4–5", 4.0m, 5.0m, true),
            ("5–6", 5.0m, 6.0m, false),
            ("6–7", 6.0m, 7.0m, false),
            ("7–8", 7.0m, 8.0m, false),
            ("8–9", 8.0m, 9.0m, false),
            ("9–10", 9.0m, 10.0m, false),
        };

        var distribution = new List<ScoreDistributionBucketDto>();

        for (int i = 0; i < ranges.Length; i++)
        {
            var r = ranges[i];
            int count;
            if (i == ranges.Length - 1)
            {
                // Last bucket includes 10.0
                count = scores.Count(s => s >= r.Min && s <= r.Max);
            }
            else
            {
                count = scores.Count(s => s >= r.Min && s < r.Max);
            }

            distribution.Add(new ScoreDistributionBucketDto
            {
                Range = r.Range,
                Count = count,
                IsBelowPass = r.IsBelowPass
            });
        }

        return distribution;
    }
}
