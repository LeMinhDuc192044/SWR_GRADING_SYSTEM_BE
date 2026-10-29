using Application.Common;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

using Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class GradingDiaryService : IGradingDiaryService
{
    private readonly IGradingDiaryRepository _diaryRepository;
    private readonly IPaperSetRepository _paperSetRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _dbContext;

    public GradingDiaryService(
        IGradingDiaryRepository diaryRepository,
        IPaperSetRepository paperSetRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IApplicationDbContext dbContext)
    {
        _diaryRepository = diaryRepository;
        _paperSetRepository = paperSetRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dbContext = dbContext;
    }

    public async Task<Result<GradingDiaryResponseDTO>> CreateAsync(
        CreateGradingDiaryRequest request,
        Guid currentUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<GradingDiaryResponseDTO>.Failure("Tên sổ chấm không được để trống.", "NAME_REQUIRED");
        }

        var lecturer = await _userRepository.GetLecturerByIdAsync(currentUserId, ct);
        if (lecturer is null)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Chỉ giảng viên mới có quyền tạo sổ chấm.", "LECTURER_REQUIRED");
        }

        var paperSet = await _paperSetRepository.GetByIdAsync(request.PaperSetId, ct);
        if (paperSet is null)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Đề thi không tồn tại.", "PAPER_SET_NOT_FOUND");
        }

        var alreadyHasDiary = await _diaryRepository.ExistsByPaperSetIdAsync(request.PaperSetId, ct);
        if (alreadyHasDiary)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Đề thi này đã được tạo sổ chấm.", "PAPER_SET_ALREADY_HAS_DIARY");
        }

        var content = string.IsNullOrWhiteSpace(request.Content) ? paperSet.PaperSetCode : request.Content.Trim();

        if (content.Length > 21)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Nội dung (Content) không được vượt quá 21 ký tự.", "CONTENT_TOO_LONG");
        }

        var diary = new GradingDiary
        {
            GradingDiaryId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Content = content,
            CreateById = currentUserId,
            PaperSetId = request.PaperSetId
        };

        await _diaryRepository.AddAsync(diary, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var response = new GradingDiaryResponseDTO
        {
            GradingDiaryId = diary.GradingDiaryId,
            Name = diary.Name,
            Content = diary.Content,
            PaperSetId = paperSet.PaperSetId,
            PaperSetCode = paperSet.PaperSetCode,
            CreateById = lecturer.Id,
            LecturerName = lecturer.FullName,
            SubmissionsCount = 0
        };

        return Result<GradingDiaryResponseDTO>.Success(response);
    }

    public async Task<PagedResult<GradingDiaryResponseDTO>> GetPagedAsync(
        PagedRequest request,
        Guid? lecturerId = null,
        CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : request.PageSize;
        var skip = (page - 1) * pageSize;

        var items = await _diaryRepository.GetPagedAsync(skip, pageSize, lecturerId, ct);
        var total = await _diaryRepository.CountByLecturerAsync(lecturerId, ct);

        var dtos = items.Select(d => new GradingDiaryResponseDTO
        {
            GradingDiaryId = d.GradingDiaryId,
            Name = d.Name,
            Content = d.Content,
            PaperSetId = d.PaperSetId,
            PaperSetCode = d.PaperSet?.PaperSetCode ?? string.Empty,
            CreateById = d.CreateById,
            LecturerName = d.CreatedBy?.FullName ?? string.Empty,
            SubmissionsCount = d.Submissions?.Count ?? 0
        }).ToList();

        return new PagedResult<GradingDiaryResponseDTO>
        {
            Items = dtos,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<GradingDiaryDetailDTO>> GetByIdAsync(
        Guid id,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var diary = await _diaryRepository.GetDetailByIdAsync(id, ct);
        if (diary is null)
        {
            return Result<GradingDiaryDetailDTO>.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result<GradingDiaryDetailDTO>.Failure("Bạn không có quyền truy cập sổ chấm này.", "FORBIDDEN");
        }

        var submissions = (diary.Submissions ?? Enumerable.Empty<StudentSubmission>())
            .OrderByDescending(s => s.CreatedDate)
            .Select(s =>
            {
                var aiEvaluation = GetLatestEvaluation(s, EvaluationType.AI);
                var lecturerEvaluation = GetLatestEvaluation(s, EvaluationType.Lecturer);
                return new SubmissionItemDTO
                {
                    SubmissionId = s.SubmissionId,
                    SubmissionFile = System.IO.Path.GetFileName(s.FileSubmissions),
                    AiScore = aiEvaluation?.TotalScore,
                    LecturerScore = lecturerEvaluation?.TotalScore,
                    Status = s.Status,
                    Comment = lecturerEvaluation?.GeneralFeedback ?? string.Empty,
                    CreatedDate = s.CreatedDate
                };
            })
            .ToList();

        var detail = new GradingDiaryDetailDTO
        {
            GradingDiaryId = diary.GradingDiaryId,
            Name = diary.Name,
            Content = diary.Content,
            PaperSetId = diary.PaperSetId,
            PaperSetCode = diary.PaperSet?.PaperSetCode ?? string.Empty,
            FileAnswerRubric = diary.PaperSet?.FileAnswerRubric,
            CreateById = diary.CreateById,
            LecturerName = diary.CreatedBy?.FullName ?? string.Empty,
            Submissions = submissions
        };

        return Result<GradingDiaryDetailDTO>.Success(detail);
    }

    public async Task<Result<GradingDiaryResponseDTO>> UpdateAsync(
        Guid id,
        UpdateGradingDiaryRequest request,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var diary = await _diaryRepository.GetDetailByIdAsync(id, ct);
        if (diary is null)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result<GradingDiaryResponseDTO>.Failure("Bạn không có quyền cập nhật sổ chấm này.", "FORBIDDEN");
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            diary.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            var trimmedContent = request.Content.Trim();
            if (trimmedContent.Length > 21)
            {
                return Result<GradingDiaryResponseDTO>.Failure("Nội dung (Content) không được vượt quá 21 ký tự.", "CONTENT_TOO_LONG");
            }
            diary.Content = trimmedContent;
        }

        _diaryRepository.Update(diary);
        await _unitOfWork.SaveChangesAsync(ct);

        var response = new GradingDiaryResponseDTO
        {
            GradingDiaryId = diary.GradingDiaryId,
            Name = diary.Name,
            Content = diary.Content,
            PaperSetId = diary.PaperSetId,
            PaperSetCode = diary.PaperSet?.PaperSetCode ?? string.Empty,
            CreateById = diary.CreateById,
            LecturerName = diary.CreatedBy?.FullName ?? string.Empty,
            SubmissionsCount = diary.Submissions?.Count ?? 0
        };

        return Result<GradingDiaryResponseDTO>.Success(response);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        Guid currentUserId,
        bool isElevatedRole,
        CancellationToken ct = default)
    {
        var diary = await _diaryRepository.GetDetailByIdAsync(id, ct);
        if (diary is null)
        {
            return Result.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result.Failure("Bạn không có quyền xóa sổ chấm này.", "FORBIDDEN");
        }

        if (diary.Submissions?.Any() == true)
        {
            return Result.Failure("Không thể xóa sổ chấm đã chứa bài nộp.", "DIARY_HAS_SUBMISSIONS");
        }

        var paperSet = await _paperSetRepository.GetByIdAsync(diary.PaperSetId, ct);
        if (paperSet is not null)
        {
            paperSet.Status = PaperSetStatus.Ready;
            _paperSetRepository.Update(paperSet);
        }

        _diaryRepository.Remove(diary);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// Thống kê tiến độ chấm bài trong sổ chấm (Số bài, % đã chốt điểm).
    /// </summary>
    public async Task<Result<GradingDiaryProgressDto>> GetProgressAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var diary = await _dbContext.GradingDiaries
            .AsNoTracking()
            .Include(d => d.PaperSet)
            .FirstOrDefaultAsync(d => d.GradingDiaryId == id, ct);

        if (diary is null)
        {
            return Result<GradingDiaryProgressDto>.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result<GradingDiaryProgressDto>.Failure("Bạn không có quyền truy cập sổ chấm này.", "FORBIDDEN");
        }

        var submissions = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Where(s => s.DiaryId == id)
            .ToListAsync(ct);

        var total = submissions.Count;
        var submittedCount = submissions.Count(s => s.Status == SubmissionStatus.Submitted);
        var aiGradedCount = submissions.Count(s => s.Status >= SubmissionStatus.AI_Graded);
        var reviewedCount = submissions.Count(s => s.Status >= SubmissionStatus.Lecturer_Reviewed);
        var finalizedCount = submissions.Count(s => s.Status == SubmissionStatus.Final);
        var progressPercent = total > 0 ? Math.Round((double)finalizedCount / total * 100, 2) : 0;

        return Result<GradingDiaryProgressDto>.Success(new GradingDiaryProgressDto
        {
            DiaryId = diary.GradingDiaryId,
            DiaryName = diary.Name,
            PaperSetCode = diary.PaperSet?.PaperSetCode ?? string.Empty,
            TotalSubmissions = total,
            SubmittedCount = submittedCount,
            AiGradedCount = aiGradedCount,
            LecturerReviewedCount = reviewedCount,
            FinalizedCount = finalizedCount,
            ProgressPercent = progressPercent
        });
    }

    /// <summary>
    /// So sánh độ lệch điểm giữa Giảng viên và AI trong sổ chấm.
    /// Giúp giảng viên phát hiện các bài thi có sự chênh lệch lớn để rà soát lại.
    /// </summary>
    public async Task<Result<IReadOnlyList<AiComparisonItemDto>>> CompareWithAiAsync(Guid id, Guid currentUserId, bool isElevatedRole, CancellationToken ct = default)
    {
        var diary = await _dbContext.GradingDiaries
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.GradingDiaryId == id, ct);

        if (diary is null)
        {
            return Result<IReadOnlyList<AiComparisonItemDto>>.Failure("Không tìm thấy sổ chấm.", "DIARY_NOT_FOUND");
        }

        if (!isElevatedRole && diary.CreateById != currentUserId)
        {
            return Result<IReadOnlyList<AiComparisonItemDto>>.Failure("Bạn không có quyền truy cập sổ chấm này.", "FORBIDDEN");
        }

        var submissions = await _dbContext.StudentSubmissions
            .AsNoTracking()
            .Include(s => s.StudentExamination)
                .ThenInclude(se => se.Student)
            .Include(s => s.Evaluations)
            .Where(s => s.DiaryId == id)
            .ToListAsync(ct);

        var comparisonList = submissions
            .Select(s =>
            {
                var aiEvaluation = GetLatestEvaluation(s, EvaluationType.AI);
                var lecturerEvaluation = GetLatestEvaluation(s, EvaluationType.Lecturer);
                decimal? diff = null;
                if (lecturerEvaluation?.TotalScore is decimal lecturerScore && aiEvaluation?.TotalScore is decimal aiScore)
                {
                    diff = Math.Abs(lecturerScore - aiScore);
                }

                return new AiComparisonItemDto
                {
                    SubmissionId = s.SubmissionId,
                    StudentCode = s.StudentExamination?.Student?.StudentCode ?? string.Empty,
                    StudentName = s.StudentExamination?.Student?.FullName ?? string.Empty,
                    SubmissionFile = System.IO.Path.GetFileName(s.FileSubmissions),
                    AiScore = aiEvaluation?.TotalScore,
                    LecturerScore = lecturerEvaluation?.TotalScore,
                    ScoreDifference = diff,
                    Status = s.Status,
                    Comment = lecturerEvaluation?.GeneralFeedback ?? string.Empty
                };
            })
            // Sắp xếp ưu tiên các bài có độ lệch điểm từ lớn đến bé
            .OrderByDescending(c => c.ScoreDifference ?? 0)
            .ToList();

        return Result<IReadOnlyList<AiComparisonItemDto>>.Success(comparisonList);
    }

    private static SubmissionEvaluation? GetLatestEvaluation(StudentSubmission submission, EvaluationType evaluationType)
    {
        return submission.Evaluations
            .Where(evaluation => evaluation.EvaluationType == evaluationType)
            .OrderByDescending(evaluation => evaluation.EvaluationRound)
            .ThenByDescending(evaluation => evaluation.CreatedAt)
            .FirstOrDefault();
    }
}
