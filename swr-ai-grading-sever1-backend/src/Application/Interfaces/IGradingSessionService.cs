using Application.Common;
using Application.DTOs.GradingSession;

namespace Application.Interfaces;

public interface IGradingSessionService
{
    Task<Result<IReadOnlyList<GradingSessionDTO>>> GetByExaminationSessionAsync(Guid examinationSessionId,
                                                                                CancellationToken ct = default);
    Task<Result<GradingSessionDTO>> GetByIdAsync(Guid examinationSessionId,
                                                 Guid gradingSessionId, 
                                                 CancellationToken ct = default);
    Task<Result<GradingSessionDTO>> CreateAsync(Guid examinationSessionId,
                                                CreateGradingSessionRequest request, 
                                                CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid examinationSessionId,
                             Guid gradingSessionId,
                             CancellationToken ct = default);
    Task<Result<GradingSessionDTO>> UpdateAsync(
        Guid examinationSessionId,
        UpdateGradingSessionRequest request,
        CancellationToken ct = default);

    Task<PagedResult<GradingSessionDTO>> GetAllAsync(
        PagedRequest request,
        CancellationToken ct = default);

    Task<PagedResult<MonitorGradingSessionDTO>> GetByMonitorAsync(
        Guid monitorId,
        PagedRequest request,
        CancellationToken ct = default);
}
