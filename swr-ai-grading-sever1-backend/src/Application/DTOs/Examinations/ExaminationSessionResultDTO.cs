using Domain.Enums;
using System.Text.Json.Serialization;

namespace Application.DTOs.Examinations;

/// <summary>
/// Examination results of a single examination session, one entry per submitted student.
/// </summary>
public sealed class ExaminationSessionResultsDTO
{
    public Guid ExaminationSessionId { get; set; }
    public string ExaminationCode { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public ExaminationSessionStatus Status { get; set; }
    public ExaminationSessionSemesterDTO Semester { get; set; } = new();
    public ExaminationSessionExaminationDTO Examination { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionPaperSetDTO? PaperSet { get; set; }

    public IReadOnlyList<ExaminationSessionStudentResultDTO> Results { get; set; } = [];
}

public sealed class ExaminationSessionPaperSetDTO
{
    public Guid PaperSetId { get; set; }
    public string PaperSetCode { get; set; } = string.Empty;
}

public sealed class ExaminationSessionStudentResultDTO
{
    public Guid SubmissionId { get; set; }
    public ExaminationSessionResultStudentDTO Student { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ExaminationSessionResultGradingDTO? Grading { get; set; }
}

public sealed class ExaminationSessionResultStudentDTO
{
    public Guid StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

/// <summary>
/// The lecturer's grading history of one submission (EvaluationType.Lecturer).
/// </summary>
public sealed class ExaminationSessionResultGradingDTO
{
    public Guid HistoryId { get; set; }
    public EvaluationType EvaluationType { get; set; } = EvaluationType.Lecturer;
    public SubmissionGradingHistoryStatus Status { get; set; }
    public int GradingRound { get; set; }
    public decimal? LecturerScore { get; set; }
    public string? GeneralFeedback { get; set; }
    public DateTime GradedAt { get; set; }
    public IReadOnlyList<ExaminationSessionQuestionScoreDTO> Questions { get; set; } = [];
}

/// <summary>
/// One SubmissionItemDetail of the lecturer's grading, with the question it belongs to.
/// </summary>
public sealed class ExaminationSessionQuestionScoreDTO
{
    public Guid DetailId { get; set; }
    public Guid EvaluationId { get; set; }
    public Guid ParsedAnswerId { get; set; }
    public Guid QuestionId { get; set; }
    public string QuestionTitle { get; set; } = string.Empty;
    public decimal MaxScore { get; set; }
    public decimal Score { get; set; }
    public string? Comment { get; set; }
}
