using Application.Common;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Quản lý tài khoản Monitor và import danh sách monitor từ Excel dành cho Admin.
/// </summary>
[ApiController]
[Route("api/admin/monitors")]
public sealed class AdminMonitorsController : ControllerBase
{
    private readonly IMonitorService _monitorService;

    public AdminMonitorsController(IMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    /// <summary>
    /// POST /api/admin/monitors/import-excel
    /// Tạo tài khoản monitor từ file Excel; mỗi tài khoản trả về mật khẩu được tạo.
    /// </summary>
    [HttpPost("import-excel")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportExcel(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Failure(400, "Vui lòng chọn file Excel để import.", "FILE_REQUIRED"));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
            return BadRequest(ApiResponse.Failure(400, "Chỉ chấp nhận file định dạng Excel (.xlsx hoặc .xls).", "INVALID_FILE_FORMAT"));

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse.Failure(400, "Dung lượng file không được vượt quá 10MB.", "FILE_TOO_LARGE"));

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var result = await _monitorService.ImportMonitorsFromExcelAsync(stream, ct);

        if (!result.IsSuccess)
            return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));

        return Ok(ApiResponse.Success(result.Data, 200, "Quá trình import monitor đã hoàn thành."));
    }

    /// <summary>
    /// GET /api/admin/monitors/import-template
    /// Tải file Excel mẫu để import danh sách monitor.
    /// </summary>
    [HttpGet("import-template")]
    public IActionResult DownloadTemplate()
    {
        var content = _monitorService.GenerateMonitorImportTemplate();
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Monitor_Import_Template.xlsx");
    }
}
