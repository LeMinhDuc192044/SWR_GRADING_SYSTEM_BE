using Application.Common;
using Application.DTOs.Students;

namespace Application.Interfaces;

public interface IStudentService
{
    Task<Result<StudentImportResultDto>> ImportStudentsFromExcelAsync(Stream fileStream, CancellationToken ct = default);
    byte[] GenerateStudentImportTemplate();
}

