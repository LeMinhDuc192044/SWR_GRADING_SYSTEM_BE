namespace Application.DTOs.Dashboard;

public class LecturerDashboardDto
{
    public LecturerDashboardKpiDto Kpi { get; set; } = new();
    public List<ScoreDistributionBucketDto> ScoreDistribution { get; set; } = [];
    public List<GradingProgressItemDto> GradingProgress { get; set; } = [];
    public List<SemesterComparisonItemDto> SemesterComparison { get; set; } = [];
}

public class LecturerDashboardKpiDto
{
    public int TotalSubmissions { get; set; }
    public int GradedSubmissions { get; set; }
    public decimal GradedPercentage { get; set; }
    public int UngradedSubmissions { get; set; }
    public decimal? AverageScore { get; set; }
    public decimal? HighestScore { get; set; }
    public decimal? LowestScore { get; set; }
}

public class ScoreDistributionBucketDto
{
    public string Range { get; set; } = string.Empty;
    public int Count { get; set; }
    public bool IsBelowPass { get; set; }
}

public class GradingProgressItemDto
{
    public string StatusKey { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal Percentage { get; set; }
    public string ColorHex { get; set; } = string.Empty;
}

public class SemesterComparisonItemDto
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public int TotalSubmissions { get; set; }
    public int GradedSubmissions { get; set; }
}

public class AdminSemesterStatisticsDto
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int TotalExaminations { get; set; }
    public int TotalSubmissions { get; set; }
    public int GradedSubmissions { get; set; }
    public int UngradedSubmissions { get; set; }
    public decimal GradedPercentage { get; set; }
    public decimal? AverageScore { get; set; }
    public decimal? HighestScore { get; set; }
    public decimal? LowestScore { get; set; }
    public decimal PassRate { get; set; }
    public decimal FailRate { get; set; }
    public List<ScoreDistributionBucketDto> ScoreDistribution { get; set; } = [];
}

public class AdminLecturerPerformanceDto
{
    public Guid LecturerId { get; set; }
    public string LecturerCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int TotalDiaries { get; set; }
    public int TotalSubmissions { get; set; }
    public int GradedSubmissions { get; set; }
    public int UngradedSubmissions { get; set; }
    public decimal CompletionRate { get; set; }
    public decimal? AverageScore { get; set; }
}
