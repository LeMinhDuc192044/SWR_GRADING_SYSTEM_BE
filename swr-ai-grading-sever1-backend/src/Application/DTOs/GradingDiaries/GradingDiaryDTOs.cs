using Domain.Enums;

namespace Application.DTOs.GradingDiaries;

public sealed class CreateGradingDiaryRequest
{
    public Guid PaperSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public sealed class UpdateGradingDiaryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Content { get; set; }
}

public sealed class GradingDiaryResponseDTO
{
    public Guid GradingDiaryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public Guid CreateById { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public int SubmissionsCount { get; set; }
}

public sealed class GradingDiaryDetailDTO
{
    public Guid GradingDiaryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
    public string? FileAnswerRubric { get; set; }
    public Guid CreateById { get; set; }
    public string LecturerName { get; set; } = string.Empty;
    public IReadOnlyList<SubmissionItemDTO> Submissions { get; set; } = Array.Empty<SubmissionItemDTO>();
}

public sealed class SubmissionItemDTO
{
    public Guid SubmissionId { get; set; }
    public string SubmissionName { get; set; } = string.Empty;
    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public SubmissionStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
