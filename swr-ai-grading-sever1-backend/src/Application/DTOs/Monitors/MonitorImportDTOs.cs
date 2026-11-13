namespace Application.DTOs.Monitors;

public sealed class MonitorImportResultDto
{
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<MonitorImportErrorDto> Errors { get; set; } = new();
    public List<MonitorImportSuccessDto> ImportedMonitors { get; set; } = new();
}

public sealed class MonitorImportSuccessDto
{
    public int RowIndex { get; set; }
    public string StaffCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateOnly Birthday { get; set; }
    public string Password { get; set; } = string.Empty;
}

public sealed class MonitorImportErrorDto
{
    public int RowIndex { get; set; }
    public string? StaffCode { get; set; }
    public string? Email { get; set; }
    public string Reason { get; set; } = string.Empty;
}
