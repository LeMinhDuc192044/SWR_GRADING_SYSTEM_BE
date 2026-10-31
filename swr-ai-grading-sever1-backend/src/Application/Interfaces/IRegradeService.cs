using Application.Common;
using Application.DTOs.Regrades;

namespace Application.Interfaces;

public interface IRegradeService
{
    Task<Result<RegradeApplicationDto>> CreateApplicationAsync(
        CreateRegradeApplicationRequest request,
        Guid studentUserId,
        CancellationToken ct = default);

    Task<Result<RegradeApplicationDto>> ReviewApplicationAsync(
        Guid applicationId,
        ReviewRegradeApplicationRequest request,
        Guid adminUserId,
        CancellationToken ct = default);

    Task<Result<RegradeApplicationDto>> GradeApplicationAsync(
        Guid applicationId,
        GradeRegradeApplicationRequest request,
        Guid lecturerUserId,
        CancellationToken ct = default);

    Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForAdminAsync(
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default);

    Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForStudentAsync(
        Guid studentUserId,
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default);

    Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForLecturerAsync(
        Guid lecturerUserId,
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default);

    Task<Result<RegradeApplicationDto>> GetApplicationByIdAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        CancellationToken ct = default);
}

