
using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Assignments;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class AssignmentService : IAssignmentService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IEmailBackgroundQueue? _emailQueue;
    private readonly ILogger<AssignmentService> _logger;

    public AssignmentService(
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILogger<AssignmentService> logger,
        IEmailBackgroundQueue? emailQueue = null)
    {
        _db = db;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
        _emailQueue = emailQueue;
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
        var semesterId = gradingSession.ExaminationSession.SemesterId;

        // Submissions that already have a monitor in this grading session are ignored.
        var assignedIds = await _db.Assignments
            .AsNoTracking()
            .Where(a => a.GradingSessionId == gradingSessionId)
            .Select(a => a.SubmissionId)
            .ToListAsync(ct);

        var candidates = await _db.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.ExaminationSessionId == examinationSessionId
                        && !assignedIds.Contains(s.SubmissionId))
            .OrderBy(s => s.CreatedDate)
            .Select(s => new { s.SubmissionId, s.StudentId })
            .ToListAsync(ct);

        var candidateStudentIds = candidates.Select(c => c.StudentId).Distinct().ToList();
        var candidateSubmissionIds = candidates.Select(c => c.SubmissionId).ToList();

        // Same rules as a manual reassignment (UpdateAsync): a monitor never grades a
        // student they are linked to in this semester, nor a submission they already
        // graded in another grading session (e.g. round 1 before a regrade).
        var linkedPairs = (await _db.StudentSemesters
                .AsNoTracking()
                .Where(ss => ss.SemesterId == semesterId
                             && candidateStudentIds.Contains(ss.StudentId)
                             && monitorIds.Contains(ss.StaffId))
                .Select(ss => new { ss.StudentId, ss.StaffId })
                .ToListAsync(ct))
            .Select(x => (x.StudentId, x.StaffId))
            .ToHashSet();

        var previousPairs = (await _db.Assignments
                .AsNoTracking()
                .Where(a => candidateSubmissionIds.Contains(a.SubmissionId)
                            && a.GradingSessionId != gradingSessionId
                            && monitorIds.Contains(a.StaffId))
                .Select(a => new { a.SubmissionId, a.StaffId })
                .ToListAsync(ct))
            .Select(x => (x.SubmissionId, x.StaffId))
            .ToHashSet();

        bool IsEligible(Guid monitorId, Guid submissionId, Guid studentId)
            => !linkedPairs.Contains((studentId, monitorId))
               && !previousPairs.Contains((submissionId, monitorId));

        // Phase 1: Feasible initial allocation using bipartite matching (augmenting paths).
        // Ensure every monitor gets at least 1 submission if feasible, allowing reassignment of provisional choices.
        var submissionToMonitor = new Dictionary<Guid, Guid>();
        if (candidates.Count >= monitorIds.Count)
        {
            bool TryMatch(Guid mId, HashSet<Guid> visited)
            {
                foreach (var c in candidates)
                {
                    if (IsEligible(mId, c.SubmissionId, c.StudentId) && visited.Add(c.SubmissionId))
                    {
                        if (!submissionToMonitor.TryGetValue(c.SubmissionId, out var existingMonitor) ||
                            TryMatch(existingMonitor, visited))
                        {
                            submissionToMonitor[c.SubmissionId] = mId;
                            return true;
                        }
                    }
                }
                return false;
            }

            foreach (var monitorId in monitorIds)
            {
                TryMatch(monitorId, new HashSet<Guid>());
            }
        }

        var allocation = monitorIds.ToDictionary(id => id, _ => new List<Guid>());
        var taken = new HashSet<Guid>();

        if (submissionToMonitor.Values.Distinct().Count() == monitorIds.Count)
        {
            foreach (var kvp in submissionToMonitor)
            {
                allocation[kvp.Value].Add(kvp.Key);
                taken.Add(kvp.Key);
            }
        }

        // Phase 2: Round-robin fill up to request.SubmissionPerMonitor
        var progressed = true;
        while (progressed)
        {
            progressed = false;

            foreach (var monitorId in monitorIds)
            {
                if (allocation[monitorId].Count >= request.SubmissionPerMonitor)
                    continue;

                var next = candidates.FirstOrDefault(c =>
                    !taken.Contains(c.SubmissionId)
                    && IsEligible(monitorId, c.SubmissionId, c.StudentId));

                if (next is null)
                    continue;

                allocation[monitorId].Add(next.SubmissionId);
                taken.Add(next.SubmissionId);
                progressed = true;
            }
        }

        // Fewer submissions than requested is fine as long as every monitor still gets at
        // least one.
        var emptyMonitorId = monitorIds.FirstOrDefault(id => allocation[id].Count == 0);
        if (emptyMonitorId != Guid.Empty)
        {
            var hasEligible = candidates.Any(c => IsEligible(emptyMonitorId, c.SubmissionId, c.StudentId));

            return hasEligible || candidates.Count < monitorIds.Count
                ? Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    $"Not enough unassigned submissions in this grading session: " +
                    $"{monitorIds.Count} monitor(s) need at least one submission each, " +
                    $"{candidates.Count} available.",
                    "INSUFFICIENT_SUBMISSIONS")
                : Result<IReadOnlyList<AssignmentDTO>>.Failure(
                    "This monitor is linked to every remaining student or already graded " +
                    "these submissions in another grading session.",
                    "MONITOR_NO_ELIGIBLE_SUBMISSION");
        }

        var now = VietnamTime.Now;
        var created = monitorIds
            .SelectMany(monitorId => allocation[monitorId].Select(submissionId => new Assignment
            {
                GradingSessionId = gradingSessionId,
                SubmissionId = submissionId,
                StaffId = monitorId,
                Status = AssignmentStatus.IN_PROGRESS,
                AssignedDate = now,
                UpdatedDate = now
            }))
            .ToList();

        _db.Assignments.AddRange(created);

        // An assigned submission is now waiting to be graded.
        var assignedSubmissionIds = created
            .Select(a => a.SubmissionId)
            .Distinct()
            .ToList();

        var assignedSubmissions = await _db.StudentSubmissions
            .Where(s => assignedSubmissionIds.Contains(s.SubmissionId))
            .ToListAsync(ct);

        foreach (var submission in assignedSubmissions)
        {
            if (submission.Status >= SubmissionStatus.Grading)
                continue;

            submission.Status = SubmissionStatus.Grading;
            submission.UpdatedDate = now;
        }

        await _unitOfWork.SaveChangesAsync(ct);

        await SendAssignmentEmailsAsync(gradingSession, created, ct);

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
        // Vietnam local date: VietnamTime.Now is UTC, so using it here would keep
        // a monitor busy on the day after the deadline until 07:00 local time.
        var today = VietnamTime.Today;
        var isBusy = MonitorAvailability.IsBusyAssignment(today);

        var busy = await _db.Assignments
            .AsNoTracking()
            .Where(a =>
                a.StaffId == staffId &&
                a.GradingSessionId != currentGradingSessionId)
            .Where(isBusy)
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
    // SEND ASSIGNMENT NOTIFICATION EMAILS
    // Each monitor receives an email with the number of assigned
    // submissions and the student codes they have to grade.
    // A failing SMTP call must never fail the assignment request.
    // =========================================================

    private async Task SendAssignmentEmailsAsync(
        GradingSession gradingSession,
        IReadOnlyList<Assignment> created,
        CancellationToken ct)
    {
        if (created.Count == 0)
            return;

        var monitorIds = created
            .Select(a => a.StaffId)
            .Distinct()
            .ToList();

        var monitors = await _db.Staff
            .AsNoTracking()
            .Where(s => monitorIds.Contains(s.Id))
            .Select(s => new
            {
                s.Id,
                s.StaffCode,
                s.FullName,
                s.Email
            })
            .ToListAsync(ct);

        if (monitors.Count == 0)
            return;

        var submissionIds = created
            .Select(a => a.SubmissionId)
            .Distinct()
            .ToList();

        var studentCodes = await _db.StudentSubmissions
            .AsNoTracking()
            .Where(s => submissionIds.Contains(s.SubmissionId))
            .Select(s => new
            {
                s.SubmissionId,
                s.Student.StudentCode
            })
            .ToDictionaryAsync(s => s.SubmissionId, s => s.StudentCode, ct);

        var examinationSession = await _db.ExaminationSessions
            .AsNoTracking()
            .Where(e => e.ExaminationSessionId == gradingSession.ExaminationSessionId)
            .Select(e => new
            {
                e.ExaminationCode,
                ExaminationName = e.Examination.Name,
                SemesterName = e.Examination.Semester.Name,
                SemesterCode = e.Examination.Semester.SemesterCode
            })
            .FirstOrDefaultAsync(ct);

        var deadlineText = gradingSession.Deadline is { } deadline
            ? deadline.ToString("dd/MM/yyyy")
            : "Không có";

        foreach (var monitor in monitors)
        {
            var codes = created
                .Where(a => a.StaffId == monitor.Id)
                .Select(a => studentCodes.TryGetValue(a.SubmissionId, out var code) ? code : null)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
                continue;

            var body = BuildAssignmentEmail(
                monitor.FullName,
                gradingSession.Title,
                examinationSession?.ExaminationName ?? string.Empty,
                examinationSession?.ExaminationCode ?? string.Empty,
                examinationSession?.SemesterName ?? string.Empty,
                deadlineText,
                codes);

            if (_emailQueue is not null)
            {
                _emailQueue.QueueEmail(new EmailMessage(
                    monitor.Email,
                    $"[SWR Grading System] Phân công chấm bài - {gradingSession.Title}",
                    body));
            }
            else
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        monitor.Email,
                        $"[SWR Grading System] Phân công chấm bài - {gradingSession.Title}",
                        body,
                        ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Lỗi khi gửi email phân công chấm bài cho monitor {Email}",
                        monitor.Email);
                }
            }
        }
    }

    private static string BuildAssignmentEmail(
        string monitorName,
        string gradingSessionTitle,
        string examinationName,
        string examinationCode,
        string semesterName,
        string deadline,
        IReadOnlyList<string> studentCodes)
    {
        var studentList = string.Join(
            Environment.NewLine,
            studentCodes.Select(code =>
                $"<li style=\"margin: 4px 0;\"><code style=\"background: #edf2f7; padding: 2px 6px; border-radius: 4px;\">{code}</code></li>"));

        return $@"
        <div style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; border: 1px solid #e1e4e8; border-radius: 8px; overflow: hidden;"">
            <div style=""background-color: #2b6cb0; padding: 20px; text-align: center; color: white;"">
                <h2 style=""margin: 0;"">Hệ Thống Chấm Điểm Tự Động SWR</h2>
            </div>
            <div style=""padding: 24px;"">
                <p>Xin chào <strong>{monitorName}</strong>,</p>
                <p>Bạn đã được phân công chấm bài cho phiên chấm điểm <strong>{gradingSessionTitle}</strong>. Dưới đây là thông tin chi tiết:</p>
                <div style=""background-color: #f7fafc; border-left: 4px solid #2b6cb0; padding: 15px; margin: 20px 0;"">
                    <p style=""margin: 5px 0;""><strong>Bài thi:</strong> {examinationName} ({examinationCode})</p>
                    <p style=""margin: 5px 0;""><strong>Học kỳ:</strong> {semesterName}</p>
                    <p style=""margin: 5px 0;""><strong>Hạn chấm bài:</strong> {deadline}</p>
                    <p style=""margin: 5px 0;""><strong>Số bài phải chấm:</strong> {studentCodes.Count}</p>
                </div>
                <p><strong>Danh sách mã sinh viên cần chấm:</strong></p>
                <ul style=""margin: 10px 0; padding-left: 20px;"">{studentList}</ul>
                <p>Vui lòng đăng nhập vào hệ thống và hoàn thành việc chấm bài trước thời hạn.</p>
                <br/>
                <p>Trân trọng,<br/><strong>Ban Quản Trị Hệ Thống SWR</strong></p>
            </div>
            <div style=""background-color: #edf2f7; padding: 12px; text-align: center; font-size: 12px; color: #718096;"">
                Đây là email tự động. Vui lòng không trả lời trực tiếp email này.
            </div>
        </div>";
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
