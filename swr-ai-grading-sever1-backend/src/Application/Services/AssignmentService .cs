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

    public AssignmentService(IApplicationDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<AssignmentDTO>>> GetByGradingSessionAsync(
        Guid gradingSessionId, CancellationToken ct = default)
    {
        if (!await _db.GradingSessions.AsNoTracking().AnyAsync(g => g.GradingSessionId == gradingSessionId, ct))
            return Result<IReadOnlyList<AssignmentDTO>>.Failure("Grading session not found.", "GRADING_SESSION_NOT_FOUND");

        var assignments = await _db.Assignments
            .AsNoTracking()
            .Where(a => a.GradingSessionId == gradingSessionId)
            .OrderByDescending(a => a.AssignedDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<AssignmentDTO>>.Success(assignments.Select(Map).ToList());
    }

    public async Task<Result<AssignmentDTO>> GetByIdAsync(
        Guid gradingSessionId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.GradingSessionId == gradingSessionId, ct);

        return assignment is null
            ? Result<AssignmentDTO>.Failure("Assignment not found.", "ASSIGNMENT_NOT_FOUND")
            : Result<AssignmentDTO>.Success(Map(assignment));
    }

    public async Task<Result<IReadOnlyList<AssignmentDTO>>> CreateAsync(
        Guid gradingSessionId, CreateAssignmentRequest request, CancellationToken ct = default)
    {
        var gradingSession = await _db.GradingSessions
            .AsNoTracking()
            .Include(g => g.ExaminationSession)
            .FirstOrDefaultAsync(g => g.GradingSessionId == gradingSessionId, ct);
        if (gradingSession is null)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure("Grading session not found.", "GRADING_SESSION_NOT_FOUND");

        if (request.NumberSubmissions <= 0)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure("Number of submissions must be greater than zero.", "INVALID_NUMBER_SUBMISSIONS");

        var staffCheck = await ValidateMonitorAsync(request.StaffId, ct);
        if (!staffCheck.IsSuccess)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(staffCheck.Error!, staffCheck.ErrorCode);

        var availability = await EnsureMonitorAvailableAsync(request.StaffId, gradingSessionId, ct);
        if (!availability.IsSuccess)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(availability.Error!, availability.ErrorCode);

        var examinationSessionId = gradingSession.ExaminationSessionId;
        var semesterId = gradingSession.ExaminationSession.SemesterId;

        // 1. Cannot request more than the examination session has. Nothing is created.
        var totalSubmissions = await _db.StudentSubmissions.AsNoTracking()
            .CountAsync(s => s.ExaminationSessionId == examinationSessionId, ct);
        if (request.NumberSubmissions > totalSubmissions)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                $"Cannot assign {request.NumberSubmissions} submission(s): the examination session only has {totalSubmissions}.",
                "NUMBER_SUBMISSIONS_EXCEEDS_TOTAL");

        // Students this monitor is linked to in this semester: their work must not be graded by this monitor.
        var ownStudents = _db.StudentSemesters
            .Where(ss => ss.SemesterId == semesterId && ss.StaffId == request.StaffId)
            .Select(ss => ss.StudentId);

        var submissionIds = await _db.StudentSubmissions.AsNoTracking()
            .Where(s => s.ExaminationSessionId == examinationSessionId && !ownStudents.Contains(s.StudentId))
            .OrderBy(s => s.CreatedDate)
            .Select(s => s.SubmissionId)
            .ToListAsync(ct);

        // Tracked on purpose: reassigned rows are updated in place.
        var existing = await _db.Assignments
            .Where(a => a.GradingSessionId == gradingSessionId)
            .ToListAsync(ct);
        var rowsBySubmission = existing
            .GroupBy(a => a.SubmissionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AssignedDate).ToList());

        // 2. Never assigned yet -> create.
        var toCreate = submissionIds
            .Where(id => !rowsBySubmission.ContainsKey(id))
            .Take(request.NumberSubmissions)
            .ToList();

        // 3. Assigned to another monitor and not completed/graded -> reassign (fills what is left of N).
        var toReassign = submissionIds
            .Where(id => rowsBySubmission.TryGetValue(id, out var rows)
                        && rows.All(r => !IsLocked(r))
                        && rows[0].StaffId != request.StaffId)
            .Take(request.NumberSubmissions - toCreate.Count)
            .Select(id => rowsBySubmission[id][0])
            .ToList();

        if (toCreate.Count + toReassign.Count == 0)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                "No eligible submissions available for this monitor.", "NO_ELIGIBLE_SUBMISSIONS");

        var now = VietnamTime.Now;

        var created = toCreate.Select(submissionId => new Assignment
        {
            GradingSessionId = gradingSessionId,
            SubmissionId = submissionId,
            StaffId = request.StaffId,
            Status = AssignmentStatus.IN_PROGRESS,
            AssignedDate = now,
            UpdatedDate = now
        }).ToList();

        foreach (var row in toReassign)
        {
            row.StaffId = request.StaffId;
            row.Status = AssignmentStatus.IN_PROGRESS;
            row.AssignedDate = now;
            row.UpdatedDate = now;
        }

        _db.Assignments.AddRange(created);
        await _unitOfWork.SaveChangesAsync(ct);   // one save: all creates and reassigns succeed or none do

        var result = created.Select(Map).ToList();
        result.AddRange(toReassign.Select(a =>
        {
            var dto = Map(a);
            dto.IsReassigned = true;
            return dto;
        }));
        return Result<IReadOnlyList<AssignmentDTO>>.Success(result);
    }

    public async Task<Result<AssignmentDTO>> UpdateAsync(
        Guid gradingSessionId, Guid assignmentId, UpdateAssignmentRequest request, CancellationToken ct = default)
    {
        var assignment = await FindTrackedAsync(gradingSessionId, assignmentId, ct);
        if (assignment is null)
            return Result<AssignmentDTO>.Failure("Assignment not found.", "ASSIGNMENT_NOT_FOUND");

        if (request.StaffId.HasValue && request.StaffId.Value != assignment.StaffId)
        {
            if (IsLocked(assignment))
                return Result<AssignmentDTO>.Failure("A completed or graded assignment cannot be reassigned.", "ASSIGNMENT_LOCKED");

            var staffCheck = await ValidateMonitorAsync(request.StaffId.Value, ct);
            if (!staffCheck.IsSuccess)
                return Result<AssignmentDTO>.Failure(staffCheck.Error!, staffCheck.ErrorCode);

            var semesterId = assignment.GradingSession.ExaminationSession.SemesterId;
            var studentId = assignment.Submission.StudentId;
            var isOwnStudent = await _db.StudentSemesters.AsNoTracking().AnyAsync(ss =>
                ss.StudentId == studentId && ss.SemesterId == semesterId && ss.StaffId == request.StaffId.Value, ct);
            if (isOwnStudent)
                return Result<AssignmentDTO>.Failure(
                    "This monitor is linked to the student and cannot grade this submission.", "MONITOR_LINKED_TO_STUDENT");

            assignment.StaffId = request.StaffId.Value;
        }

        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(request.Status.Value))
                return Result<AssignmentDTO>.Failure("Invalid assignment status.", "INVALID_ASSIGNMENT_STATUS");
            assignment.Status = request.Status.Value;
        }

        assignment.UpdatedDate = VietnamTime.Now;
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<AssignmentDTO>.Success(Map(assignment));
    }

    public async Task<Result> DeleteAsync(
        Guid gradingSessionId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await FindTrackedAsync(gradingSessionId, assignmentId, ct);
        if (assignment is null)
            return Result.Failure("Assignment not found.", "ASSIGNMENT_NOT_FOUND");
        if (IsLocked(assignment))
            return Result.Failure("A completed or graded assignment cannot be deleted.", "ASSIGNMENT_LOCKED");

        _db.Assignments.Remove(assignment);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Assignment?> FindTrackedAsync(Guid gradingSessionId, Guid assignmentId, CancellationToken ct)
        => await _db.Assignments
            .Include(a => a.Submission)
            .Include(a => a.GradingSession).ThenInclude(g => g.ExaminationSession)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId && a.GradingSessionId == gradingSessionId, ct);

    private async Task<Result> ValidateMonitorAsync(Guid staffId, CancellationToken ct)
    {
        var staff = await _db.Staff.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == staffId && !s.IsDeleted && s.IsActive, ct);
        if (staff is null)
            return Result.Failure("Staff not found.", "STAFF_NOT_FOUND");
        if (staff.Role != UserRole.Monitor)
            return Result.Failure("Only monitors can be assigned to grade submissions.", "INVALID_STAFF_ROLE");
        return Result.Success();
    }

    private async Task<Result> EnsureMonitorAvailableAsync(Guid staffId, Guid currentGradingSessionId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(VietnamTime.Now);

        var busy = await _db.Assignments.AsNoTracking()
            .Where(a => a.StaffId == staffId
                        && a.GradingSessionId != currentGradingSessionId
                        && (a.GradingSession.Deadline == null || a.GradingSession.Deadline >= today))
            .OrderBy(a => a.GradingSession.Deadline)
            .Select(a => new { a.GradingSession.Title, a.GradingSession.Deadline })
            .FirstOrDefaultAsync(ct);

        if (busy is null)
            return Result.Success();

        var deadlineText = busy.Deadline is { } d ? $" (deadline {d:yyyy-MM-dd})" : " (no deadline)";
        return Result.Failure(
            $"This monitor is already assigned to grading session '{busy.Title}'{deadlineText}. " +
            "A monitor can be assigned to another grading session only after that deadline has passed.",
            "MONITOR_BUSY_IN_ANOTHER_GRADING_SESSION");
    }

    private static bool IsLocked(Assignment assignment)
        => assignment.Status is AssignmentStatus.COMPLETED or AssignmentStatus.GRADED;

    private static AssignmentDTO Map(Assignment assignment) => new()
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