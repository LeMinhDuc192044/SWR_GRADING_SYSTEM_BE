
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

        var assignments = await _db.Assignments
            .AsNoTracking()
            .Where(a => a.GradingSessionId == gradingSessionId)
            .OrderByDescending(a => a.AssignedDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<AssignmentDTO>>.Success(
            assignments.Select(Map).ToList());
    }

    // =========================================================
    // GET ASSIGNMENT BY ID
    // =========================================================

    public async Task<Result<AssignmentDTO>> GetByIdAsync(
        Guid gradingSessionId,
        Guid assignmentId,
        CancellationToken ct = default)
    {
        var assignment = await _db.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.AssignmentId == assignmentId
                     && a.GradingSessionId == gradingSessionId,
                ct);

        return assignment is null
            ? Result<AssignmentDTO>.Failure(
                "Assignment not found.",
                "ASSIGNMENT_NOT_FOUND")
            : Result<AssignmentDTO>.Success(Map(assignment));
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

        // Determine whether the request uses manual selection.
        var hasExplicitSelection = request.SubmissionIds is { Count: > 0 };

        var requestedIds = request.SubmissionIds?
            .Distinct()
            .ToList() ?? [];

        if (hasExplicitSelection &&
            (requestedIds.Any(id => id == Guid.Empty)
             || requestedIds.Count != request.SubmissionIds!.Count))
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Submission IDs must be valid and cannot contain duplicates.",
                "INVALID_SUBMISSION_IDS");
        }

        if (!hasExplicitSelection && request.NumberSubmissions <= 0)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "Number of submissions must be greater than zero.",
                "INVALID_NUMBER_SUBMISSIONS");
        }

        // Validate monitor.
        var staffCheck = await ValidateMonitorAsync(
            request.StaffId, ct);

        if (!staffCheck.IsSuccess)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                staffCheck.Error!,
                staffCheck.ErrorCode);
        }

        // Check whether the monitor is available.
        var availability = await EnsureMonitorAvailableAsync(
            request.StaffId,
            gradingSessionId,
            ct);

        if (!availability.IsSuccess)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                availability.Error!,
                availability.ErrorCode);
        }

        var examinationSessionId = gradingSession.ExaminationSessionId;
        var semesterId = gradingSession.ExaminationSession.SemesterId;

        // Get the total number of submissions in this examination session.
        var totalSubmissions = await _db.StudentSubmissions
            .AsNoTracking()
            .CountAsync(
                s => s.ExaminationSessionId == examinationSessionId,
                ct);

        // Automatic assignment cannot request more than the session contains.
        if (!hasExplicitSelection &&
            request.NumberSubmissions > totalSubmissions)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                $"Cannot assign {request.NumberSubmissions} submission(s): " +
                $"the examination session only has {totalSubmissions}.",
                "NUMBER_SUBMISSIONS_EXCEEDS_TOTAL");
        }

        // Manual selection: validate every selected submission.
        if (hasExplicitSelection)
        {
            var validation = await ValidateExplicitSelectionAsync(
                requestedIds,
                examinationSessionId,
                gradingSessionId,
                semesterId,
                request.StaffId,
                ct);

            if (!validation.IsSuccess)
            {
                return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    validation.Error!,
                    validation.ErrorCode);
            }
        }

        // Load the assignments belonging to this grading session.
        // Tracking is required because some assignments may be reassigned.
        var existingQuery = _db.Assignments
            .Where(a => a.GradingSessionId == gradingSessionId);

        if (hasExplicitSelection)
        {
            existingQuery = existingQuery.Where(
                a => requestedIds.Contains(a.SubmissionId));
        }

        var existing = await existingQuery.ToListAsync(ct);

        var rowsBySubmission = existing
            .GroupBy(a => a.SubmissionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(a => a.AssignedDate).ToList());

        List<Guid> candidateIds;

        if (hasExplicitSelection)
        {
            // Use exactly the submissions selected by the requester.
            candidateIds = requestedIds;
        }
        else
        {
            // Exclude students linked to this monitor in this semester.
            var ownStudents = _db.StudentSemesters
                .Where(ss =>
                    ss.SemesterId == semesterId &&
                    ss.StaffId == request.StaffId)
                .Select(ss => ss.StudentId);

            // A monitor cannot grade the same submission again
            // if they were assigned to it in another grading session.
            var previouslyAssignedToMonitor = _db.Assignments
                .Where(a =>
                    a.StaffId == request.StaffId &&
                    a.GradingSessionId != gradingSessionId)
                .Select(a => a.SubmissionId);

            candidateIds = await _db.StudentSubmissions
                .AsNoTracking()
                .Where(s =>
                    s.ExaminationSessionId == examinationSessionId &&
                    !ownStudents.Contains(s.StudentId) &&
                    !previouslyAssignedToMonitor.Contains(s.SubmissionId))
                .OrderBy(s => s.CreatedDate)
                .Select(s => s.SubmissionId)
                .ToListAsync(ct);
        }

        // Do not reassign completed or graded submissions.
        var lockedSubmission = candidateIds.FirstOrDefault(id =>
            rowsBySubmission.TryGetValue(id, out var rows) &&
            rows.Any(IsLocked));

        if (lockedSubmission != Guid.Empty)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "One or more selected submissions already have completed or graded assignments.",
                "ASSIGNMENT_LOCKED");
        }

        // Do not create duplicate assignments for the same monitor.
        var alreadyAssignedToThisMonitor = candidateIds.Any(id =>
            rowsBySubmission.TryGetValue(id, out var rows) &&
            rows[0].StaffId == request.StaffId);

        if (alreadyAssignedToThisMonitor)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "One or more submissions are already assigned to this monitor in this grading session.",
                "SUBMISSION_ALREADY_ASSIGNED");
        }

        // Automatic assignment uses the requested count.
        // Manual selection uses the number of selected submissions.
        var numberToAssign = hasExplicitSelection
            ? candidateIds.Count
            : request.NumberSubmissions;

        // 1. Create new assignments for submissions never assigned
        // in this grading session.
        var toCreate = candidateIds
            .Where(id => !rowsBySubmission.ContainsKey(id))
            .Take(numberToAssign)
            .ToList();

        // 2. Reassign unlocked submissions from another monitor.
        // This fills the remaining requested number.
        var remaining = numberToAssign - toCreate.Count;

        var toReassign = candidateIds
            .Where(id =>
                rowsBySubmission.TryGetValue(id, out var rows) &&
                rows.All(r => !IsLocked(r)) &&
                rows[0].StaffId != request.StaffId)
            .Take(remaining)
            .Select(id => rowsBySubmission[id][0])
            .ToList();

        if (toCreate.Count + toReassign.Count == 0)
        {
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "No eligible submissions available for this monitor.",
                "NO_ELIGIBLE_SUBMISSIONS");
        }

        var now = VietnamTime.Now;

        // Create new assignment records.
        var created = toCreate.Select(submissionId => new Assignment
        {
            GradingSessionId = gradingSessionId,
            SubmissionId = submissionId,
            StaffId = request.StaffId,
            Status = AssignmentStatus.IN_PROGRESS,
            AssignedDate = now,
            UpdatedDate = now
        }).ToList();

        // Update existing assignment records.
        foreach (var assignment in toReassign)
        {
            assignment.StaffId = request.StaffId;
            assignment.Status = AssignmentStatus.IN_PROGRESS;
            assignment.AssignedDate = now;
            assignment.UpdatedDate = now;
        }

        _db.Assignments.AddRange(created);

        // Save all changes together.
        await _unitOfWork.SaveChangesAsync(ct);

        var result = created.Select(Map).ToList();

        result.AddRange(toReassign.Select(assignment =>
        {
            var dto = Map(assignment);
            dto.IsReassigned = true;
            return dto;
        }));

        return Result<IReadOnlyList<AssignmentDTO>>.Success(result);
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

    private async Task<Result> ValidateExplicitSelectionAsync(
        IReadOnlyList<Guid> submissionIds,
        Guid examinationSessionId,
        Guid gradingSessionId,
        Guid semesterId,
        Guid staffId,
        CancellationToken ct)
    {
        var submissions = await _db.StudentSubmissions
            .AsNoTracking()
            .Where(s => submissionIds.Contains(s.SubmissionId))
            .Select(s => new
            {
                s.SubmissionId,
                s.StudentId,
                s.ExaminationSessionId
            })
            .ToListAsync(ct);

        if (submissions.Count != submissionIds.Count)
        {
            return Result.Failure(
                "One or more requested submissions were not found.",
                "SUBMISSION_NOT_FOUND");
        }

        // All submissions must belong to this examination session.
        if (submissions.Any(s =>
                s.ExaminationSessionId != examinationSessionId))
        {
            return Result.Failure(
                "One or more submissions do not belong to this examination session.",
                "SUBMISSION_SESSION_MISMATCH");
        }

        // Check whether the monitor is linked to any selected student.
        var studentIds = submissions
            .Select(s => s.StudentId)
            .Distinct()
            .ToList();

        var hasOwnStudent = await _db.StudentSemesters
            .AsNoTracking()
            .AnyAsync(ss =>
                ss.SemesterId == semesterId &&
                ss.StaffId == staffId &&
                studentIds.Contains(ss.StudentId),
                ct);

        if (hasOwnStudent)
        {
            return Result.Failure(
                "This monitor is linked to one or more students and cannot grade their submissions.",
                "MONITOR_LINKED_TO_STUDENT");
        }

        // The same monitor cannot grade the same submission
        // in another grading session.
        var gradedInOtherSession = await _db.Assignments
            .AsNoTracking()
            .AnyAsync(a =>
                a.StaffId == staffId &&
                a.GradingSessionId != gradingSessionId &&
                submissionIds.Contains(a.SubmissionId),
                ct);

        if (gradedInOtherSession)
        {
            return Result.Failure(
                "This monitor was already assigned to one or more of these submissions in another grading session.",
                "MONITOR_DUPLICATE_SUBMISSION");
        }

        return Result.Success();
    }

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
