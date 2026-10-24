using Application.Common;
using Application.DTOs.Submissions;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed partial class SubmissionService
{
    public async Task<Result<PagedResult<AdminSubmissionItemDto>>> GetSubmissionsAsync(
        GetSubmissionsRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var query = _dbContext.StudentSubmissions
            .AsNoTracking()
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.CreatedBy)
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.PaperSet)
            .Include(s => s.Evaluations)
            .AsQueryable();

        // RBAC: Non-admin can only see submissions in their own diaries
        if (!isElevatedRole)
        {
            query = query.Where(s => s.GradingDiary.CreateById == currentUserId);
        }

        if (request.DiaryId.HasValue && request.DiaryId.Value != Guid.Empty)
        {
            query = query.Where(s => s.DiaryId == request.DiaryId.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (request.SemesterId.HasValue && request.SemesterId.Value != Guid.Empty)
        {
            query = query.Where(s => s.GradingDiary.PaperSet.SemesterId == request.SemesterId.Value);
        }

        if (request.ExaminationId.HasValue && request.ExaminationId.Value != Guid.Empty)
        {
            query = query.Where(s => s.GradingDiary.PaperSet.ExaminationId == request.ExaminationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(s =>
                s.FileSubmissions.ToLower().Contains(search) ||
                s.StudentExamination.Student.StudentCode.ToLower().Contains(search) ||
                s.StudentExamination.Student.FullName.ToLower().Contains(search) ||
                s.GradingDiary.Name.ToLower().Contains(search) ||
                s.GradingDiary.PaperSet.PaperSetCode.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(ct);

        var list = await query
            .OrderByDescending(s => s.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new
            {
                s.SubmissionId,
                StudentId = s.StudentExamination.StudentId,
                StudentCode = s.StudentExamination.Student.StudentCode ?? string.Empty,
                StudentName = s.StudentExamination.Student.FullName ?? string.Empty,
                s.FileSubmissions,
                s.Status,
                s.CreatedDate,
                s.UpdatedDate,
                s.DiaryId,
                DiaryName = s.GradingDiary.Name ?? string.Empty,
                ExaminationCode = s.GradingDiary.Content ?? string.Empty,
                PaperSetCode = s.GradingDiary.PaperSet.PaperSetCode ?? string.Empty,
                LecturerId = s.GradingDiary.CreateById,
                LecturerName = s.GradingDiary.CreatedBy.FullName ?? string.Empty,
                AiScore = s.Evaluations
                    .Where(e => e.EvaluationType == EvaluationType.AI)
                    .OrderByDescending(e => e.EvaluationRound)
                    .Select(e => e.TotalScore)
                    .FirstOrDefault(),
                LecturerScore = s.Evaluations
                    .Where(e => e.EvaluationType == EvaluationType.Lecturer)
                    .OrderByDescending(e => e.EvaluationRound)
                    .Select(e => e.TotalScore)
                    .FirstOrDefault(),
                FinalScore = s.Evaluations
                    .Where(e => e.IsFinalScore)
                    .Select(e => e.TotalScore)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        var items = list.Select(s => new AdminSubmissionItemDto
        {
            SubmissionId = s.SubmissionId,
            StudentId = s.StudentId,
            StudentCode = s.StudentCode,
            StudentName = s.StudentName,
            FileSubmissions = s.FileSubmissions,
            Status = s.Status,
            CreatedDate = s.CreatedDate,
            UpdatedDate = s.UpdatedDate,
            DiaryId = s.DiaryId,
            DiaryName = s.DiaryName,
            ExaminationCode = s.ExaminationCode,
            PaperSetCode = s.PaperSetCode,
            LecturerId = s.LecturerId,
            LecturerName = s.LecturerName,
            AiScore = s.AiScore,
            LecturerScore = s.LecturerScore,
            FinalScore = s.FinalScore ?? s.LecturerScore ?? s.AiScore
        }).ToList();

        var result = new PagedResult<AdminSubmissionItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };

        return Result<PagedResult<AdminSubmissionItemDto>>.Success(result);
    }

    public async Task<Result<AdminSubmissionItemDto>> UpdateSubmissionStatusAsync(
        Guid submissionId,
        UpdateSubmissionStatusRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var submission = await _dbContext.StudentSubmissions
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.CreatedBy)
            .Include(s => s.GradingDiary)
                .ThenInclude(gd => gd.PaperSet)
            .Include(s => s.Evaluations)
            .FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

        if (submission is null)
            return Result<AdminSubmissionItemDto>.Failure("Không tìm thấy bài nộp.", "SUBMISSION_NOT_FOUND");

        // RBAC: Only admin or the diary owner can update status
        if (!isElevatedRole && submission.GradingDiary.CreateById != currentUserId)
            return Result<AdminSubmissionItemDto>.Failure("Bạn không có quyền cập nhật trạng thái bài nộp này.", "FORBIDDEN");

        submission.Status = request.Status;
        submission.UpdatedDate = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var aiScore = submission.Evaluations
            .Where(e => e.EvaluationType == EvaluationType.AI)
            .OrderByDescending(e => e.EvaluationRound)
            .Select(e => e.TotalScore)
            .FirstOrDefault();

        var lecturerScore = submission.Evaluations
            .Where(e => e.EvaluationType == EvaluationType.Lecturer)
            .OrderByDescending(e => e.EvaluationRound)
            .Select(e => e.TotalScore)
            .FirstOrDefault();

        var finalScore = submission.Evaluations
            .Where(e => e.IsFinalScore)
            .Select(e => e.TotalScore)
            .FirstOrDefault() ?? lecturerScore ?? aiScore;

        var dto = new AdminSubmissionItemDto
        {
            SubmissionId = submission.SubmissionId,
            StudentId = submission.StudentExamination?.StudentId ?? Guid.Empty,
            StudentCode = submission.StudentExamination?.Student?.StudentCode ?? string.Empty,
            StudentName = submission.StudentExamination?.Student?.FullName ?? string.Empty,
            FileSubmissions = submission.FileSubmissions,
            Status = submission.Status,
            CreatedDate = submission.CreatedDate,
            UpdatedDate = submission.UpdatedDate,
            DiaryId = submission.DiaryId,
            DiaryName = submission.GradingDiary?.Name ?? string.Empty,
            ExaminationCode = submission.GradingDiary?.Content ?? string.Empty,
            PaperSetCode = submission.GradingDiary?.PaperSet?.PaperSetCode ?? string.Empty,
            LecturerId = submission.GradingDiary?.CreateById ?? Guid.Empty,
            LecturerName = submission.GradingDiary?.CreatedBy?.FullName ?? string.Empty,
            AiScore = aiScore,
            LecturerScore = lecturerScore,
            FinalScore = finalScore
        };

        return Result<AdminSubmissionItemDto>.Success(dto);
    }
}
