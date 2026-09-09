// Application/Semesters/Commands/CreateSemester/CreateSemesterCommandHandler.cs
using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Application.Semesters;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;

public class CreateSemesterCommandHandler : IRequestHandler<CreateSemesterCommand, SemesterDTO>
{
    private readonly IApplicationDbContext _context;

    public CreateSemesterCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SemesterDTO> Handle(CreateSemesterCommand request, CancellationToken cancellationToken)
    {
        // 1. Duplicate code check
        var codeExists = await _context.Semesters
            .AnyAsync(s => s.SemesterCode == request.SemesterCode, cancellationToken);

        if (codeExists)
            throw new ConflictException($"A semester with code '{request.SemesterCode}' already exists.");

        // 2. Overlap check — new range must not intersect any existing semester's range,
        //    and must not exactly match an existing range either (covered by the same condition).
        var overlaps = await _context.Semesters
            .AnyAsync(s =>
                    s.StartDate < request.EndDate &&
                    s.EndDate > request.StartDate,
                cancellationToken);

        if (overlaps)
            throw new ConflictException(
                "The given start/end date range overlaps with an existing semester.");

        var semester = new Semester
        {
            SemesterCode = request.SemesterCode,
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Status = request.Status
        };

        _context.Semesters.Add(semester);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505")
        {
            throw new ConflictException($"A semester with code '{request.SemesterCode}' already exists.");
        }

        return new SemesterDTO
        {
            SemesterId = semester.SemesterId,
            SemesterCode = semester.SemesterCode,
            Name = semester.Name,
            StartDate = semester.StartDate,
            EndDate = semester.EndDate,
            Status = semester.Status
        };
    }
}