using Application.Common;
using Application.DTOs.Applications;
using Domain.Enums;

namespace Application.Interfaces;

public interface IApplicationService
{
    Task<Result<ApplicationDTO>> CreateAsync(CreateApplicationRequest request, Guid currentUserId, CancellationToken ct = default);
    Task<Result<PagedResult<ApplicationDTO>>> GetMyApplicationsAsync(PagedRequest? request, Guid currentUserId, CancellationToken ct = default);
    Task<Result<PagedResult<ApplicationDTO>>> GetAllAsync(PagedRequest? request, Guid? examinationSessionId = null, ApplicationStatus? status = null, CancellationToken ct = default);
    Task<Result<ApplicationDTO>> GetByIdAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default);
    Task<Result<ApplicationDTO>> UpdateStatusAsync(Guid id, UpdateApplicationStatusRequest request, Guid currentUserId, CancellationToken ct = default);
}
