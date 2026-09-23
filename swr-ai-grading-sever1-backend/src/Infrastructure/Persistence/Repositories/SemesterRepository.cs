using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SemesterRepository : Repository<Semester>, ISemesterRepository
{
	public SemesterRepository(AppDbContext db) : base(db) { }

	public Task<bool> IsCodeExistsAsync(string code, CancellationToken ct = default)
		=> AnyAsync(s => s.SemesterCode == code, ct);

	public Task<bool> IsDateRangeOverlappingAsync(DateOnly startDate, DateOnly endDate, Guid? excludedSemesterId = null, CancellationToken ct = default)
		=> AnyAsync(s => (!excludedSemesterId.HasValue || s.SemesterId != excludedSemesterId.Value)
			&& s.StartDate <= endDate
			&& s.EndDate >= startDate, ct);

	public Task<bool> HasExaminationsAsync(Guid semesterId, CancellationToken ct = default)
		=> _db.Examinations.AnyAsync(e => e.SemesterId == semesterId, ct);
}