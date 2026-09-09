using AI_Assisted_SWR_Grading_System.Application.Semesters;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;

public record CreateSemesterCommand : IRequest<SemesterDTO>
{
    public string SemesterCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public SemesterStatus Status { get; init; } = SemesterStatus.Upcoming;
}