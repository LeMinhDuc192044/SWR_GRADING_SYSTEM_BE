using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterById;

public record GetSemesterByIdQuery(Guid SemesterId) : IRequest<SemesterDTO>;