using Application.Common;

namespace Application.DTOs.Reports;

/// <summary>
/// Thông tin ngữ cảnh lọc của báo cáo (Kỳ thi, Học kỳ) giúp Frontend hiển thị tiêu đề và breadcrumb rõ ràng.
/// </summary>
public sealed class ReportFilterContextDto
{
    public Guid? SemesterId { get; set; }
    public string? SemesterName { get; set; }
    public string? SemesterCode { get; set; }
    public Guid? ExaminationId { get; set; }
    public string? ExaminationName { get; set; }
    public string? ExaminationCode { get; set; }
}

/// <summary>
/// DTO Báo cáo phổ điểm so sánh giữa AI và Human trên các bài đã chốt điểm OFFICIAL.
/// </summary>
public sealed class ScoreDistributionComparisonDto
{
    public ReportFilterContextDto FilterContext { get; set; } = new();
    public int TotalOfficialSubmissions { get; set; }
    public int TotalAiGradedSubmissions { get; set; }
    public int TotalHumanOfficialSubmissions { get; set; }
    public IReadOnlyList<ScoreDistributionBucketComparisonDto> Buckets { get; set; } = [];
}

/// <summary>
/// Thống kê phân bố điểm theo từng khoảng điểm song song cho cả AI và Human.
/// </summary>
public sealed class ScoreDistributionBucketComparisonDto
{
    public string Label { get; set; } = string.Empty; // "0 - 2đ", "2 - 4đ", ...
    public decimal MinScore { get; set; }
    public decimal MaxScore { get; set; }
    public int AiCount { get; set; }
    public double AiPercentage { get; set; }
    public int HumanCount { get; set; }
    public double HumanPercentage { get; set; }
}

/// <summary>
/// DTO Báo cáo thống kê chi tiết & phân tích đối chiếu điểm số giữa AI và Human (Status: OFFICIAL).
/// </summary>
public sealed class AiHumanScoreStatisticsDto
{
    public ReportFilterContextDto FilterContext { get; set; } = new();
    public int TotalOfficialSubmissions { get; set; }
    public int BothGradedCount { get; set; }

    // Thống kê điểm Giảng viên (Human Official)
    public decimal? HumanAverageScore { get; set; }
    public decimal? HumanHighestScore { get; set; }
    public decimal? HumanLowestScore { get; set; }

    // Thống kê điểm AI chấm
    public decimal? AiAverageScore { get; set; }
    public decimal? AiHighestScore { get; set; }
    public decimal? AiLowestScore { get; set; }

    // Đối chiếu & Sai số giữa AI và Human
    public decimal? AverageScoreDifference { get; set; } // Avg(AI - Human): > 0 là AI chấm thoáng hơn, < 0 là AI chấm chặt hơn
    public decimal? MeanAbsoluteError { get; set; }       // MAE = Avg(|AI - Human|)
    public int AiHigherCount { get; set; }
    public int HumanHigherCount { get; set; }
    public int EqualCount { get; set; }
    public double AcceptableDeviationRate { get; set; }   // Tỷ lệ % số bài có độ lệch <= 0.5 điểm
}

/// <summary>
/// DTO Báo cáo hiệu suất cán bộ chấm thi (Monitor) bao gồm chỉ số KPI tổng thể và danh sách phân trang.
/// </summary>
public sealed class MonitorPerformanceReportDto
{
    public ReportFilterContextDto FilterContext { get; set; } = new();
    
    // KPI Cards tổng thể
    public int TotalMonitors { get; set; }
    public int TotalAssignments { get; set; }
    public int TotalCompletedAssignments { get; set; }
    public double OverallCompletionRate { get; set; }

    // Bảng dữ liệu Monitor có hỗ trợ phân trang chuẩn PagedResult
    public PagedResult<MonitorPerformanceItemDto> PagedMonitors { get; set; } = new();
}

/// <summary>
/// Chi tiết hiệu suất của từng cán bộ chấm thi (Monitor).
/// </summary>
public sealed class MonitorPerformanceItemDto
{
    public Guid MonitorId { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int AssignedCount { get; set; }
    public int CompletedCount { get; set; }
    public int InProgressCount { get; set; }
    public int OverdueCount { get; set; }
    public double CompletionRate { get; set; }
    public decimal? AverageGivenScore { get; set; }
}
