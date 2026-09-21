using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<Lecturer> Lecturers { get; }
    DbSet<PaperSet> PaperSets { get; }
    DbSet<Examination> Examinations { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<StudentExamination> StudentExaminations { get; }
    DbSet<GradingDiary> GradingDiaries { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
