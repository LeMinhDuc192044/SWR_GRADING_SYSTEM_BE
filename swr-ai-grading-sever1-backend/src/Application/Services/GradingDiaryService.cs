using Application.Common;
using Application.DTOs.GradingDiaries;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Services;

public sealed class GradingDiaryService : IGradingDiaryService
{
    private readonly IGradingDiaryRepository _diaryRepository;
    private readonly IPaperSetRepository _paperSetRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public GradingDiaryService(
        IGradingDiaryRepository diaryRepository,
        IPaperSetRepository paperSetRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _diaryRepository = diaryRepository;
        _paperSetRepository = paperSetRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
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

        var diary = new GradingDiary
        {
            GradingDiaryId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Content = string.IsNullOrWhiteSpace(request.Content) ? paperSet.PaperSetCode : request.Content.Trim(),
            CreateById = currentUserId,
            PaperSetId = request.PaperSetId
        };

        paperSet.Status = PaperSetStatus.Used;
        _paperSetRepository.Update(paperSet);

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

        var submissions = (diary.Submissions ?? Enumerable.Empty<Submission>())
            .OrderByDescending(s => s.CreatedDate)
            .Select(s => new SubmissionItemDTO
            {
                SubmissionId = s.SubmissionId,
                SubmissionFile = s.SubmissionFile,
                AiScore = s.AiScore,
                LecturerScore = s.LecturerScore,
                Status = s.Status,
                Comment = s.Comment ?? string.Empty,
                CreatedDate = s.CreatedDate
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
            diary.Content = request.Content.Trim();
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
}
