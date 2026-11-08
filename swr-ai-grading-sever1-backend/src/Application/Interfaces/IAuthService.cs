using Application.Common;
using Application.DTOs.AuthDTOs;

namespace Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthResponseDto>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<AuthResponseDto>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<IReadOnlyList<MonitorDTO>>> GetAllAsync(CancellationToken ct = default);
}
