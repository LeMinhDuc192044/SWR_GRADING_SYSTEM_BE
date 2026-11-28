namespace Application.DTOs.Submissions;

public sealed class SubmissionParsedAnswerDTO
{
    public Guid ParsedAnswerId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? ElementId { get; set; }
    public string? ElementCode { get; set; }
    public string? ElementName { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractedImageUrl { get; set; }
    public string? ExtractedXml { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssignParsedAnswerElementRequest
{
    /// <summary>Diagram element this answer belongs to; null removes the link.</summary>
    public Guid? ElementId { get; set; }
}

public sealed class SaveSubmissionParsedAnswerRequest
{
    public Guid SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? ElementId { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractedImageUrl { get; set; }
    public string? ExtractedXml { get; set; }
}