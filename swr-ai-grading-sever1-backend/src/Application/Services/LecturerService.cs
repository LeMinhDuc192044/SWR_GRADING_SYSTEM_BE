using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Lecturers;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public sealed class LecturerService : ILecturerService
{
    private readonly IApplicationDbContext _dbContext;

    public LecturerService(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ giảng viên trong hệ thống.
    /// </summary>
    public async Task<Result<IReadOnlyList<LecturerSummaryDto>>> GetLecturersAsync(CancellationToken ct = default)
    {
        var lecturers = await _dbContext.Lecturers
            .AsNoTracking()
            .Select(l => new LecturerSummaryDto
            {
                Id = l.Id,
                LecturerCode = l.LecturerCode,
                FullName = l.FullName,
                Email = l.Email,
                Subject = l.Subject,
                IsActive = l.IsActive,
                PaperSetsCount = _dbContext.PaperSets.Count(p => p.CreateById == l.Id && !p.IsDeleted),
                GradingDiariesCount = _dbContext.GradingDiaries.Count(d => d.CreateById == l.Id)
            })
            .OrderBy(l => l.LecturerCode)
            .ToListAsync(ct);

        return Result<IReadOnlyList<LecturerSummaryDto>>.Success(lecturers);
    }

    /// <summary>
    /// Cập nhật thông tin cơ bản của giảng viên (Họ tên, email, bộ môn).
    /// </summary>
    public async Task<Result<LecturerSummaryDto>> UpdateLecturerAsync(Guid id, UpdateLecturerRequest request, CancellationToken ct = default)
    {
        var lecturer = await _dbContext.Lecturers
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lecturer is null)
        {
            return Result<LecturerSummaryDto>.Failure("Không tìm thấy giảng viên.", "LECTURER_NOT_FOUND");
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return Result<LecturerSummaryDto>.Failure("Họ và tên không được để trống.", "INVALID_NAME");
        }

        lecturer.FullName = request.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            lecturer.Email = request.Email.Trim();
        }
        if (!string.IsNullOrWhiteSpace(request.Subject))
        {
            lecturer.Subject = request.Subject.Trim();
        }

        await _dbContext.SaveChangesAsync(ct);

        var dto = new LecturerSummaryDto
        {
            Id = lecturer.Id,
            LecturerCode = lecturer.LecturerCode,
            FullName = lecturer.FullName,
            Email = lecturer.Email,
            Subject = lecturer.Subject,
            IsActive = lecturer.IsActive,
            PaperSetsCount = await _dbContext.PaperSets.CountAsync(p => p.CreateById == lecturer.Id && !p.IsDeleted, ct),
            GradingDiariesCount = await _dbContext.GradingDiaries.CountAsync(d => d.CreateById == lecturer.Id, ct)
        };

        return Result<LecturerSummaryDto>.Success(dto);
    }

    /// <summary>
    /// Bật/Tắt trạng thái hoạt động (kích hoạt hoặc tạm khóa tài khoản giảng viên).
    /// </summary>
    public async Task<Result<bool>> ToggleStatusAsync(Guid id, CancellationToken ct = default)
    {
        var lecturer = await _dbContext.Lecturers
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (lecturer is null)
        {
            return Result<bool>.Failure("Không tìm thấy giảng viên.", "LECTURER_NOT_FOUND");
        }

        // Đảo trạng thái active/inactive
        lecturer.IsActive = !lecturer.IsActive;
        await _dbContext.SaveChangesAsync(ct);

        return Result<bool>.Success(lecturer.IsActive);
    }
}
