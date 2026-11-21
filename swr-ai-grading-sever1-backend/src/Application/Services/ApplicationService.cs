using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Applications;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class ApplicationService : IApplicationService
{
    private readonly IApplicationDbContext _db;

    public ApplicationService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Result<ApplicationDTO>> CreateAsync(
        CreateApplicationRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result<ApplicationDTO>.Failure("Lý do phúc khảo không được để trống.", "INVALID_REASON");

        var student = await _db.Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == currentUserId && !s.IsDeleted, ct);
        if (student is null)
            return Result<ApplicationDTO>.Failure("Chỉ sinh viên mới có quyền tạo đơn phúc khảo.", "STUDENT_NOT_FOUND");

        var submission = await _db.StudentSubmissions
            .Include(s => s.ExaminationSession)
                .ThenInclude(es => es!.Examination)
            .Include(s => s.SubmissionGradingHistories)
            .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId, ct);

        if (submission is null)
            return Result<ApplicationDTO>.Failure("Không tìm thấy bài thi cần phúc khảo.", "SUBMISSION_NOT_FOUND");

        if (submission.StudentId != student.Id)
            return Result<ApplicationDTO>.Failure("Bạn không thể phúc khảo bài thi của sinh viên khác.", "FORBIDDEN");

        var officialGrading = submission.SubmissionGradingHistories
            .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL);

        if (officialGrading is null)
            return Result<ApplicationDTO>.Failure("Bài thi chưa được công bố điểm chính thức, không thể nộp đơn phúc khảo.", "NOT_OFFICIALLY_GRADED");

        var existingApp = await _db.Applications
            .FirstOrDefaultAsync(a => a.SubmissionId == submission.SubmissionId, ct);

        if (existingApp is not null)
        {
            if (existingApp.Status == ApplicationStatus.REJECTED)
            {
                // Verify that the stored status in database is still REJECTED to prevent stale overwrites
                var isStillRejected = await _db.Applications.AsNoTracking()
                    .AnyAsync(a => a.Id == existingApp.Id && a.Status == ApplicationStatus.REJECTED, ct);

                if (!isStillRejected)
                {
                    return Result<ApplicationDTO>.Failure("Trạng thái đơn phúc khảo đã thay đổi, không thể nộp lại lúc này.", "APPLICATION_STATUS_CONFLICT");
                }

                existingApp.reason = request.Reason.Trim();
                existingApp.Status = ApplicationStatus.PENDING;
                existingApp.UpdatedDay = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
                return Result<ApplicationDTO>.Success(ToDto(existingApp, student, submission, officialGrading?.TotalScore));
            }

            return Result<ApplicationDTO>.Failure("Bài thi này đã có đơn phúc khảo, không thể nộp thêm đơn mới.", "APPLICATION_ALREADY_EXISTS");
        }

        var now = DateTime.UtcNow;
        var application = new Domain.Entities.Application
        {
            Id = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            StudentId = student.Id,
            reason = request.Reason.Trim(),
            Status = ApplicationStatus.PENDING,
            CreatedDay = now,
            UpdatedDay = now
        };

        _db.Applications.Add(application);
        await _db.SaveChangesAsync(ct);

        return Result<ApplicationDTO>.Success(ToDto(application, student, submission, officialGrading?.TotalScore));
    }

    public async Task<Result<PagedResult<ApplicationDTO>>> GetMyApplicationsAsync(
        PagedRequest? request, Guid currentUserId, CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var query = _db.Applications.AsNoTracking()
            .Where(a => a.StudentId == currentUserId);

        var totalCount = await query.CountAsync(ct);

        var applications = await query
            .Include(a => a.Student)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.ExaminationSession)
                    .ThenInclude(es => es!.Examination)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.SubmissionGradingHistories)
            .OrderByDescending(a => a.CreatedDay)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var list = applications.Select(a =>
        {
            var officialScore = a.StudentSubmission.SubmissionGradingHistories
                .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;
            return ToDto(a, a.Student, a.StudentSubmission, officialScore);
        }).ToList();

        var pagedResult = new PagedResult<ApplicationDTO>
        {
            Items = list,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<ApplicationDTO>>.Success(pagedResult);
    }

    public async Task<Result<PagedResult<ApplicationDTO>>> GetAllAsync(
        PagedRequest? request,
        Guid? examinationSessionId = null,
        ApplicationStatus? status = null,
        CancellationToken ct = default)
    {
        request ??= new PagedRequest();
        var query = _db.Applications.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.ExaminationSession)
                    .ThenInclude(es => es!.Examination)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.SubmissionGradingHistories)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (examinationSessionId.HasValue)
            query = query.Where(a => a.StudentSubmission.ExaminationSessionId == examinationSessionId.Value);

        var totalCount = await query.CountAsync(ct);

        var applications = await query
            .OrderByDescending(a => a.CreatedDay)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var list = applications.Select(a =>
        {
            var officialScore = a.StudentSubmission.SubmissionGradingHistories
                .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;
            return ToDto(a, a.Student, a.StudentSubmission, officialScore);
        }).ToList();

        var pagedResult = new PagedResult<ApplicationDTO>
        {
            Items = list,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<ApplicationDTO>>.Success(pagedResult);
    }

    public async Task<Result<ApplicationDTO>> GetByIdAsync(
        Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var application = await _db.Applications.AsNoTracking()
            .Include(a => a.Student)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.ExaminationSession)
                    .ThenInclude(es => es!.Examination)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.SubmissionGradingHistories)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application is null)
            return Result<ApplicationDTO>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");

        if (!isElevatedRole && application.StudentId != currentUserId)
            return Result<ApplicationDTO>.Failure("Bạn không có quyền xem đơn phúc khảo này.", "FORBIDDEN");

        var officialScore = application.StudentSubmission.SubmissionGradingHistories
            .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;

        return Result<ApplicationDTO>.Success(ToDto(application, application.Student, application.StudentSubmission, officialScore));
    }

    public async Task<Result<ApplicationDTO>> UpdateStatusAsync(
        Guid id, UpdateApplicationStatusRequest request, Guid currentUserId, CancellationToken ct = default)
    {
        var application = await _db.Applications
            .Include(a => a.Student)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.ExaminationSession)
                    .ThenInclude(es => es!.Examination)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.SubmissionGradingHistories)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (application is null)
            return Result<ApplicationDTO>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");

        if (!Enum.IsDefined(typeof(ApplicationStatus), request.Status))
            return Result<ApplicationDTO>.Failure("Trạng thái đơn phúc khảo không hợp lệ.", "INVALID_STATUS");

        if (application.Status != request.Status)
        {
            var isValidTransition = (application.Status, request.Status) switch
            {
                (ApplicationStatus.PENDING, ApplicationStatus.APPROVED) => true,
                (ApplicationStatus.PENDING, ApplicationStatus.REJECTED) => true,
                (ApplicationStatus.APPROVED, ApplicationStatus.IN_PROGRESS) => true,
                (ApplicationStatus.APPROVED, ApplicationStatus.COMPLETED) => true,
                (ApplicationStatus.IN_PROGRESS, ApplicationStatus.COMPLETED) => true,
                _ => false
            };

            if (!isValidTransition)
            {
                return Result<ApplicationDTO>.Failure(
                    $"Không thể chuyển trạng thái đơn phúc khảo từ {application.Status} sang {request.Status}.",
                    "INVALID_STATUS_TRANSITION");
            }
        }

        var wasApproved = application.Status == ApplicationStatus.APPROVED;
        application.Status = request.Status;
        application.UpdatedDay = DateTime.UtcNow;

        // Only on the transition to APPROVED, so approving twice can't wipe a re-grade.
        // We KEEP the existing official score visible until the regrade round is finalized.
        if (request.Status == ApplicationStatus.APPROVED && !wasApproved)
        {
            // The monitor's assignment was GRADED: reopen it so the submission can be re-graded and finalized again.
            var assignments = await _db.Assignments
                .Where(a => a.SubmissionId == application.SubmissionId
                            && (a.Status == AssignmentStatus.COMPLETED || a.Status == AssignmentStatus.GRADED))
                .ToListAsync(ct);
            foreach (var assignment in assignments)
            {
                assignment.Status = AssignmentStatus.IN_PROGRESS;
                assignment.UpdatedDate = VietnamTime.Now;
            }
        }

        await _db.SaveChangesAsync(ct);
        
        var officialScore = application.StudentSubmission.SubmissionGradingHistories
            .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;

        return Result<ApplicationDTO>.Success(ToDto(application, application.Student, application.StudentSubmission, officialScore));
    }

    private static ApplicationDTO ToDto(
        Domain.Entities.Application app,
        Student? student,
        StudentSubmission? sub,
        decimal? officialScore)
    {
        return new ApplicationDTO
        {
            Id = app.Id,
            SubmissionId = app.SubmissionId,
            StudentId = app.StudentId,
            StudentCode = student?.StudentCode ?? string.Empty,
            StudentName = student?.FullName ?? string.Empty,
            Reason = app.reason,
            Status = app.Status,
            CreatedDay = app.CreatedDay,
            UpdatedDay = app.UpdatedDay,
            ExaminationName = sub?.ExaminationSession?.Examination?.Name,
            ExaminationSessionCode = sub?.ExaminationSession?.ExaminationCode,
            OfficialScore = officialScore
        };
    }
}
