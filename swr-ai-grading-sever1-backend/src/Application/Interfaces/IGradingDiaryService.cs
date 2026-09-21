using Application.Common;
using Application.DTOs.GradingDiaries;

namespace Application.Interfaces;

public interface IGradingDiaryService
{
    Task<Result<GradingDiaryResponseDTO>> CreateAsync(CreateGradingDiaryRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<PagedResult<GradingDiaryResponseDTO>> GetPagedAsync(PagedRequest request, Guid? lecturerId = null, CancellationToken ct = default);
    Task<Result<GradingDiaryDetailDTO>> GetByIdAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
    Task<Result<GradingDiaryResponseDTO>> UpdateAsync(Guid id, UpdateGradingDiaryRequest request, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
}
