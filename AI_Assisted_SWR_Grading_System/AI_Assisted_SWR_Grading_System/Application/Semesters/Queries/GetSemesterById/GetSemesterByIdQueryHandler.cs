using AI_Assisted_SWR_Grading_System.Application.Common.Exceptions;
using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterById;

public class GetSemesterByIdQueryHandler : IRequestHandler<GetSemesterByIdQuery, SemesterDTO>
{
    private readonly IApplicationDbContext _context;

    public GetSemesterByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SemesterDTO> Handle(GetSemesterByIdQuery request, CancellationToken cancellationToken)
    {
        var semester = await _context.Semesters
            .Where(s => s.SemesterId == request.SemesterId)
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
            throw new NotFoundException(nameof(Semester), request.SemesterId);

        return semester;
    }
}