using Application.Common.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.UnitTests.Helpers;

public class TestDbContext : DbContext, IApplicationDbContext
{
    public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

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
    public DbSet<Diagram> Diagrams => Set<Diagram>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionRubric> QuestionRubrics => Set<QuestionRubric>();
    public DbSet<DiagramElement> DiagramElements => Set<DiagramElement>();
    public DbSet<SubmissionParsedAnswer> SubmissionParsedAnswers => Set<SubmissionParsedAnswer>();
    public DbSet<SubmissionEvaluation> SubmissionEvaluations => Set<SubmissionEvaluation>();
    public DbSet<EvaluationItemDetail> EvaluationItemDetails => Set<EvaluationItemDetail>();
}

public static class TestDbContextFactory
{
    public static TestDbContext Create(string dbName)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        var context = new TestDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}

