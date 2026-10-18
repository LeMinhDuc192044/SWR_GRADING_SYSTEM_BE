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

    public DbSet<PaperSet> PaperSets => Set<PaperSet>();
    public DbSet<Examination> Examinations => Set<Examination>();
    public DbSet<StudentSubmission> StudentSubmissions => Set<StudentSubmission>();
    public DbSet<StudentSubmission> Submissions => StudentSubmissions;
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<StudentExamination> StudentExaminations => Set<StudentExamination>();
    public DbSet<GradingDiary> GradingDiaries => Set<GradingDiary>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<DiagramElement> DiagramElements => Set<DiagramElement>();
    public DbSet<SubmissionParsedAnswer> SubmissionParsedAnswers => Set<SubmissionParsedAnswer>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<StudentSubmission>()
            .HasMany(submission => submission.ParsedAnswers)
            .WithOne(answer => answer.Submission)
            .HasForeignKey(answer => answer.SubmissionId)
            .IsRequired();

        builder.Entity<SubmissionParsedAnswer>()
            .HasOne(answer => answer.Element)
            .WithOne(element => element.ParsedAnswer)
            .HasForeignKey<SubmissionParsedAnswer>(answer => answer.ElementId);

        base.OnModelCreating(builder);
    }
}
