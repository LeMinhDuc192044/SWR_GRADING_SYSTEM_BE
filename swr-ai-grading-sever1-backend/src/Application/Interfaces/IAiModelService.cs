using Application.Common;
using Application.DTOs.AiModels;

namespace Application.Interfaces;

public interface IAiModelService
{
    Task<Result<PagedResult<AiModelDTO>>> GetAllAsync(PagedRequest request, CancellationToken ct = default);
    Task<Result<AiModelDTO>> GetByIdAsync(Guid modelId, CancellationToken ct = default);
    Task<Result<AiModelDTO>> CreateAsync(CreateAiModelRequest request, CancellationToken ct = default);
    Task<Result<AiModelDTO>> UpdateAsync(Guid modelId, UpdateAiModelRequest request, CancellationToken ct = default);
    Task<Result<AiModelDTO>> UpdateStatusAsync(Guid modelId, UpdateAiModelStatusRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid modelId, CancellationToken ct = default);

    /// <summary>
    /// Seeds the default selectable models once, so a fresh database has something to grade with.
    /// Runs only when the table is still empty.
    /// </summary>
    Task EnsureDefaultModelsAsync(string? defaultApiKey, CancellationToken ct = default);
}
