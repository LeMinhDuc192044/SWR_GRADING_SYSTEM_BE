using AI_Assisted_SWR_Grading_System.Domain.Enums;


namespace AI_Assisted_SWR_Grading_System.Domain.Entities;


public class Grading
{
    public Guid Id { get; set; }

    public decimal? AiScore { get; set; }
    public decimal? LecturerScore { get; set; }
    public string? AiLogs { get; set; }
    public DateTime CreateDate { get; set; }
    public DateTime UpdateDate { get; set; }
    public string? Comment { get; set; }
    public decimal? FinalScore { get; set; }
    public GradingStatus Status { get; set; }

    public Guid SubmissionId { get; set; }
    public Submission Submission { get; set; } = null!;
}