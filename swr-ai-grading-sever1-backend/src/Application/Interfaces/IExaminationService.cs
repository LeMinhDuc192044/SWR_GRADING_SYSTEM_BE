using Application.Common;
using Application.DTOs.Examinations;

namespace Application.Interfaces;

public interface IExaminationService
{
    Task<PagedResult<ExaminationDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<ExaminationDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<ExaminationDTO>> CreateAsync(CreateExaminationRequest request, CancellationToken ct = default);
    Task<Result<ExaminationDTO>> UpdateAsync(Guid id, UpdateExaminationRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}