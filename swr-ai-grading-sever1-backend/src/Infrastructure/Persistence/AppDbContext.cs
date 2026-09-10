using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Lecturer> Lecturers => Set<Lecturer>();

    public DbSet<ExamMaterial> ExamMaterials => Set<ExamMaterial>();
    public DbSet<Examination> Examinations => Set<Examination>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<Grading> Gradings => Set<Grading>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(builder);
    }
}