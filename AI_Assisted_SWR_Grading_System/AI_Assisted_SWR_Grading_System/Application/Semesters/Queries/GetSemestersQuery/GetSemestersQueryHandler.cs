using AI_Assisted_SWR_Grading_System.Application.Common.Interfaces;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesters;

public class GetSemestersQueryHandler : IRequestHandler<GetSemestersQuery, List<SemesterDTO>>
{
    private readonly IApplicationDbContext _context;

    public GetSemestersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SemesterDTO>> Handle(GetSemestersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Semesters.AsQueryable();

        if (request.Status.HasValue)
            query = query.Where(s => s.Status == request.Status.Value);

        return await query
            .OrderByDescending(s => s.StartDate)
            .Select(s => new SemesterDTO
            {
                SemesterId = s.SemesterId,
                SemesterCode = s.SemesterCode,
                Name = s.Name,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                Status = s.Status
            })
            .ToListAsync(cancellationToken);
    }
}