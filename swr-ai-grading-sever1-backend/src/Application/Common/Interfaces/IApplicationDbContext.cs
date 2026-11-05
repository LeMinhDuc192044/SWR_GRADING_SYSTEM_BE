using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Student> Students { get; }
    DbSet<Staff> Staff { get; }
    DbSet<PaperSet> PaperSets { get; }
    DbSet<Examination> Examinations { get; }
    DbSet<StudentSubmission> StudentSubmissions { get; }
    DbSet<StudentSubmission> Submissions { get; }
    DbSet<StudentExaminationSession> StudentExaminationSessions { get; }
    DbSet<ExaminationSession> ExaminationSessions { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<StudentSemester> StudentSemesters { get; }
    DbSet<Diagram> Diagrams { get; }
    DbSet<Question> Questions { get; }
    DbSet<QuestionCriteria> QuestionCriteria { get; }
    DbSet<DiagramElement> DiagramElements { get; }
    DbSet<SubmissionParsedAnswer> SubmissionParsedAnswers { get; }
    DbSet<SubmissionGradingHistory> SubmissionGradingHistories { get; }
    DbSet<SubmissionItemDetail> SubmissionItemDetails { get; }
    DbSet<GradingSession> GradingSessions { get; }
    DbSet<Assignment> Assignments { get; }
    DbSet<Domain.Entities.Application> Applications { get; }
    DbSet<AiModel> AiModels { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
