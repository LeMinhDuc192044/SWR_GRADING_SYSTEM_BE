namespace Application.DTOs.Submissions;

public sealed class SubmissionParsedAnswerDTO
{
    public Guid ParsedAnswerId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? ElementId { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractedImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SaveSubmissionParsedAnswerRequest
{
    public Guid SubmissionId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? ElementId { get; set; }
    public string? ExtractedText { get; set; }
    public string? ExtractedImageUrl { get; set; }
}