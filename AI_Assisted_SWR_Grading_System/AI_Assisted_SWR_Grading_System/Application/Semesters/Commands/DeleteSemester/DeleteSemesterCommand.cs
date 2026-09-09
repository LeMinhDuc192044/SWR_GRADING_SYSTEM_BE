using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.DeleteSemester;

public record DeleteSemesterCommand(Guid SemesterId) : IRequest;    