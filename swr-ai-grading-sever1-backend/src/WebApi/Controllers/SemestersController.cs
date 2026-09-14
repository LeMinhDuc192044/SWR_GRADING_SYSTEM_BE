using Application.Common;
using Application.DTOs.Semesters;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/semesters")]
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
}