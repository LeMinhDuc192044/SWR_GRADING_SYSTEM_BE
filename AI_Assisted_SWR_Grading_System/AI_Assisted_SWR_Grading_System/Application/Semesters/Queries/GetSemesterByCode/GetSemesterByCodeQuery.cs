using AI_Assisted_SWR_Grading_System.Application.Semesters;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterByCode;

public record GetSemesterByCodeQuery(string SemesterCode) : IRequest<SemesterDTO>;