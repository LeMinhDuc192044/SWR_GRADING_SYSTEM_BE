namespace Application.DTOs.AiModels;

/// <summary>
/// AI model used by the grading pipeline. The API key is never returned in full.
/// </summary>
public sealed class AiModelDTO
{
    public Guid ModelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKeyMasked { get; set; } = string.Empty;
    public bool HasApiKey { get; set; }
    public bool Status { get; set; }
    public bool IsSelected { get; set; }
    public decimal Price { get; set; }
}

public sealed class CreateAiModelRequest
{
    public string? Name { get; set; }

    /// <summary>Required, and must not already belong to another AI model row.</summary>
    public string? ApiKey { get; set; }

    public decimal? Price { get; set; }

    /// <summary>When true the new model becomes the one used for grading.</summary>
    public bool? Status { get; set; }
}

public sealed class UpdateAiModelRequest
{
    public string? Name { get; set; }

    /// <summary>Omit or leave empty to keep the stored key; a new key must be unused.</summary>
    public string? ApiKey { get; set; }

    public decimal? Price { get; set; }
}

public sealed class UpdateAiModelStatusRequest
{
    public bool Status { get; set; }
}
