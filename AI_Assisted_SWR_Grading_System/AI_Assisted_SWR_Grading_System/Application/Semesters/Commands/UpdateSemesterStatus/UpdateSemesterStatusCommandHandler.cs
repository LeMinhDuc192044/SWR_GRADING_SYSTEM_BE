using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemesterStatus;

public class UpdateSemesterStatusCommandHandler : IRequestHandler<UpdateSemesterStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateSemesterStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateSemesterStatusCommand request, CancellationToken cancellationToken)
    {
        var semester = await _context.Semesters
            .FirstOrDefaultAsync(s => s.SemesterId == request.SemesterId, cancellationToken);

        if (semester is null)
            throw new NotFoundException(nameof(Semester), request.SemesterId);

        if (semester.Status == SemesterStatus.Ended && request.Status == SemesterStatus.Upcoming)
            throw new ConflictException("An ended semester cannot be moved back to Upcoming.");

        semester.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);
    }
}