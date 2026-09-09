using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemesterStatus;

public record UpdateSemesterStatusCommand : IRequest
{
    public Guid SemesterId { get; init; }
    public SemesterStatus Status { get; init; }
}