using static System.Runtime.InteropServices.JavaScript.JSType;
namespace Application.DTOs.Examinations;

public sealed class GradingSessionDTO
{
    public Guid GradingSessionId { get; set; }
    public Guid ExaminationSessionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateOnly CreatedDate { get; set; }
    public DateOnly UpdatedDate { get; set; }
    public DateOnly? Deadline { get; set; }
    public int NumberSubmissions { get; set; }
}

public sealed class CreateGradingSessionRequest
{
    public string Title { get; set; } = string.Empty;
    public DateOnly? Deadline { get; set; }
}

public sealed class UpdateGradingSessionRequest
{
    public string? Title { get; set; }
    public DateOnly? Deadline { get; set; }
    public int? NumberSubmissions { get; set; }
}
