using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class GradingSessionService : IGradingSessionService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public GradingSessionService(IApplicationDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<GradingSessionDTO>>> GetByExaminationSessionAsync(Guid examinationSessionId, CancellationToken ct = default)
    {
        if (!await ExaminationSessionExistsAsync(examinationSessionId, ct))
            return Result<IReadOnlyList<GradingSessionDTO>>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

        var sessions = await _db.GradingSessions
            .AsNoTracking()
            .Where(session => session.ExaminationSessionId == examinationSessionId)
            .OrderByDescending(session => session.CreatedDate)
            .ToListAsync(ct);

        return Result<IReadOnlyList<GradingSessionDTO>>.Success(sessions.Select(Map).ToList());
    }

    public async Task<Result<GradingSessionDTO>> GetByIdAsync(Guid examinationSessionId, Guid gradingSessionId, CancellationToken ct = default)
    {
        var session = await FindAsync(examinationSessionId, gradingSessionId, ct);
        return session is null
            ? Result<GradingSessionDTO>.Failure("Grading session not found.", "GRADING_SESSION_NOT_FOUND")
            : Result<GradingSessionDTO>.Success(Map(session));
    }

    public async Task<Result<GradingSessionDTO>> CreateAsync(Guid examinationSessionId, CreateGradingSessionRequest request, CancellationToken ct = default)
    {
        var examinationSession = await _db.ExaminationSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ExaminationSessionId == examinationSessionId, ct);

        if (examinationSession is null)
            return Result<GradingSessionDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

        var title = ValidateTitle(request.Title);
        if (title is null)
            return Result<GradingSessionDTO>.Failure("Title is required and must be 200 characters or fewer.", "INVALID_GRADING_SESSION_TITLE");

        var today = DateOnly.FromDateTime(VietnamTime.Now);
        if (request.Deadline == default || request.Deadline <= today)
            return Result<GradingSessionDTO>.Failure("Deadline is required and must be in the future.", "INVALID_GRADING_SESSION_DEADLINE");

        var numberSubmissions = await _db.StudentSubmissions
            .AsNoTracking()
            .CountAsync(s => s.ExaminationSessionId == examinationSessionId, ct);

        var session = new GradingSession
        {
            ExaminationSessionId = examinationSessionId,
            Title = title,
            CreatedDate = today,
            UpdatedDate = today,
            Deadline = request.Deadline,
            NumberSubmissions = numberSubmissions
        };

        _db.GradingSessions.Add(session);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<GradingSessionDTO>.Success(Map(session));
    }

    public async Task<Result<GradingSessionDTO>> UpdateAsync(
        Guid examinationSessionId,
        UpdateGradingSessionRequest request,
        CancellationToken ct = default)
    {
        var session = await _db.GradingSessions.FirstOrDefaultAsync(
            s => s.ExaminationSessionId == examinationSessionId,
            ct);

        if (session is null)
            return Result<GradingSessionDTO>.Failure(
                "Grading session not found.",
                "GRADING_SESSION_NOT_FOUND");

        if (request.Title is not null)
        {
            var title = ValidateTitle(request.Title);
            if (title is null)
                return Result<GradingSessionDTO>.Failure("Title is required and must be 200 characters or fewer.", "INVALID_GRADING_SESSION_TITLE");
            session.Title = title;
        }
        if (request.Deadline.HasValue)
            session.Deadline = request.Deadline.Value;
        if (request.NumberSubmissions.HasValue)
        {
            if (request.NumberSubmissions.Value < 0)
                return Result<GradingSessionDTO>.Failure("Number of submissions cannot be negative.", "INVALID_NUMBER_SUBMISSIONS");
            session.NumberSubmissions = request.NumberSubmissions.Value;
        }

        session.UpdatedDate = DateOnly.FromDateTime(VietnamTime.Now);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<GradingSessionDTO>.Success(Map(session));
    }

    public async Task<Result> DeleteAsync(Guid examinationSessionId, Guid gradingSessionId, CancellationToken ct = default)
    {
        var session = await FindAsync(examinationSessionId, gradingSessionId, ct);
        if (session is null)
            return Result.Failure("Grading session not found.", "GRADING_SESSION_NOT_FOUND");

        _db.GradingSessions.Remove(session);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<bool> ExaminationSessionExistsAsync(Guid examinationSessionId, CancellationToken ct)
        => await _db.ExaminationSessions.AsNoTracking().AnyAsync(
            session => session.ExaminationSessionId == examinationSessionId,
            ct);

    private async Task<GradingSession?> FindAsync(
        Guid examinationSessionId,
        Guid gradingSessionId,
        CancellationToken ct)
    {
        return await _db.GradingSessions.FirstOrDefaultAsync(
            session =>
                session.GradingSessionId == gradingSessionId &&
                session.ExaminationSessionId == examinationSessionId,
            ct);
    }

    private static string? ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return null;
        var trimmed = title.Trim();
        return trimmed.Length <= 200 ? trimmed : null;
    }

    private static GradingSessionDTO Map(GradingSession session) => new()
    {
        GradingSessionId = session.GradingSessionId,
        ExaminationSessionId = session.ExaminationSessionId,
        Title = session.Title,
        CreatedDate = session.CreatedDate,
        UpdatedDate = session.UpdatedDate,
        Deadline = session.Deadline,
        NumberSubmissions = session.NumberSubmissions
    };
}
