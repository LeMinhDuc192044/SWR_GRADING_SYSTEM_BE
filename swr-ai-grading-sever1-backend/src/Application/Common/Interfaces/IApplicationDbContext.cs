using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<PaperSet> PaperSets { get; }
    DbSet<Examination> Examinations { get; }
    DbSet<StudentSubmission> StudentSubmissions { get; }
    DbSet<StudentSubmission> Submissions { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<Diagram> Diagrams { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionRubric> QuestionRubrics { get; }
    DbSet<DiagramElement> DiagramElements { get; }
    DbSet<SubmissionParsedAnswer> SubmissionParsedAnswers { get; }
    DbSet<SubmissionGradingHistory> SubmissionGradingHistories { get; }
    DbSet<SubmissionItemDetail> SubmissionItemDetails { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
