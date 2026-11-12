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
            .FirstOrDefaultAsync(a => a.SubmissionId == submission.SubmissionId && a.Status != ApplicationStatus.REJECTED, ct);

        if (existingApp is not null)
            return Result<ApplicationDTO>.Failure("Bài thi này đã có đơn phúc khảo đang được xử lý hoặc đã được duyệt.", "APPLICATION_ALREADY_EXISTS");

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

    public async Task<Result<IReadOnlyList<ApplicationDTO>>> GetMyApplicationsAsync(
        Guid currentUserId, CancellationToken ct = default)
    {
        var applications = await _db.Applications.AsNoTracking()
            .Where(a => a.StudentId == currentUserId)
            .Include(a => a.Student)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.ExaminationSession)
                    .ThenInclude(es => es!.Examination)
            .Include(a => a.StudentSubmission)
                .ThenInclude(s => s.SubmissionGradingHistories)
            .OrderByDescending(a => a.CreatedDay)
            .ToListAsync(ct);

        var list = applications.Select(a =>
        {
            var officialScore = a.StudentSubmission.SubmissionGradingHistories
                .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;
            return ToDto(a, a.Student, a.StudentSubmission, officialScore);
        }).ToList();

        return Result<IReadOnlyList<ApplicationDTO>>.Success(list);
    }

    public async Task<Result<IReadOnlyList<ApplicationDTO>>> GetAllAsync(
        Guid? examinationSessionId = null, ApplicationStatus? status = null, CancellationToken ct = default)
    {
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

        var applications = await query
            .OrderByDescending(a => a.CreatedDay)
            .ToListAsync(ct);

        var list = applications.Select(a =>
        {
            var officialScore = a.StudentSubmission.SubmissionGradingHistories
                .FirstOrDefault(h => h.Status == SubmissionGradingHistoryStatus.OFFICIAL)?.TotalScore;
            return ToDto(a, a.Student, a.StudentSubmission, officialScore);
        }).ToList();

        return Result<IReadOnlyList<ApplicationDTO>>.Success(list);
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

        application.Status = request.Status;
        application.UpdatedDay = DateTime.UtcNow;

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
