using Domain.Enums;
using Microsoft.AspNetCore.Http;


namespace Application.DTOs.PaperSets;

public class PaperSetMetadataDTO
{
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int TotalQuestions { get; set; }
    public IReadOnlyList<PaperSetQuestionDTO> Questions { get; set; } = [];
    public IReadOnlyList<PaperSetFileDTO> Files { get; set; } = [];
    public PaperSetStatus Status { get; set; }
    public Guid? ExaminationId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid CreateById { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public sealed class PaperSetQuestionDTO
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Point { get; set; }
}

public sealed class PaperSetFileDTO
{
    public PaperSetFileType FileType { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long FileSize { get; set; }
}

public sealed class PaperSetDetailDTO : PaperSetMetadataDTO
{
    public string StoragePath { get; set; } = string.Empty;
}

public sealed class UpdatePaperSetRequest
{
    public string? Description { get; set; }
    public int? TotalQuestions { get; set; }
    public Guid? SemesterId { get; set; }
    public Guid? ExaminationId { get; set; }
    public PaperSetStatus? Status { get; set; }
}

public sealed class MaterialFileUpload
{
    public required PaperSetFileType FileType { get; init; }
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/octet-stream";
    public long Length { get; init; }
}

public class AddPaperSetFilesRequest
{
    public IFormFile? Question { get; set; }
    public IFormFile? AnswerRubric { get; set; }
    public IFormFile? AnswerTemplate { get; set; }
}

public class CreatePaperSetRequest
{
    public string Description { get; set; } = string.Empty;
    public Guid SemesterId { get; set; }
    // public int TotalQuestions { get; set; }
    public List<CreateQuestionRequest> Questions { get; set; } = new();
    public IFormFile? Question { get; set; }
    public IFormFile? AnswerRubric { get; set; }
    public IFormFile? AnswerTemplate { get; set; }
}

public sealed class CreatePaperSetsRequest
{
    public string Description { get; set; } = string.Empty;
    public Guid SemesterId { get; set; }
    public List<CreatePaperSetItemRequest> Materials { get; set; } = new();
}

public sealed class CreatePaperSetItemRequest
{
    public string Description { get; set; } = string.Empty;
    public List<CreateQuestionRequest> Questions { get; set; } = new();
    public IFormFile? Question { get; set; }
    public IFormFile? AnswerRubric { get; set; }
    public IFormFile? AnswerTemplate { get; set; }
}

public sealed class CreatePaperSetInput
{
    public required string Description { get; init; }
    public required IReadOnlyList<CreateQuestionInput> Questions { get; init; }
    public required IReadOnlyList<MaterialFileUpload> Files { get; init; }
}

public sealed class CreateQuestionRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Point { get; set; }
}

public sealed class CreateQuestionInput
{
    public required string Title { get; init; }
    public required string Content { get; init; }
    public decimal Point { get; init; }
}