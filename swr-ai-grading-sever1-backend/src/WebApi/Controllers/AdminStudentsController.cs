using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller quản lý tài khoản Sinh viên và Import từ Excel dành cho Admin.
/// </summary>
[ApiController]
[Route("api/admin/students")]
[Authorize(Roles = "Admin")]
public sealed class AdminStudentsController : ControllerBase
{
    private readonly IStudentService _studentService;

    public AdminStudentsController(IStudentService studentService)
    {
        _studentService = studentService;
    }

    /// <summary>
    /// POST /api/admin/students/import-excel
    /// Upload file Excel chứa danh sách sinh viên để tạo tài khoản tự động và gửi thông tin về email sinh viên.
    /// </summary>
    [HttpPost("import-excel")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportExcel(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn file Excel để import.", "FILE_REQUIRED"));
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
        {
            return BadRequest(ApiResponse.Failure(400, "Chỉ chấp nhận file định dạng Excel (.xlsx hoặc .xls).", "INVALID_FILE_FORMAT"));
        }

        // Giới hạn dung lượng tối đa 10MB
        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse.Failure(400, "Dung lượng file không được vượt quá 10MB.", "FILE_TOO_LARGE"));
        }

        using var stream = file.OpenReadStream();
        var result = await _studentService.ImportStudentsFromExcelAsync(stream, ct);

        if (!result.IsSuccess)
        {
            return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
        }

        return Ok(ApiResponse.Success(result.Data, 200, "Quá trình import sinh viên đã hoàn thành."));
    }

    /// <summary>
    /// GET /api/admin/students/import-template
    /// Tải file Excel mẫu để import danh sách sinh viên.
    /// </summary>
    [HttpGet("import-template")]
    public IActionResult DownloadTemplate()
    {
        var content = _studentService.GenerateStudentImportTemplate();
        var fileName = "Student_Import_Template.xlsx";
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

