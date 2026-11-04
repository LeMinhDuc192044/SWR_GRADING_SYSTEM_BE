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

    public DbSet<PaperSet> PaperSets => Set<PaperSet>();
    public DbSet<Examination> Examinations => Set<Examination>();
    public DbSet<StudentSubmission> StudentSubmissions => Set<StudentSubmission>();
    public DbSet<StudentSubmission> Submissions => StudentSubmissions;
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<StudentSemester> StudentSemesters => Set<StudentSemester>();
    public DbSet<Diagram> Diagrams => Set<Diagram>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<DiagramElement> DiagramElements => Set<DiagramElement>();
    public DbSet<SubmissionParsedAnswer> SubmissionParsedAnswers => Set<SubmissionParsedAnswer>();
    public DbSet<SubmissionGradingHistory> SubmissionGradingHistories => Set<SubmissionGradingHistory>();
    public DbSet<SubmissionItemDetail> SubmissionItemDetails => Set<SubmissionItemDetail>();
    public DbSet<GradingSession> GradingSessions => Set<GradingSession>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AiModel> AiModels => Set<AiModel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<StudentSemester>()
            .HasKey(studentSemester => new { studentSemester.StudentId, studentSemester.SemesterId });

        builder.Entity<StudentSemester>()
            .HasOne(studentSemester => studentSemester.Student)
            .WithMany(student => student.StudentSemesters)
            .HasForeignKey(studentSemester => studentSemester.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentSemester>()
            .HasOne(studentSemester => studentSemester.Semester)
            .WithMany(semester => semester.StudentSemesters)
            .HasForeignKey(studentSemester => studentSemester.SemesterId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentSubmission>()
            .HasMany(submission => submission.ParsedAnswers)
            .WithOne(answer => answer.Submission)
            .HasForeignKey(answer => answer.SubmissionId)
            .IsRequired();

        builder.Entity<SubmissionParsedAnswer>()
            .HasOne(answer => answer.Element)
            .WithOne(element => element.ParsedAnswer)
            .HasForeignKey<SubmissionParsedAnswer>(answer => answer.ElementId);

        builder.Entity<SubmissionGradingHistory>()
            .HasOne(e => e.Submission)
            .WithMany(s => s.Evaluations)
            .HasForeignKey(e => e.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SubmissionGradingHistory>()
            .HasIndex(e => new { e.SubmissionId, e.EvaluationType })
            .IsUnique();

        builder.Entity<SubmissionItemDetail>()
            .HasOne(d => d.Evaluation)
            .WithMany(e => e.Details)
            .HasForeignKey(d => d.EvaluationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SubmissionItemDetail>()
            .HasOne(d => d.ParsedAnswer)
            .WithMany(pa => pa.EvaluationDetails)
            .HasForeignKey(d => d.ParsedAnswerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Assignment>()
            .HasOne(assignment => assignment.Staff)
            .WithMany(staff => staff.Assignments)
            .HasForeignKey(assignment => assignment.StaffId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Assignment>()
            .HasOne(assignment => assignment.GradingSession)
            .WithMany(session => session.Assignments)
            .HasForeignKey(assignment => assignment.GradingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Assignment>()
            .HasOne(assignment => assignment.Submission)
            .WithMany(submission => submission.Assignments)
            .HasForeignKey(assignment => assignment.SubmissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentSubmission>()
            .HasOne(ss => ss.UploadedBy)
            .WithMany(s => s.UploadedSubmissions)
            .HasForeignKey(ss => ss.UploadedById)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        base.OnModelCreating(builder);
    }
}
