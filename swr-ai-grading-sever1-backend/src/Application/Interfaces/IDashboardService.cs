using Application.Common;
using Application.DTOs.Dashboard;

namespace Application.Interfaces;

public interface IDashboardService
{
    Task<Result<LecturerDashboardDto>> GetLecturerDashboardAsync(Guid lecturerId, Guid? semesterId = null, CancellationToken ct = default);
    Task<Result<IReadOnlyList<AdminSemesterStatisticsDto>>> GetAdminSemestersStatisticsAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<AdminLecturerPerformanceDto>>> GetAdminLecturersPerformanceAsync(Guid? semesterId = null, CancellationToken ct = default);
}
