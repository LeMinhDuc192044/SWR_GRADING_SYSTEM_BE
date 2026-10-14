namespace Application.DTOs.PaperSets;

public sealed class RubricSubItemDto
{
    public string SubId { get; set; } = string.Empty;
    public decimal Point { get; set; }
    public string? Guideline { get; set; }
}

public sealed class ParsedRubricDto
{
    public string QuestionNumber { get; set; } = string.Empty;
    public decimal TotalPoint { get; set; }
    public List<RubricSubItemDto> SubItems { get; set; } = [];
}

public sealed class QuestionPreviewDto
{
    public int QuestionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Point { get; set; }
    public string? DetectedDiagramType { get; set; }
    public Guid? DiagramId { get; set; }
    public ParsedRubricDto? Rubric { get; set; }
    public bool IsScoreMatched { get; set; } = true;
    public string? ScoreDifferenceMessage { get; set; }
}

public sealed class PaperSetPreviewResultDTO
{
    public decimal TotalQuestionsScore { get; set; }
    public decimal TotalRubricsScore { get; set; }
    public bool IsValid { get; set; }
    public List<string> Warnings { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public List<QuestionPreviewDto> Questions { get; set; } = [];
}
