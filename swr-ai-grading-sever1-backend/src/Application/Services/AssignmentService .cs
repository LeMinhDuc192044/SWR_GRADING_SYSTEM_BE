
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Assignments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class AssignmentService : IAssignmentService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public AssignmentService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    // =========================================================
    // GET ASSIGNMENTS BY GRADING SESSION
    // =========================================================

    public async Task<Result<IReadOnlyList<AssignmentDTO>>> GetByGradingSessionAsync(
        Guid gradingSessionId,
        CancellationToken ct = default)
    {
        var gradingSessionExists = await _db.GradingSessions
            .AsNoTracking()
            .AnyAsync(g => g.GradingSessionId == gradingSessionId, ct);

        if (!gradingSessionExists)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Grading session not found.",
                "GRADING_SESSION_NOT_FOUND");
        }

        // Assigner, student, paper set, examination session, examination and
        // semester are all joined in a single query (no N+1).
        var assignments = await ProjectDetails(_db.Assignments
                .AsNoTracking()
                .Where(a => a.GradingSessionId == gradingSessionId))
            .OrderByDescending(a => a.AssignedDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<AssignmentDTO>>.Success(assignments);
    }

    // =========================================================
    // GET ASSIGNMENT BY ID
    // =========================================================

    public async Task<Result<AssignmentDTO>> GetByIdAsync(
        Guid gradingSessionId,
        Guid assignmentId,
        CancellationToken ct = default)
    {
        var assignment = await ProjectDetails(_db.Assignments
                .AsNoTracking()
                .Where(a => a.AssignmentId == assignmentId
                            && a.GradingSessionId == gradingSessionId))
            .FirstOrDefaultAsync(ct);

        return assignment is null
            ? Result<AssignmentDTO>.Failure(
                "Assignment not found.",
                "ASSIGNMENT_NOT_FOUND")
            : Result<AssignmentDTO>.Success(assignment);
    }

    // =========================================================
    // CREATE ASSIGNMENTS
    // Supports:
    // 1. Automatic assignment using NumberSubmissions.
    // 2. Manual assignment using SubmissionIds.
    // 3. Reassignment of unlocked assignments.
    // =========================================================

    public async Task<Result<IReadOnlyList<AssignmentDTO>>> CreateAsync(
        Guid gradingSessionId,
        CreateAssignmentRequest request,
        CancellationToken ct = default)
    {
        var gradingSession = await _db.GradingSessions
            .AsNoTracking()
            .Include(g => g.ExaminationSession)
            .FirstOrDefaultAsync(
                g => g.GradingSessionId == gradingSessionId,
                ct);

        if (gradingSession is null)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Grading session not found.",
                "GRADING_SESSION_NOT_FOUND");
        }

        if (gradingSession.ExaminationSession is null)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "The grading session is not linked to an examination session.",
                "EXAMINATION_SESSION_NOT_FOUND");
        }

        var monitorIds = request.Monitors ?? [];

        if (monitorIds.Count == 0)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "At least one monitor is required.",
                "INVALID_MONITORS");
        }

        if (monitorIds.Any(id => id == Guid.Empty))
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Every monitor id must be valid.",
                "INVALID_MONITORS");
        }

        if (monitorIds.Distinct().Count() != monitorIds.Count)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Each monitor can only appear once.",
                "INVALID_MONITORS");
        }

        if (request.SubmissionPerMonitor <= 0)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "The number of submissions per monitor must be greater than zero.",
                "INVALID_SUBMISSIONS_PER_MONITOR");
        }

        // Every monitor must be an active monitor who is not busy in another grading session.
        foreach (var monitorId in monitorIds)
        {
            var staffCheck = await ValidateMonitorAsync(monitorId, ct);

            if (!staffCheck.IsSuccess)
            {
                return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    staffCheck.Error!,
                    staffCheck.ErrorCode);
            }

            var availability = await EnsureMonitorAvailableAsync(
                monitorId,
                gradingSessionId,
                ct);

            if (!availability.IsSuccess)
            {
                return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    availability.Error!,
                    availability.ErrorCode);
            }
        }

        var examinationSessionId = gradingSession.ExaminationSessionId;

        // Submissions that already have a monitor in this grading session are ignored.
        var assignedIds = await _db.Assignments
            .AsNoTracking()
            .Where(a => a.GradingSessionId == gradingSessionId)
            .Select(a => a.SubmissionId)
            .ToListAsync(ct);

        var required = monitorIds.Count * request.SubmissionPerMonitor;

        var candidateIds = await _db.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.ExaminationSessionId == examinationSessionId
                        && !assignedIds.Contains(s.SubmissionId))
            .OrderBy(s => s.CreatedDate)
            .Select(s => s.SubmissionId)
            .Take(required)
            .ToListAsync(ct);

        // Each monitor gets exactly the requested number of submissions, so workloads
        // stay balanced; if the session cannot supply that, reject instead of unbalancing it.
        if (candidateIds.Count < required)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                $"Not enough unassigned submissions in this grading session: " +
                $"{monitorIds.Count} monitor(s) x {request.SubmissionPerMonitor} = {required} required, " +
                $"{candidateIds.Count} available.",
                "INSUFFICIENT_SUBMISSIONS");
        }

        var now = VietnamTime.Now;
        var created = new List<Assignment>();

        for (var index = 0; index < monitorIds.Count; index++)
        {
            var monitorId = monitorIds[index];

            var submissionsForMonitor = candidateIds
                .Skip(index * request.SubmissionPerMonitor)
                .Take(request.SubmissionPerMonitor);

            foreach (var submissionId in submissionsForMonitor)
            {
                created.Add(new Assignment
                {
                    GradingSessionId = gradingSessionId,
                    SubmissionId = submissionId,
                    StaffId = monitorId,
                    Status = AssignmentStatus.IN_PROGRESS,
                    AssignedDate = now,
                    UpdatedDate = now
                });
            }
        }

        _db.Assignments.AddRange(created);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<IReadOnlyList<AssignmentDTO>>.Success(
            created.Select(Map).ToList());
    }

    // =========================================================
    // UPDATE ASSIGNMENT
    // =========================================================

    public async Task<Result<AssignmentDTO>> UpdateAsync(
        Guid gradingSessionId,
        Guid assignmentId,
        UpdateAssignmentRequest request,
        CancellationToken ct = default)
    {
        var assignment = await FindTrackedAsync(
            gradingSessionId,
            assignmentId,
            ct);

        if (assignment is null)
        {
            return Result<AssignmentDTO>.Failure(
                "Assignment not found.",
                "ASSIGNMENT_NOT_FOUND");
        }

        // Reassign to another monitor.
        if (request.StaffId.HasValue &&
            request.StaffId.Value != assignment.StaffId)
        {
            if (IsLocked(assignment))
            {
                return Result<AssignmentDTO>.Failure(
                    "A completed or graded assignment cannot be reassigned.",
                    "ASSIGNMENT_LOCKED");
            }

            var newStaffId = request.StaffId.Value;

            var staffCheck = await ValidateMonitorAsync(
                newStaffId, ct);

            if (!staffCheck.IsSuccess)
            {
                return Result<AssignmentDTO>.Failure(
                    staffCheck.Error!,
                    staffCheck.ErrorCode);
            }

            // Check whether the new monitor is already busy.
            var availability = await EnsureMonitorAvailableAsync(
                newStaffId,
                gradingSessionId,
                ct);

            if (!availability.IsSuccess)
            {
                return Result<AssignmentDTO>.Failure(
                    availability.Error!,
                    availability.ErrorCode);
            }

            var semesterId = assignment.GradingSession.ExaminationSession.SemesterId;
            var studentId = assignment.Submission.StudentId;

            // A monitor cannot grade their own linked student.
            var isOwnStudent = await _db.StudentSemesters
                .AsNoTracking()
                .AnyAsync(ss =>
                    ss.StudentId == studentId &&
                    ss.SemesterId == semesterId &&
                    ss.StaffId == newStaffId,
                    ct);

            if (isOwnStudent)
            {
                return Result<AssignmentDTO>.Failure(
                    "This monitor is linked to the student and cannot grade this submission.",
                    "MONITOR_LINKED_TO_STUDENT");
            }

            // A monitor cannot be assigned the same submission
            // in another grading session.
            var wasPreviouslyAssigned = await _db.Assignments
                .AsNoTracking()
                .AnyAsync(a =>
                    a.SubmissionId == assignment.SubmissionId &&
                    a.GradingSessionId != gradingSessionId &&
                    a.StaffId == newStaffId,
                    ct);

            if (wasPreviouslyAssigned)
            {
                return Result<AssignmentDTO>.Failure(
                    "This monitor was already assigned to this submission in another grading session.",
                    "MONITOR_DUPLICATE_SUBMISSION");
            }

            assignment.StaffId = newStaffId;
        }

        // Update assignment status.
        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(request.Status.Value))
            {
                return Result<AssignmentDTO>.Failure(
                    "Invalid assignment status.",
                    "INVALID_ASSIGNMENT_STATUS");
            }

            assignment.Status = request.Status.Value;
        }

        assignment.UpdatedDate = VietnamTime.Now;

        await _unitOfWork.SaveChangesAsync(ct);

        return Result<AssignmentDTO>.Success(Map(assignment));
    }

    // =========================================================
    // DELETE ASSIGNMENT
    // =========================================================

    public async Task<Result> DeleteAsync(
        Guid gradingSessionId,
        Guid assignmentId,
        CancellationToken ct = default)
    {
        var assignment = await FindTrackedAsync(
            gradingSessionId,
            assignmentId,
            ct);

        if (assignment is null)
        {
            return Result.Failure(
                "Assignment not found.",
                "ASSIGNMENT_NOT_FOUND");
        }

        if (IsLocked(assignment))
        {
            return Result.Failure(
                "A completed or graded assignment cannot be deleted.",
                "ASSIGNMENT_LOCKED");
        }

        _db.Assignments.Remove(assignment);

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // =========================================================
    // FIND TRACKED ASSIGNMENT
    // =========================================================

    private async Task<Assignment?> FindTrackedAsync(
        Guid gradingSessionId,
        Guid assignmentId,
        CancellationToken ct)
    {
        return await _db.Assignments
            .Include(a => a.Submission)
            .Include(a => a.GradingSession)
                .ThenInclude(g => g.ExaminationSession)
            .FirstOrDefaultAsync(
                a => a.AssignmentId == assignmentId &&
                     a.GradingSessionId == gradingSessionId,
                ct);
    }

    // =========================================================
    // VALIDATE EXPLICIT SUBMISSION SELECTION
    // =========================================================
    // VALIDATE MONITOR
    // =========================================================

    private async Task<Result> ValidateMonitorAsync(
        Guid staffId,
        CancellationToken ct)
    {
        var staff = await _db.Staff
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == staffId &&
                     !s.IsDeleted &&
                     s.IsActive,
                ct);

        if (staff is null)
        {
            return Result.Failure(
                "Staff not found or inactive.",
                "STAFF_NOT_FOUND");
        }

        if (staff.Role != UserRole.Monitor)
        {
            return Result.Failure(
                "Only monitors can be assigned to grade submissions.",
                "INVALID_STAFF_ROLE");
        }

        return Result.Success();
    }

    // =========================================================
    // CHECK MONITOR AVAILABILITY
    // =========================================================

    private async Task<Result> EnsureMonitorAvailableAsync(
        Guid staffId,
        Guid currentGradingSessionId,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(VietnamTime.Now);

        var busy = await _db.Assignments
            .AsNoTracking()
            .Where(a =>
                a.StaffId == staffId &&
                a.GradingSessionId != currentGradingSessionId &&
                (a.GradingSession.Deadline == null ||
                 a.GradingSession.Deadline >= today))
            .OrderBy(a => a.GradingSession.Deadline)
            .Select(a => new
            {
                a.GradingSession.Title,
                a.GradingSession.Deadline
            })
            .FirstOrDefaultAsync(ct);

        if (busy is null)
        {
            return Result.Success();
        }

        var deadlineText = busy.Deadline is { } deadline
            ? $" (deadline {deadline:yyyy-MM-dd})"
            : " (no deadline)";

        return Result.Failure(
            $"This monitor is already assigned to grading session '{busy.Title}'{deadlineText}. " +
            "A monitor can be assigned to another grading session only after that deadline has passed.",
            "MONITOR_BUSY_IN_ANOTHER_GRADING_SESSION");
    }

    // =========================================================
    // CHECK ASSIGNMENT LOCK
    // =========================================================

    private static bool IsLocked(Assignment assignment)
    {
        return assignment.Status is
            AssignmentStatus.COMPLETED or
            AssignmentStatus.GRADED;
    }

    // =========================================================
    // PROJECTION WITH RELATED DETAILS
    // Includes the assigner, student, paper set, examination session,
    // examination and semester of an assignment.
    // =========================================================

    private static IQueryable<AssignmentDTO> ProjectDetails(IQueryable<Assignment> query)
    {
        return query.Select(a => new AssignmentDTO
        {
            AssignmentId = a.AssignmentId,
            GradingSessionId = a.GradingSessionId,
            SubmissionId = a.SubmissionId,
            StaffId = a.StaffId,
            Status = a.Status,
            AssignedDate = a.AssignedDate,
            UpdatedDate = a.UpdatedDate,
            Staff = new AssignmentStaffDTO
            {
                StaffId = a.Staff.Id,
                StaffCode = a.Staff.StaffCode,
                FullName = a.Staff.FullName,
                Email = a.Staff.Email
            },
            Student = new AssignmentStudentDTO
            {
                StudentId = a.Submission.StudentId,
                StudentCode = a.Submission.Student.StudentCode,
                FullName = a.Submission.Student.FullName
            },
            PaperSet = a.GradingSession.ExaminationSession.PaperSet == null
                ? null
                : new AssignmentPaperSetDTO
                {
                    PaperSetId = a.GradingSession.ExaminationSession.PaperSet.PaperSetId,
                    PaperSetCode = a.GradingSession.ExaminationSession.PaperSet.PaperSetCode,
                    TotalQuestions = a.GradingSession.ExaminationSession.PaperSet.TotalQuestions
                },
            ExaminationSession = new AssignmentExaminationSessionDTO
            {
                ExaminationSessionId = a.GradingSession.ExaminationSessionId,
                ExaminationCode = a.GradingSession.ExaminationSession.ExaminationCode,
                ExaminationName = a.GradingSession.ExaminationSession.Examination.Name,
                SemesterName = a.GradingSession.ExaminationSession.Examination.Semester.Name,
                SemesterCode = a.GradingSession.ExaminationSession.Examination.Semester.SemesterCode
            }
        });
    }

    // =========================================================
    // MAP ENTITY TO DTO
    // =========================================================

    private static AssignmentDTO Map(Assignment assignment)
    {
        return new AssignmentDTO
        {
            AssignmentId = assignment.AssignmentId,
            GradingSessionId = assignment.GradingSessionId,
            SubmissionId = assignment.SubmissionId,
            StaffId = assignment.StaffId,
            Status = assignment.Status,
            AssignedDate = assignment.AssignedDate,
            UpdatedDate = assignment.UpdatedDate
        };
    }
}
