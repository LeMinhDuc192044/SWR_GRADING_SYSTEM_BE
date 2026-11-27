using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Common;

public static class MonitorAvailability
{
    // A monitor stays busy while the grading session is still open, unless every submission
    // they were given there already has an official grade (nothing left for them to do).
    // The official grade must have been made after the assignment, so an official result from
    // an earlier round does not release a monitor who is now grading the same submission again.
    public static Expression<Func<Assignment, bool>> IsBusyAssignment(DateOnly today) =>
        a => a.Status == AssignmentStatus.IN_PROGRESS &&
             (a.GradingSession == null || a.GradingSession.Deadline == null || a.GradingSession.Deadline >= today) &&
             !a.Submission.SubmissionGradingHistories.Any(h =>
                 h.Status == SubmissionGradingHistoryStatus.OFFICIAL &&
                 h.CreatedAt >= a.AssignedDate);
}
