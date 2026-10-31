using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Regrades;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public sealed class RegradeService : IRegradeService
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<RegradeService> _logger;

    public RegradeService(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<RegradeService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<Result<RegradeApplicationDto>> CreateApplicationAsync(
        CreateRegradeApplicationRequest request,
        Guid studentUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Result<RegradeApplicationDto>.Failure("Lý do phúc khảo không được để trống.", "REASON_REQUIRED");
        }

        var submission = await _context.StudentSubmissions
            .Include(s => s.Evaluations)
            .Include(s => s.Student)
            .Include(s => s.GradingDiary)
            .FirstOrDefaultAsync(s => s.SubmissionId == request.SubmissionId, ct);

        if (submission is null)
        {
            return Result<RegradeApplicationDto>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");
        }

        if (submission.StudentId != studentUserId)
        {
            return Result<RegradeApplicationDto>.Failure("Bạn không phải chủ sở hữu của bài nộp này.", "FORBIDDEN");
        }

        // Kiểm tra xem bài nộp đã có điểm chưa (Final hoặc Lecturer_Reviewed)
        if (submission.Status != SubmissionStatus.Final && submission.Status != SubmissionStatus.Lecturer_Reviewed && submission.Status != SubmissionStatus.ReGraded)
        {
            return Result<RegradeApplicationDto>.Failure("Bài nộp chưa hoàn thành chấm điểm nên không thể yêu cầu phúc khảo.", "SUBMISSION_NOT_FINALIZED");
        }

        // Kiểm tra xem đã có đơn đang chờ duyệt hoặc đang chấm lại hay chưa
        var hasActiveApplication = await _context.RegradeApplications
            .AnyAsync(r => r.SubmissionId == submission.SubmissionId &&
                          (r.Status == RegradeApplicationStatus.Pending || r.Status == RegradeApplicationStatus.Approved), ct);

        if (hasActiveApplication)
        {
            return Result<RegradeApplicationDto>.Failure("Bài nộp này đã có đơn phúc khảo đang được xử lý.", "DUPLICATE_APPLICATION");
        }

        // Lấy điểm hiện tại của bài nộp
        var currentScore = submission.Evaluations
            .Where(e => e.IsFinalScore && e.TotalScore.HasValue)
            .Select(e => e.TotalScore)
            .FirstOrDefault()
            ?? submission.Evaluations
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => e.TotalScore)
                .FirstOrDefault();

        var application = new RegradeApplication
        {
            ApplicationId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            StudentId = studentUserId,
            Reason = request.Reason.Trim(),
            Status = RegradeApplicationStatus.Pending,
            OriginalScore = currentScore,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        await _context.RegradeApplications.AddAsync(application, ct);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Student {StudentId} created regrade application {AppId} for submission {SubId}",
            studentUserId, application.ApplicationId, submission.SubmissionId);

        return await GetApplicationByIdInternalAsync(application.ApplicationId, ct);
    }

    public async Task<Result<RegradeApplicationDto>> ReviewApplicationAsync(
        Guid applicationId,
        ReviewRegradeApplicationRequest request,
        Guid adminUserId,
        CancellationToken ct = default)
    {
        var application = await _context.RegradeApplications
            .Include(r => r.Submission)
                .ThenInclude(s => s.Student)
            .Include(r => r.Submission)
                .ThenInclude(s => s.GradingDiary)
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);

        if (application is null)
        {
            return Result<RegradeApplicationDto>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");
        }

        if (application.Status != RegradeApplicationStatus.Pending)
        {
            return Result<RegradeApplicationDto>.Failure("Chỉ có thể xét duyệt đơn ở trạng thái Chờ duyệt (Pending).", "INVALID_STATUS");
        }

        application.AdminId = adminUserId;
        application.AdminFeedback = request.AdminFeedback?.Trim();
        application.UpdatedDate = DateTime.UtcNow;

        if (request.IsApproved)
        {
            if (!request.AssignedLecturerId.HasValue || request.AssignedLecturerId.Value == Guid.Empty)
            {
                return Result<RegradeApplicationDto>.Failure("Khi chấp nhận đơn phúc khảo, bắt buộc phải chọn giảng viên chấm lại.", "LECTURER_REQUIRED");
            }

            var lecturer = await _context.Lecturers.FirstOrDefaultAsync(l => l.Id == request.AssignedLecturerId.Value, ct);
            if (lecturer is null)
            {
                return Result<RegradeApplicationDto>.Failure("Giảng viên được chọn không tồn tại.", "LECTURER_NOT_FOUND");
            }

            application.Status = RegradeApplicationStatus.Approved;
            application.AssignedLecturerId = lecturer.Id;

            // Chuyển trạng thái bài nộp sang ReGrading
            application.Submission.Status = SubmissionStatus.ReGrading;
            application.Submission.UpdatedDate = DateTime.UtcNow;

            _logger.LogInformation("Admin {AdminId} approved regrade app {AppId}, assigned to lecturer {LecturerId}. Submission status -> ReGrading",
                adminUserId, applicationId, lecturer.Id);

            // Gửi email thông báo cho học sinh là đơn đã được duyệt
            try
            {
                var body = $@"
                    <p>Xin chào <strong>{application.Submission.Student.FullName}</strong>,</p>
                    <p>Đơn xin phúc khảo cho bài thi của bạn đã được <strong>Chấp nhận</strong>.</p>
                    <p>Hệ thống đã phân công giảng viên chấm lại bài thi của bạn. Kết quả sẽ được cập nhật sau khi chấm hoàn tất.</p>
                    {(string.IsNullOrWhiteSpace(request.AdminFeedback) ? "" : $"<p><strong>Ghi chú từ Admin:</strong> {request.AdminFeedback}</p>")}";

                await _emailService.SendEmailAsync(
                    application.Submission.Student.Email,
                    "[SWR Grading System] Đơn phúc khảo của bạn đã được duyệt",
                    body,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email approval to student {Email}", application.Submission.Student.Email);
            }
        }
        else
        {
            application.Status = RegradeApplicationStatus.Rejected;

            _logger.LogInformation("Admin {AdminId} rejected regrade app {AppId}", adminUserId, applicationId);

            // Gửi email thông báo cho học sinh là đơn bị từ chối
            try
            {
                var body = $@"
                    <p>Xin chào <strong>{application.Submission.Student.FullName}</strong>,</p>
                    <p>Rất tiếc, đơn xin phúc khảo cho bài thi của bạn đã bị <strong>Từ chối</strong>.</p>
                    {(string.IsNullOrWhiteSpace(request.AdminFeedback) ? "" : $"<p><strong>Lý do từ chối:</strong> {request.AdminFeedback}</p>")}";

                await _emailService.SendEmailAsync(
                    application.Submission.Student.Email,
                    "[SWR Grading System] Kết quả xét duyệt đơn phúc khảo",
                    body,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email rejection to student {Email}", application.Submission.Student.Email);
            }
        }

        await _context.SaveChangesAsync(ct);
        return await GetApplicationByIdInternalAsync(application.ApplicationId, ct);
    }

    public async Task<Result<RegradeApplicationDto>> GradeApplicationAsync(
        Guid applicationId,
        GradeRegradeApplicationRequest request,
        Guid lecturerUserId,
        CancellationToken ct = default)
    {
        if (request.RegradedScore < 0 || request.RegradedScore > 10)
        {
            return Result<RegradeApplicationDto>.Failure("Điểm phúc khảo phải nằm trong khoảng từ 0 đến 10.", "INVALID_SCORE");
        }

        var application = await _context.RegradeApplications
            .Include(r => r.Submission)
                .ThenInclude(s => s.Evaluations)
            .Include(r => r.Submission)
                .ThenInclude(s => s.Student)
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);

        if (application is null)
        {
            return Result<RegradeApplicationDto>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");
        }

        if (application.Status != RegradeApplicationStatus.Approved)
        {
            return Result<RegradeApplicationDto>.Failure("Chỉ có thể chấm bài với đơn đã được Admin phê duyệt (Approved).", "INVALID_STATUS");
        }

        if (application.AssignedLecturerId != lecturerUserId)
        {
            return Result<RegradeApplicationDto>.Failure("Bạn không phải giảng viên được phân công chấm đơn phúc khảo này.", "FORBIDDEN");
        }

        var submission = application.Submission;

        // Reset tất cả IsFinalScore cũ
        foreach (var eval in submission.Evaluations)
        {
            eval.IsFinalScore = false;
        }

        var maxRound = submission.Evaluations.Count > 0 ? submission.Evaluations.Max(e => e.EvaluationRound) : 0;

        // Tạo bản ghi SubmissionEvaluation mới cho lượt chấm phúc khảo
        var regradeEvaluation = new SubmissionEvaluation
        {
            EvaluationId = Guid.NewGuid(),
            SubmissionId = submission.SubmissionId,
            EvaluationType = EvaluationType.Lecturer,
            EvaluationRound = maxRound + 1,
            TotalScore = request.RegradedScore,
            GeneralFeedback = !string.IsNullOrWhiteSpace(request.LecturerFeedback)
                ? $"[Phúc khảo] {request.LecturerFeedback.Trim()}"
                : "[Phúc khảo] Điểm sau khi chấm lại.",
            IsFinalScore = true,
            AdminSelectedId = lecturerUserId,
            SelectionReason = "Chấm lại theo đơn phúc khảo",
            CreatedAt = DateTime.UtcNow
        };

        await _context.SubmissionEvaluations.AddAsync(regradeEvaluation, ct);
        submission.Evaluations.Add(regradeEvaluation);

        // Cập nhật trạng thái bài nộp sang ReGraded
        submission.Status = SubmissionStatus.ReGraded;
        submission.UpdatedDate = DateTime.UtcNow;

        // Cập nhật trạng thái đơn phúc khảo sang Completed
        application.Status = RegradeApplicationStatus.Completed;
        application.RegradedScore = request.RegradedScore;
        application.LecturerFeedback = request.LecturerFeedback?.Trim();
        application.UpdatedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Lecturer {LecturerId} completed regrade for app {AppId}, score: {Score}. Submission status -> ReGraded",
            lecturerUserId, applicationId, request.RegradedScore);

        // Gửi email thông báo điểm mới cho học sinh
        try
        {
            var body = $@"
                <p>Xin chào <strong>{submission.Student.FullName}</strong>,</p>
                <p>Bài thi được yêu cầu phúc khảo của bạn đã được giảng viên hoàn thành chấm lại.</p>
                <div style=""background-color: #f7fafc; border-left: 4px solid #3182ce; padding: 12px; margin: 15px 0;"">
                    <p style=""margin: 4px 0;""><strong>Điểm ban đầu:</strong> {application.OriginalScore?.ToString("0.##") ?? "N/A"}</p>
                    <p style=""margin: 4px 0;""><strong>Điểm phúc khảo mới:</strong> <span style=""font-size: 16px; font-weight: bold; color: #2b6cb0;"">{request.RegradedScore:0.##}</span></p>
                    {(string.IsNullOrWhiteSpace(request.LecturerFeedback) ? "" : $"<p style=\"margin: 4px 0;\"><strong>Nhận xét của giảng viên chấm lại:</strong> {request.LecturerFeedback}</p>")}
                </div>
                <p>Vui lòng đăng nhập hệ thống để xem chi tiết.</p>";

            await _emailService.SendEmailAsync(
                submission.Student.Email,
                "[SWR Grading System] Kết quả chấm phúc khảo bài thi",
                body,
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email regraded result to student {Email}", submission.Student.Email);
        }

        return await GetApplicationByIdInternalAsync(application.ApplicationId, ct);
    }

    public async Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForAdminAsync(
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default)
    {
        var query = BuildBaseQuery();

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        if (request.SubmissionId.HasValue && request.SubmissionId.Value != Guid.Empty)
        {
            query = query.Where(r => r.SubmissionId == request.SubmissionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(r =>
                r.Student.StudentCode.ToLower().Contains(search) ||
                r.Student.FullName.ToLower().Contains(search) ||
                r.Student.Email.ToLower().Contains(search) ||
                (r.AssignedLecturer != null && r.AssignedLecturer.FullName.ToLower().Contains(search)));
        }

        return await PaginateQueryAsync(query, request.Page, request.PageSize, ct);
    }

    public async Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForStudentAsync(
        Guid studentUserId,
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default)
    {
        var query = BuildBaseQuery().Where(r => r.StudentId == studentUserId);

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        return await PaginateQueryAsync(query, request.Page, request.PageSize, ct);
    }

    public async Task<Result<PagedResult<RegradeApplicationDto>>> GetApplicationsForLecturerAsync(
        Guid lecturerUserId,
        GetRegradeApplicationsRequest request,
        CancellationToken ct = default)
    {
        var query = BuildBaseQuery().Where(r => r.AssignedLecturerId == lecturerUserId);

        if (request.Status.HasValue)
        {
            query = query.Where(r => r.Status == request.Status.Value);
        }

        return await PaginateQueryAsync(query, request.Page, request.PageSize, ct);
    }

    public async Task<Result<RegradeApplicationDto>> GetApplicationByIdAsync(
        Guid applicationId,
        Guid currentUserId,
        string role,
        CancellationToken ct = default)
    {
        var app = await BuildBaseQuery().FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);
        if (app is null)
        {
            return Result<RegradeApplicationDto>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");
        }

        if (role == "Student" && app.StudentId != currentUserId)
        {
            return Result<RegradeApplicationDto>.Failure("Bạn không có quyền xem đơn phúc khảo này.", "FORBIDDEN");
        }

        if (role == "Lecturer" && app.AssignedLecturerId != currentUserId)
        {
            return Result<RegradeApplicationDto>.Failure("Bạn không có quyền xem đơn phúc khảo này.", "FORBIDDEN");
        }

        return Result<RegradeApplicationDto>.Success(MapToDto(app));
    }

    private async Task<Result<RegradeApplicationDto>> GetApplicationByIdInternalAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await BuildBaseQuery().FirstOrDefaultAsync(r => r.ApplicationId == applicationId, ct);
        if (app is null)
        {
            return Result<RegradeApplicationDto>.Failure("Không tìm thấy đơn phúc khảo.", "APPLICATION_NOT_FOUND");
        }
        return Result<RegradeApplicationDto>.Success(MapToDto(app));
    }

    private IQueryable<RegradeApplication> BuildBaseQuery()
    {
        return _context.RegradeApplications
            .AsNoTracking()
            .Include(r => r.Student)
            .Include(r => r.AssignedLecturer)
            .Include(r => r.Admin)
            .Include(r => r.Submission)
                .ThenInclude(s => s.GradingDiary)
            .OrderByDescending(r => r.CreatedDate);
    }

    private static async Task<Result<PagedResult<RegradeApplicationDto>>> PaginateQueryAsync(
        IQueryable<RegradeApplication> query,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = items.Select(MapToDto).ToList();

        var result = new PagedResult<RegradeApplicationDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Result<PagedResult<RegradeApplicationDto>>.Success(result);
    }

    private static RegradeApplicationDto MapToDto(RegradeApplication r)
    {
        return new RegradeApplicationDto
        {
            ApplicationId = r.ApplicationId,
            SubmissionId = r.SubmissionId,
            StudentId = r.StudentId,
            StudentCode = r.Student?.StudentCode ?? string.Empty,
            StudentName = r.Student?.FullName ?? string.Empty,
            StudentEmail = r.Student?.Email ?? string.Empty,
            ExaminationCode = r.Submission?.GradingDiary?.Content ?? string.Empty,
            DiaryName = r.Submission?.GradingDiary?.Name ?? string.Empty,
            FileSubmissions = r.Submission?.FileSubmissions ?? string.Empty,
            Reason = r.Reason,
            Status = r.Status,
            AssignedLecturerId = r.AssignedLecturerId,
            AssignedLecturerName = r.AssignedLecturer?.FullName,
            AssignedLecturerCode = r.AssignedLecturer?.LecturerCode,
            AdminId = r.AdminId,
            AdminName = r.Admin?.FullName,
            AdminFeedback = r.AdminFeedback,
            OriginalScore = r.OriginalScore,
            RegradedScore = r.RegradedScore,
            LecturerFeedback = r.LecturerFeedback,
            CreatedDate = r.CreatedDate,
            UpdatedDate = r.UpdatedDate
        };
    }
}

