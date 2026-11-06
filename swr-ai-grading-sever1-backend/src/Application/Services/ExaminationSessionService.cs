using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class ExaminationSessionService : IExaminationSessionService
{
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;

    public ExaminationSessionService(IApplicationDbContext db, IUnitOfWork unitOfWork)
    {
        _db = db;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<IReadOnlyList<ExaminationSessionDTO>>> GetByExaminationAsync(Guid examinationId, CancellationToken ct = default)
    {
        var sessions = await _db.ExaminationSessions
            .AsNoTracking()
            .Where(s => s.ExaminationId == examinationId)
            .Include(s => s.StudentExaminationSessions)
            .OrderBy(s => s.StartDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return Result<IReadOnlyList<ExaminationSessionDTO>>.Success(sessions.Select(session => Map(session)).ToList());
    }

    public async Task<Result<ExaminationSessionDTO>> GetByIdAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await FindSessionAsync(examinationId, sessionId, ct);
        return session is null
            ? Result<ExaminationSessionDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND")
            : Result<ExaminationSessionDTO>.Success(Map(session));
    }

    public async Task<Result<ExaminationSessionDTO>> CreateAsync(Guid examinationId, CreateExaminationSessionRequest request, CancellationToken ct = default)
    {
        var examination = await _db.Examinations.FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);
        if (examination is null)
            return Result<ExaminationSessionDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.ExaminationCode))
            return Result<ExaminationSessionDTO>.Failure("Examination code is required.", "INVALID_EXAMINATION_SESSION");
        if (!Enum.IsDefined(request.Status))
            return Result<ExaminationSessionDTO>.Failure("Invalid examination session status.", "INVALID_EXAMINATION_SESSION_STATUS");
        if (request.StartDate < examination.StartDate || request.StartDate > examination.EndDate)
            return Result<ExaminationSessionDTO>.Failure("Session date must be within the examination dates.", "SESSION_DATE_OUT_OF_EXAMINATION");

        var paperSetId = NormalizeGuid(request.PaperSetId);
        if (paperSetId.HasValue)
        {
            var paperSet = await _db.PaperSets.FirstOrDefaultAsync(p => p.PaperSetId == paperSetId.Value && !p.IsDeleted, ct);
            if (paperSet is null)
                return Result<ExaminationSessionDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
            if (paperSet.ExaminationId != examinationId)
                return Result<ExaminationSessionDTO>.Failure("Paper set does not belong to the examination.", "PAPER_SET_EXAMINATION_MISMATCH");
        }

        var studentIds = request.StudentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        foreach (var studentId in studentIds)
        {
            if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsDeleted, ct))
                return Result<ExaminationSessionDTO>.Failure("Student not found.", "STUDENT_NOT_FOUND");
        }

        var session = new ExaminationSession
        {
            ExaminationId = examinationId,
            ExaminationCode = request.ExaminationCode.Trim(),
            StartDate = request.StartDate,
            StartTime = request.StartTime,
            Status = request.Status,
            SemesterId = examination.SemesterId,
            PaperSetId = paperSetId,
            CreatedDay = VietnamTime.Now,
            UpdatedDay = VietnamTime.Now
        };

        foreach (var studentId in studentIds)
        {
            session.StudentExaminationSessions.Add(new StudentExaminationSession
            {
                StudentId = studentId,
                ExaminationSessionId = session.ExaminationSessionId,
                ExaminationSession = session
            });
        }

        _db.ExaminationSessions.Add(session);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationSessionDTO>.Success(Map(session));
    }

    public async Task<Result<ExaminationSessionDTO>> UpdateAsync(Guid examinationId, Guid sessionId, UpdateExaminationSessionRequest request, CancellationToken ct = default)
    {
        var session = await FindSessionAsync(examinationId, sessionId, ct);
        if (session is null)
            return Result<ExaminationSessionDTO>.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

        var examination = await _db.Examinations.FirstOrDefaultAsync(e => e.ExaminationId == examinationId, ct);
        if (examination is null)
            return Result<ExaminationSessionDTO>.Failure("Examination not found.", "EXAMINATION_NOT_FOUND");

        var code = request.ExaminationCode?.Trim();
        if (code is not null && string.IsNullOrWhiteSpace(code))
            return Result<ExaminationSessionDTO>.Failure("Examination code is required.", "INVALID_EXAMINATION_SESSION");

        var startDate = request.StartDate ?? session.StartDate;
        if (startDate < examination.StartDate || startDate > examination.EndDate)
            return Result<ExaminationSessionDTO>.Failure("Session date must be within the examination dates.", "SESSION_DATE_OUT_OF_EXAMINATION");

        if (!string.IsNullOrWhiteSpace(code)) session.ExaminationCode = code;
        session.StartDate = startDate;
        if (request.StartTime.HasValue) session.StartTime = request.StartTime.Value;
        if (request.Status.HasValue)
        {
            if (!Enum.IsDefined(request.Status.Value))
                return Result<ExaminationSessionDTO>.Failure("Invalid examination session status.", "INVALID_EXAMINATION_SESSION_STATUS");
            session.Status = request.Status.Value;
        }

        if (request.PaperSetId.HasValue)
        {
            var paperSetId = NormalizeGuid(request.PaperSetId);
            if (paperSetId.HasValue)
            {
                var paperSet = await _db.PaperSets.FirstOrDefaultAsync(p => p.PaperSetId == paperSetId.Value && !p.IsDeleted, ct);
                if (paperSet is null)
                    return Result<ExaminationSessionDTO>.Failure("Paper set not found.", "PAPER_SET_NOT_FOUND");
                if (paperSet.ExaminationId != examinationId)
                    return Result<ExaminationSessionDTO>.Failure("Paper set does not belong to the examination.", "PAPER_SET_EXAMINATION_MISMATCH");
            }
            session.PaperSetId = paperSetId;
        }

        if (request.StudentIds is not null)
        {
            var studentIds = request.StudentIds.Where(id => id != Guid.Empty).Distinct().ToList();
            foreach (var studentId in studentIds)
            {
                if (!await _db.Students.AnyAsync(s => s.Id == studentId && !s.IsDeleted, ct))
                    return Result<ExaminationSessionDTO>.Failure("Student not found.", "STUDENT_NOT_FOUND");
            }

            var existing = session.StudentExaminationSessions.ToList();
            foreach (var assignment in existing.Where(a => !studentIds.Contains(a.StudentId)))
                _db.StudentExaminationSessions.Remove(assignment);
            foreach (var studentId in studentIds.Where(id => existing.All(a => a.StudentId != id)))
                _db.StudentExaminationSessions.Add(new StudentExaminationSession { StudentId = studentId, ExaminationSessionId = sessionId });
        }

        session.UpdatedDay = VietnamTime.Now;
        await _unitOfWork.SaveChangesAsync(ct);
        return Result<ExaminationSessionDTO>.Success(await MapLoadedAsync(session, ct));
    }

    public async Task<Result> DeleteAsync(Guid examinationId, Guid sessionId, CancellationToken ct = default)
    {
        var session = await FindSessionAsync(examinationId, sessionId, ct);
        if (session is null)
            return Result.Failure("Examination session not found.", "EXAMINATION_SESSION_NOT_FOUND");

        _db.ExaminationSessions.Remove(session);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<ExaminationSession?> FindSessionAsync(Guid examinationId, Guid sessionId, CancellationToken ct)
        => await _db.ExaminationSessions
            .Include(s => s.StudentExaminationSessions)
            .FirstOrDefaultAsync(s => s.ExaminationId == examinationId && s.ExaminationSessionId == sessionId, ct);

    private async Task<ExaminationSessionDTO> MapLoadedAsync(ExaminationSession session, CancellationToken ct)
    {
        var studentIds = await _db.StudentExaminationSessions
            .AsNoTracking()
            .Where(a => a.ExaminationSessionId == session.ExaminationSessionId)
            .Select(a => a.StudentId)
            .ToListAsync(ct);
        return Map(session, studentIds);
    }

    private static ExaminationSessionDTO Map(ExaminationSession session, IReadOnlyList<Guid>? studentIds = null) => new()
    {
        ExaminationSessionId = session.ExaminationSessionId,
        ExaminationId = session.ExaminationId,
        ExaminationCode = session.ExaminationCode,
        StartDate = session.StartDate,
        StartTime = session.StartTime,
        Status = session.Status,
        SemesterId = session.SemesterId,
        PaperSetId = session.PaperSetId,
        StudentIds = studentIds?.ToList() ?? session.StudentExaminationSessions.Select(a => a.StudentId).ToList()
    };

    private static Guid? NormalizeGuid(Guid? id) => id.HasValue && id.Value != Guid.Empty ? id.Value : null;
}
