using Application.Common;
using Application.DTOs.Students;

namespace Application.Interfaces;

public interface IStudentService
{
    Task<Result<StudentImportResultDto>> ImportStudentsFromExcelAsync(Stream fileStream, CancellationToken ct = default);
    byte[] GenerateStudentImportTemplate();

    /// <summary>
    /// Lấy danh sách sinh viên đầy đủ thông tin người dùng, có phân trang và bộ lọc theo học kỳ, kỳ thi, ca thi.
    /// </summary>
    Task<Result<PagedResult<StudentResponseDto>>> GetPagedStudentsAsync(StudentFilterRequest request, CancellationToken ct = default);
}
