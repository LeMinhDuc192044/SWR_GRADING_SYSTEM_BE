using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Domain.Common;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemester;

public class UpdateSemesterCommandHandler : IRequestHandler<UpdateSemesterCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(UpdateSemesterCommand request, CancellationToken cancellationToken)
    {
        var semester = await _context.Semesters
            .FirstOrDefaultAsync(s => s.SemesterId == request.SemesterId, cancellationToken);

        if (semester is null)
            throw new NotFoundException(nameof(Semester), request.SemesterId);

        var newSemesterCode = SemesterCodeGenerator.Generate(request.StartDate);

        var codeTaken = await _context.Semesters
            .AnyAsync(s => s.SemesterCode == newSemesterCode
                        && s.SemesterId != request.SemesterId, cancellationToken);

        if (codeTaken)
            throw new ConflictException($"A semester for '{newSemesterCode}' already exists.");

        semester.SemesterCode = newSemesterCode;
        semester.Name = request.Name;
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;
        semester.Status = request.Status;

        await _context.SaveChangesAsync(cancellationToken);
    }
}