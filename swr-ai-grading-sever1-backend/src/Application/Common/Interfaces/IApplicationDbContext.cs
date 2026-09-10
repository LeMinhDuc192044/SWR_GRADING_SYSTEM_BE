using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<Lecturer> Lecturers { get; }
    DbSet<ExamMaterial> ExamMaterials { get; }
    DbSet<Examination> Examinations { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<Grading> Gradings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
