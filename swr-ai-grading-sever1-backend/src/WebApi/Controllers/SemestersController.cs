using Application.Common;
using Application.DTOs.Semesters;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/semesters")]
// [Authorize(Roles = "Examiner,Admin")]
public class SemestersController : ControllerBase
{
    private readonly ISemesterService _service;

    public SemestersController(ISemesterService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
        => Ok(ApiResponse.Success(await _service.GetPagedAsync(request, ct)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpPost]
    // [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSemesterRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode == "SEMESTER_CODE_EXISTS"
            ? Conflict(ApiResponse.Failure(409, result.Error!))
            : BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSemesterRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "SEMESTER_CODE_EXISTS")
            return Conflict(ApiResponse.Failure(409, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "SEMESTER_HAS_EXAMINATIONS")
            return Conflict(ApiResponse.Failure(409, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateSemesterStatusRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPost("{id:guid}/students")]
    public async Task<IActionResult> AddStudents(
        Guid id,
        [FromBody] List<AddStudentToSemesterRequest> requests,
        CancellationToken ct)
    {
        var result = await _service.AddStudentsAsync(id, requests, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    [HttpGet("students/import-template")]
    public IActionResult DownloadStudentImportTemplate()
    {
        var content = _service.GenerateStudentImportTemplate();
        return File(
            content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Semester_Student_Import_Template.xlsx");
    }

    [HttpPost("{id:guid}/students/import-excel")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportStudents(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse.Failure(400, "Please select an Excel file."));

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx" && extension != ".xls")
            return BadRequest(ApiResponse.Failure(400, "Only .xlsx or .xls files are supported."));

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(ApiResponse.Failure(400, "The file size must not exceed 10 MB."));

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var result = await _service.ImportStudentsFromExcelAsync(id, stream, ct);
        if (!result.IsSuccess)
            return result.ErrorCode == "SEMESTER_NOT_FOUND"
                ? NotFound(ApiResponse.Failure(404, result.Error!))
                : BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));

        return Ok(ApiResponse.Success(result.Data));
    }
}