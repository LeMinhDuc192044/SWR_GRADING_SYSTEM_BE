using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesters;

public record GetSemestersQuery : IRequest<List<SemesterDTO>>
{
    public SemesterStatus? Status { get; init; }
}