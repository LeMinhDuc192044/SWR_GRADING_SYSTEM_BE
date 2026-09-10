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
    public async Task<ActionResult<PagedResult<SemesterDTO>>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
        => Ok(await _service.GetPagedAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SemesterDTO>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result);
    }

    [HttpPost]
    public async Task<ActionResult<SemesterDTO>> Create(
        [FromBody] CreateSemesterRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.SemesterId }, result.Data);

        return result.ErrorCode == "SEMESTER_CODE_EXISTS"
            ? Conflict(result)
            : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SemesterDTO>> Update(
        Guid id,
        [FromBody] UpdateSemesterRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(result.Data);
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(result);
        if (result.ErrorCode == "SEMESTER_CODE_EXISTS")
            return Conflict(result);
        return BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return NoContent();
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(result);
        if (result.ErrorCode == "SEMESTER_HAS_EXAMINATIONS")
            return Conflict(result);
        return BadRequest(result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<SemesterDTO>> UpdateStatus(
        Guid id,
        [FromBody] UpdateSemesterStatusRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(result.Data);
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(result);
        return BadRequest(result);
    }
}