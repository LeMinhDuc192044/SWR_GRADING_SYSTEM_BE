namespace Application.DTOs.Diagrams;

/// <summary>Result of connecting one student submission's parsed answers to diagram elements.</summary>
public sealed class ConnectSubmissionDiagramResultDTO
{
    public Guid SubmissionId { get; set; }

    /// <summary>Questions of the submission that have a diagram and were processed.</summary>
    public int ConnectedQuestionCount { get; set; }

    /// <summary>Parsed answers whose element link changed in this call.</summary>
    public int ConnectedAnswerCount { get; set; }

    /// <summary>The submission's questions that have a diagram, with that diagram.</summary>
    public IReadOnlyList<ConnectSubmissionQuestionDTO> Questions { get; set; } = [];

    /// <summary>
    /// Parsed answers of those questions, each with the diagram of its question and the element it
    /// now points to.
    /// </summary>
    public IReadOnlyList<ConnectSubmissionAnswerDTO> Answers { get; set; } = [];
}

public sealed class ConnectSubmissionQuestionDTO
{
    public Guid QuestionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public Guid? DiagramId { get; set; }
    public string? DiagramName { get; set; }
}

public sealed class ConnectSubmissionAnswerDTO
{
    public Guid ParsedAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? DiagramId { get; set; }
    public string? DiagramName { get; set; }
    public Guid? ElementId { get; set; }
    public string? ElementCode { get; set; }
    public string? ElementName { get; set; }
}
