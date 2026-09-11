using Application.Common;
using Application.DTOs.Semesters;

namespace Application.Interfaces;

public interface ISemesterService
{
    Task<PagedResult<SemesterDTO>> GetPagedAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<SemesterDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Result<SemesterDTO>> CreateAsync(CreateSemesterRequest request, CancellationToken ct = default);
    Task<Result<SemesterDTO>> UpdateAsync(Guid id, UpdateSemesterRequest request, CancellationToken ct = default);
    Task<Result<SemesterDTO>> UpdateStatusAsync(Guid id, UpdateSemesterStatusRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}