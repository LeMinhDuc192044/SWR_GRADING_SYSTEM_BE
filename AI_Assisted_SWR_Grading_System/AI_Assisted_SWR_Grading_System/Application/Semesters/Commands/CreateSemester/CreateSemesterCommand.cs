using MediatR;

using AI_Assisted_SWR_Grading_System.Domain.Enums;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;
public record CreateSemesterCommand : IRequest<Guid>
{
    public string SemesterCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public SemesterStatus Status { get; init; } = SemesterStatus.Upcoming;
}