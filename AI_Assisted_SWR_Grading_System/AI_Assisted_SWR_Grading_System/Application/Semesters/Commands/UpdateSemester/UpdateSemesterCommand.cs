using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemester;

public record UpdateSemesterCommand : IRequest
{
    public Guid SemesterId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public SemesterStatus Status { get; init; }
}