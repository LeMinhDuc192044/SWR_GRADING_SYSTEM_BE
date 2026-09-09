using MediatR;
using Microsoft.EntityFrameworkCore;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Domain.Common;
using AI_Assisted_SWR_Grading_System.Domain.Entities;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;

public class CreateSemesterCommandHandler : IRequestHandler<CreateSemesterCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateSemesterCommand request, CancellationToken cancellationToken)
    {
        var semesterCode = SemesterCodeGenerator.Generate(request.StartDate);

        var codeExists = await _context.Semesters
            .AnyAsync(s => s.SemesterCode == semesterCode, cancellationToken);

        if (codeExists)
            throw new ConflictException($"A semester for '{semesterCode}' already exists.");

        var semester = new Semester
        {
            SemesterCode = semesterCode,
            Name = request.Name,
            Code = request.Code,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status
        };

        _context.Semesters.Add(semester);
        await _context.SaveChangesAsync(cancellationToken);

        return semester.SemesterId;
    }
}