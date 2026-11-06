using Application.DTOs.PaperSets;

namespace Application.DTOs.Questions;

public class QuestionDTO
{
    public Guid QuestionId { get; set; }
    public Guid PaperSetId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal Point { get; set; }
    public Guid? DiagramId { get; set; }
    public string? DiagramName { get; set; }
    public IReadOnlyList<QuestionCriteriaDTO> Criteria { get; set; } = [];
    public DateTime CreatedDay { get; set; }
    public DateTime UpdatedDay { get; set; }
}

public sealed class QuestionCriteriaDTO
{
    public string Criteria { get; set; } = string.Empty;
    public decimal Point { get; set; }
}

public class QuestionRubricSummaryDTO
{
    public decimal Point { get; set; }
    public string Content { get; set; } = string.Empty;
    public IReadOnlyList<RubricSubItemDto> SubItems { get; set; } = [];
}
