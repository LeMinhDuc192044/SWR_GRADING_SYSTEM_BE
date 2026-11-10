using Application.DTOs.Assignments;
using Domain.Enums;

namespace Application.DTOs.GradingSession;

public sealed class GradingSessionDTO
{
    public Guid GradingSessionId { get; set; }
    public Guid ExaminationSessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public GradingSessionType Type { get; set; }
    public DateOnly CreatedDate { get; set; }
    public DateOnly UpdatedDate { get; set; }
    public DateOnly? Deadline { get; set; }
    public int NumberSubmissions { get; set; }
}

public sealed class CreateGradingSessionRequest
{
    public string Title { get; set; } = string.Empty;
    public GradingSessionType Type { get; set; } = GradingSessionType.Normal_Grading;
    public DateOnly? Deadline { get; set; }
}

public sealed class UpdateGradingSessionRequest
{
    public string? Title { get; set; }
    public GradingSessionType? Type { get; set; }
    public DateOnly? Deadline { get; set; }
    public int? NumberSubmissions { get; set; }
}

public sealed class MonitorGradingSessionDTO
{
    public Guid GradingSessionId { get; set; }
    public Guid ExaminationSessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public GradingSessionType Type { get; set; }
    public DateOnly CreatedDate { get; set; }
    public DateOnly UpdatedDate { get; set; }
    public DateOnly? Deadline { get; set; }
    public int NumberSubmissions { get; set; }
    public int AssignedCount { get; set; }                       // assignments given to this monitor in this session
    public IReadOnlyList<AssignmentDTO> Assignments { get; set; } = [];
}