using Application.Common;
using Application.DTOs.Monitors;

namespace Application.Interfaces;

public interface IMonitorService
{
    Task<Result<MonitorImportResultDto>> ImportMonitorsFromExcelAsync(Stream fileStream, CancellationToken ct = default);
    byte[] GenerateMonitorImportTemplate();

    /// <summary>
    /// Lấy danh sách tài khoản cán bộ chấm thi (Monitor) có phân trang và bộ lọc tìm kiếm cho Manager / Admin.
    /// </summary>
    Task<Result<PagedResult<MonitorResponseDto>>> GetPagedMonitorsAsync(MonitorFilterRequest request, CancellationToken ct = default);
}
