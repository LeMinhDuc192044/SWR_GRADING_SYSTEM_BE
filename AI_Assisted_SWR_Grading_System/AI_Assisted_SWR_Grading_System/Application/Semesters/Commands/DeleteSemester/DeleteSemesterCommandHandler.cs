using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.DeleteSemester;

public class DeleteSemesterCommandHandler : IRequestHandler<DeleteSemesterCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteSemesterCommand request, CancellationToken cancellationToken)
    {
        var semester = await _context.Semesters
            .Include(s => s.Examinations)
            .FirstOrDefaultAsync(s => s.SemesterId == request.SemesterId, cancellationToken);

        if (semester is null)
            throw new NotFoundException(nameof(Semester), request.SemesterId);

        if (semester.Examinations.Any())
            throw new ConflictException("Cannot delete a semester that has examinations linked to it.");

        _context.Semesters.Remove(semester);
        await _context.SaveChangesAsync(cancellationToken);
    }
}