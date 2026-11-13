using Application.Common;
using Application.DTOs.Monitors;

namespace Application.Interfaces;

public interface IMonitorService
{
    Task<Result<MonitorImportResultDto>> ImportMonitorsFromExcelAsync(Stream fileStream, CancellationToken ct = default);
    byte[] GenerateMonitorImportTemplate();
}
