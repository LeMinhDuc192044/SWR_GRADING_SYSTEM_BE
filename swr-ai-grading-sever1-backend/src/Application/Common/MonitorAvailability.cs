using System.Linq.Expressions;
using Domain.Entities;
using Domain.Enums;

namespace Application.Common;

public static class MonitorAvailability
{
    public static Expression<Func<Assignment, bool>> IsBusyAssignment(DateOnly today) =>
        a => a.Status == AssignmentStatus.IN_PROGRESS &&
             (a.GradingSession == null || a.GradingSession.Deadline == null || a.GradingSession.Deadline >= today);
}
