using Application.Common;
using Application.DTOs.Examinations;

namespace Application.Interfaces;

public interface IExaminationSessionService
{
    Task<Result<IReadOnlyList<ExaminationSessionDTO>>> GetByExaminationAsync(Guid examinationId, CancellationToken ct = default);
    Task<Result<ExaminationSessionDTO>> GetByIdAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default);
    Task<Result<ExaminationSessionDTO>> CreateAsync(Guid examinationId, CreateExaminationSessionRequest request, CancellationToken ct = default);
    Task<Result<ExaminationSessionDTO>> UpdateAsync(Guid examinationId, Guid sessionId, UpdateExaminationSessionRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default);
}
