using Application.DTOs.PaperSets;

namespace Application.DTOs.Rubrics;

public class RubricDTO
{
    public Guid RubricId { get; set; }
    public Guid PaperSetId { get; set; }
    public decimal Point { get; set; }
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<RubricSubItemDto> SubItems { get; set; } = [];
    public Guid? DiagramId { get; set; }
    public string? DiagramName { get; set; }
    public IReadOnlyList<RubricQuestionSummaryDTO> Questions { get; set; } = [];
}

public class RubricQuestionSummaryDTO
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Point { get; set; }
}
