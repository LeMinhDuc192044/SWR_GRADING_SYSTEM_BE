using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.AiModels;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class AiModelService : IAiModelService
{
    private const int MaxNameLength = 200;
    private const int MaxApiKeyLength = 500;

    private readonly IApplicationDbContext _db;
    private readonly IAiModelKeyValidator _keyValidator;

    public AiModelService(IApplicationDbContext db, IAiModelKeyValidator keyValidator)
    {
        _db = db;
        _keyValidator = keyValidator;
    }

    public async Task<Result<PagedResult<AiModelDTO>>> GetAllAsync(PagedRequest request, CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        var query = _db.AiModels.AsNoTracking();
        var totalCount = await query.CountAsync(ct);

        var models = await query
            .OrderByDescending(model => model.Status)
            .ThenBy(model => model.Name)
            .ThenBy(model => model.ModelId)   // tie-breaker: stable pages when names repeat
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Result<PagedResult<AiModelDTO>>.Success(new PagedResult<AiModelDTO>
        {
            Items = models.Select(ToDTO).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    public async Task<Result<AiModelDTO>> GetByIdAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _db.AiModels.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ModelId == modelId, ct);

        return model is null
            ? Result<AiModelDTO>.Failure("AI model not found.", "AI_MODEL_NOT_FOUND")
            : Result<AiModelDTO>.Success(ToDTO(model));
    }

    public async Task<Result<AiModelDTO>> CreateAsync(CreateAiModelRequest request, CancellationToken ct = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (ValidateName(name) is { } nameError)
            return nameError;

        // The same model name may be added more than once, but an API key belongs to one row only.
        var apiKey = request.ApiKey?.Trim() ?? string.Empty;
        if (apiKey.Length == 0)
            return Result<AiModelDTO>.Failure("API key is required.", "AI_MODEL_API_KEY_REQUIRED");

        if (apiKey.Length > MaxApiKeyLength)
            return Result<AiModelDTO>.Failure($"API key must not exceed {MaxApiKeyLength} characters.", "AI_MODEL_API_KEY_TOO_LONG");

        if (await FindModelByApiKeyAsync(apiKey, null, ct) is { } keyOwner)
            return Result<AiModelDTO>.Failure(
                $"This API key is already used by AI model '{keyOwner.Name}'.",
                "AI_MODEL_API_KEY_EXISTS");

        if (await _keyValidator.ValidateAsync(name, apiKey, ct) is { IsSuccess: false } keyCheck)
            return Result<AiModelDTO>.Failure(keyCheck.Error!, keyCheck.ErrorCode);

        var activate = request.Status ?? false;
        if (activate)
            await DeactivateOtherModelsAsync(null, ct);

        var model = new AiModel
        {
            ModelId = Guid.NewGuid(),
            Name = name,
            ApiKey = apiKey,
            Price = request.Price ?? 0m,
            Status = activate,
            IsSelected = activate
        };

        _db.AiModels.Add(model);
        await _db.SaveChangesAsync(ct);

        return Result<AiModelDTO>.Success(ToDTO(model));
    }

    public async Task<Result<AiModelDTO>> UpdateAsync(Guid modelId, UpdateAiModelRequest request, CancellationToken ct = default)
    {
        var model = await _db.AiModels.FirstOrDefaultAsync(item => item.ModelId == modelId, ct);
        if (model is null)
            return Result<AiModelDTO>.Failure("AI model not found.", "AI_MODEL_NOT_FOUND");

        var name = model.Name;
        if (request.Name is not null)
        {
            var requestedName = request.Name.Trim();
            if (ValidateName(requestedName) is { } nameError)
                return nameError;
            name = requestedName;
        }

        var apiKey = model.ApiKey;
        var keyChanged = !string.IsNullOrWhiteSpace(request.ApiKey);
        if (keyChanged)
        {
            apiKey = request.ApiKey!.Trim();
            if (apiKey.Length > MaxApiKeyLength)
                return Result<AiModelDTO>.Failure($"API key must not exceed {MaxApiKeyLength} characters.", "AI_MODEL_API_KEY_TOO_LONG");

            if (await FindModelByApiKeyAsync(apiKey, modelId, ct) is { } keyOwner)
                return Result<AiModelDTO>.Failure(
                    $"This API key is already used by AI model '{keyOwner.Name}'.",
                    "AI_MODEL_API_KEY_EXISTS");
        }

        // Re-check with Gemini whenever the model/key pair sent to the API changes.
        var nameChanged = !string.Equals(name, model.Name, StringComparison.Ordinal);
        if ((keyChanged || nameChanged) && !string.IsNullOrWhiteSpace(apiKey))
        {
            if (await _keyValidator.ValidateAsync(name, apiKey, ct) is { IsSuccess: false } keyCheck)
                return Result<AiModelDTO>.Failure(keyCheck.Error!, keyCheck.ErrorCode);
        }

        model.Name = name;
        model.ApiKey = apiKey;

        if (request.Price.HasValue)
            model.Price = request.Price.Value;

        await _db.SaveChangesAsync(ct);

        return Result<AiModelDTO>.Success(ToDTO(model));
    }

    public async Task<Result<AiModelDTO>> UpdateStatusAsync(Guid modelId, UpdateAiModelStatusRequest request, CancellationToken ct = default)
    {
        var model = await _db.AiModels.FirstOrDefaultAsync(item => item.ModelId == modelId, ct);
        if (model is null)
            return Result<AiModelDTO>.Failure("AI model not found.", "AI_MODEL_NOT_FOUND");

        if (request.Status)
        {
            // Only one model may be in use, so enabling this one disables every other model.
            await DeactivateOtherModelsAsync(modelId, ct);
            model.Status = true;
            model.IsSelected = true;
        }
        else
        {
            model.Status = false;
            model.IsSelected = false;
        }

        await _db.SaveChangesAsync(ct);

        return Result<AiModelDTO>.Success(ToDTO(model));
    }

    public async Task<Result> DeleteAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _db.AiModels.FirstOrDefaultAsync(item => item.ModelId == modelId, ct);
        if (model is null)
            return Result.Failure("AI model not found.", "AI_MODEL_NOT_FOUND");

        if (model.Status || model.IsSelected)
            return Result.Failure(
                "This model is in use. Enable another model before deleting it.",
                "AI_MODEL_IN_USE");

        _db.AiModels.Remove(model);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task EnsureDefaultModelsAsync(string? defaultApiKey, CancellationToken ct = default)
    {
        if (await _db.AiModels.AnyAsync(ct))
            return;

        var apiKey = defaultApiKey?.Trim() ?? string.Empty;

        for (var index = 0; index < SupportedAiModels.Defaults.Count; index++)
        {
            // A key belongs to one row, so only the enabled seed model gets the configured key;
            // the other defaults stay keyless until an admin adds their own key.
            var isActive = index == 0;
            _db.AiModels.Add(new AiModel
            {
                ModelId = Guid.NewGuid(),
                Name = SupportedAiModels.Defaults[index],
                ApiKey = isActive ? apiKey : string.Empty,
                Price = 0m,
                Status = isActive,
                IsSelected = isActive
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<AiModel?> FindModelByApiKeyAsync(string apiKey, Guid? excludeModelId, CancellationToken ct)
    {
        var query = _db.AiModels.AsNoTracking().Where(model => model.ApiKey == apiKey);
        if (excludeModelId.HasValue)
            query = query.Where(model => model.ModelId != excludeModelId.Value);

        return await query.FirstOrDefaultAsync(ct);
    }

    private static Result<AiModelDTO>? ValidateName(string name)
    {
        if (name.Length == 0)
            return Result<AiModelDTO>.Failure("Model name is required.", "AI_MODEL_NAME_REQUIRED");

        if (name.Length > MaxNameLength)
            return Result<AiModelDTO>.Failure(
                $"Model name must not exceed {MaxNameLength} characters.",
                "AI_MODEL_NAME_TOO_LONG");

        if (!SupportedAiModels.IsSupported(name))
            return Result<AiModelDTO>.Failure(
                $"AI model '{name}' is not supported. Available models: {SupportedAiModels.SupportedList}.",
                "AI_MODEL_NOT_SUPPORTED");

        return null;
    }

    private async Task DeactivateOtherModelsAsync(Guid? keepModelId, CancellationToken ct)
    {
        var others = await _db.AiModels
            .Where(item => item.ModelId != keepModelId && (item.Status || item.IsSelected))
            .ToListAsync(ct);

        foreach (var other in others)
        {
            other.Status = false;
            other.IsSelected = false;
        }
    }

    private static AiModelDTO ToDTO(AiModel model) => new()
    {
        ModelId = model.ModelId,
        Name = model.Name,
        ApiKeyMasked = MaskApiKey(model.ApiKey),
        HasApiKey = !string.IsNullOrWhiteSpace(model.ApiKey),
        Status = model.Status,
        IsSelected = model.IsSelected,
        Price = model.Price
    };

    private static string MaskApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return string.Empty;

        var trimmed = apiKey.Trim();
        return trimmed.Length <= 4 ? "****" : $"****{trimmed[^4..]}";
    }
}
