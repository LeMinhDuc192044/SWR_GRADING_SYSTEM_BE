// Application/Semesters/Queries/GetSemesterByCode/GetSemesterByCodeQueryHandler.cs
using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterByCode;

public class GetSemesterByCodeQueryHandler : IRequestHandler<GetSemesterByCodeQuery, SemesterDTO>
{
    private readonly IApplicationDbContext _context;

    public GetSemesterByCodeQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SemesterDTO> Handle(GetSemesterByCodeQuery request, CancellationToken cancellationToken)
    {
        var semester = await _context.Semesters
            .Where(s => s.SemesterCode == request.SemesterCode)
            .Select(s => new SemesterDTO
            {
                SemesterId = s.SemesterId,
                SemesterCode = s.SemesterCode,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Status = s.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (semester is null)
            throw new NotFoundException(nameof(Semester), request.SemesterCode);

        return semester;
    }
}