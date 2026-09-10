using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext db) : base(db) { }

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _set.AsNoTracking().FirstOrDefaultAsync(user => user.Email == email, ct);

    public Task<Lecturer?> GetLecturerByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Lecturers.FirstOrDefaultAsync(lecturer => lecturer.Id == id, ct);

    public async Task<User?> GetByStudentCodeAsync(string studentCode, CancellationToken ct = default)
        => await _db.Students.AsNoTracking()
            .FirstOrDefaultAsync(student => student.StundentCode == studentCode, ct);

    public async Task<User?> GetByLecturerCodeAsync(string lecturerCode, CancellationToken ct = default)
        => await _db.Lecturers.AsNoTracking()
            .FirstOrDefaultAsync(lecturer => lecturer.LecturerCode == lecturerCode, ct);

    public Task<bool> IsEmailExistsAsync(string email, CancellationToken ct = default)
        => _set.AnyAsync(user => user.Email == email, ct);

    public Task<bool> IsStudentCodeExistsAsync(string studentCode, CancellationToken ct = default)
        => _db.Students.AnyAsync(student => student.StundentCode == studentCode, ct);

    public Task<bool> IsLecturerCodeExistsAsync(string lecturerCode, CancellationToken ct = default)
        => _db.Lecturers.AnyAsync(lecturer => lecturer.LecturerCode == lecturerCode, ct);
}