namespace Application.DTOs.Reports;

/// <summary>
/// DTO chứa stream và thông tin file xuất báo cáo bảng điểm Excel
/// </summary>
public sealed class ExportReportFileDto
{
    public byte[] FileBytes { get; set; } = [];
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
