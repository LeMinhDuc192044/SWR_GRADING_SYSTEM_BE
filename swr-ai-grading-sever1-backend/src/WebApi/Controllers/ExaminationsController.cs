using Application.Common;
using Application.DTOs.Examinations;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/examinations")]
public class ExaminationsController : ControllerBase
{
    private readonly IExaminationService _service;

    public ExaminationsController(IExaminationService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExaminationDTO>>> GetAll(
        [FromQuery] PagedRequest request,
        CancellationToken ct)
        => Ok(await _service.GetPagedAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExaminationDTO>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result);
    }

    [HttpPost]
    public async Task<ActionResult<ExaminationDTO>> Create(
        [FromBody] CreateExaminationRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.ExaminationId }, result.Data);
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(result);
        return BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExaminationDTO>> Update(
        Guid id,
        [FromBody] UpdateExaminationRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(result.Data);
        if (result.ErrorCode is "EXAMINATION_NOT_FOUND" or "SEMESTER_NOT_FOUND")
            return NotFound(result);
        return BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return NoContent();
        if (result.ErrorCode == "EXAMINATION_NOT_FOUND")
            return NotFound(result);
        if (result.ErrorCode == "EXAMINATION_HAS_MATERIALS")
            return Conflict(result);
        return BadRequest(result);
    }
}