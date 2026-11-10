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

        var requestedIds = request.SubmissionIds?
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        var hasExplicitSelection = requestedIds is { Count: > 0 };

        if (!hasExplicitSelection && request.NumberSubmissions <= 0)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure("Number of submissions must be greater than zero.", "INVALID_NUMBER_SUBMISSIONS");

        var staffCheck = await ValidateMonitorAsync(request.StaffId, ct);
        if (!staffCheck.IsSuccess)
            return Result<IReadOnlyList<AssignmentDTO>>.Failure(staffCheck.Error!, staffCheck.ErrorCode);

        var examinationSessionId = gradingSession.ExaminationSessionId;
        var semesterId = gradingSession.ExaminationSession.SemesterId;

        List<StudentSubmission> eligible;
        if (hasExplicitSelection)
        {
            var validationResult = await ValidateExplicitSelectionAsync(
                requestedIds!,
                examinationSessionId,
                gradingSessionId,
                semesterId,
                request.StaffId,
                ct);
            if (!validationResult.IsSuccess)
                return Result<IReadOnlyList<AssignmentDTO>>.Failure(validationResult.Error!, validationResult.ErrorCode);

            eligible = await _db.StudentSubmissions
                .AsNoTracking()
                .Where(s => requestedIds!.Contains(s.SubmissionId))
                .OrderBy(s => s.CreatedDate)
                .ToListAsync(ct);
        }
        else
        {
            var alreadyAssigned = _db.Assignments
                .Where(a => a.GradingSessionId == gradingSessionId)
                .Select(a => a.SubmissionId);

            // Students this monitor is linked to in this semester: their work must not be graded by this monitor.
            var ownStudents = _db.StudentSemesters
                .Where(ss => ss.SemesterId == semesterId && ss.StaffId == request.StaffId)
                .Select(ss => ss.StudentId);

            // A monitor who already graded or was assigned to a submission in another session (e.g. Round 1) cannot regrade it.
            var previouslyAssignedToMonitor = _db.Assignments
                .Where(a => a.StaffId == request.StaffId && a.GradingSessionId != gradingSessionId)
                .Select(a => a.SubmissionId);

            eligible = await _db.StudentSubmissions
                .AsNoTracking()
                .Where(s => s.ExaminationSessionId == examinationSessionId
                            && !alreadyAssigned.Contains(s.SubmissionId)
                            && !ownStudents.Contains(s.StudentId)
                            && !previouslyAssignedToMonitor.Contains(s.SubmissionId))
                .OrderBy(s => s.CreatedDate)
                .Take(request.NumberSubmissions)
                .ToListAsync(ct);

            if (eligible.Count == 0)
                return Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    "No eligible submissions available for this monitor.", "NO_ELIGIBLE_SUBMISSIONS");
        }

        var now = VietnamTime.Now;
        var created = eligible.Select(s => new Assignment
        {
            GradingSessionId = gradingSessionId,
            SubmissionId = s.SubmissionId,
            StaffId = request.StaffId,
            Status = AssignmentStatus.IN_PROGRESS,
            AssignedDate = now,
            UpdatedDate = now
        }).ToList();

        _db.Assignments.AddRange(created);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<IReadOnlyList<AssignmentDTO>>.Success(created.Select(Map).ToList());
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

            var wasPreviouslyAssigned = await _db.Assignments.AsNoTracking().AnyAsync(a =>
                a.SubmissionId == assignment.SubmissionId
                && a.GradingSessionId != gradingSessionId
                && a.StaffId == request.StaffId.Value, ct);
            if (wasPreviouslyAssigned)
                return Result<AssignmentDTO>.Failure(
                    "This monitor was already assigned to this submission in another grading session.",
                    "MONITOR_DUPLICATE_SUBMISSION");

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
            .Select(s => new { s.SubmissionId, s.StudentId, s.ExaminationSessionId })
            .ToListAsync(ct);

        if (submissions.Count != submissionIds.Count)
            return Result.Failure("One or more requested submissions were not found.", "SUBMISSION_NOT_FOUND");

        if (submissions.Any(s => s.ExaminationSessionId != examinationSessionId))
            return Result.Failure("One or more submissions do not belong to this examination session.", "SUBMISSION_SESSION_MISMATCH");

        var duplicateInSession = await _db.Assignments.AsNoTracking().AnyAsync(a =>
            a.GradingSessionId == gradingSessionId && submissionIds.Contains(a.SubmissionId), ct);
        if (duplicateInSession)
            return Result.Failure("One or more submissions are already assigned in this grading session.", "SUBMISSION_ALREADY_ASSIGNED");

        var studentIds = submissions.Select(s => s.StudentId).Distinct().ToList();
        var hasOwnStudent = await _db.StudentSemesters.AsNoTracking().AnyAsync(ss =>
            ss.SemesterId == semesterId && ss.StaffId == staffId && studentIds.Contains(ss.StudentId), ct);
        if (hasOwnStudent)
            return Result.Failure("This monitor is linked to one or more students and cannot grade their submissions.", "MONITOR_LINKED_TO_STUDENT");

        var gradedInOtherSession = await _db.Assignments.AsNoTracking().AnyAsync(a =>
            a.StaffId == staffId && a.GradingSessionId != gradingSessionId && submissionIds.Contains(a.SubmissionId), ct);
        if (gradedInOtherSession)
            return Result.Failure("This monitor was already assigned to one or more of these submissions in another grading session.", "MONITOR_DUPLICATE_SUBMISSION");

        return Result.Success();
    }

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