namespace Application.DTOs.Examinations;

public sealed class AssignStudentsToExaminationSessionRequest
{
    public List<string> StudentCodes { get; set; } = [];
}

public sealed class ExaminationSessionStudentAssignmentResultDTO
{
    public Guid ExaminationSessionId { get; set; }
    public int AddedCount { get; set; }
    public int SkippedCount { get; set; }
    public IReadOnlyList<Guid> AddedStudentIds { get; set; } = [];
}
