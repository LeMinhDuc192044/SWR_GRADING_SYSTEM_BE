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
        [FromBody] CreateExaminationRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode is "SEMESTER_NOT_FOUND" or "PAPER_SET_NOT_FOUND" or "PAPER_SET_SEMESTER_MISMATCH")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateExaminationRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode is "EXAMINATION_NOT_FOUND" or "SEMESTER_NOT_FOUND" or "PAPER_SET_NOT_FOUND" or "PAPER_SET_SEMESTER_MISMATCH")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));
        if (result.ErrorCode == "EXAMINATION_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "EXAMINATION_HAS_MATERIALS")
            return Conflict(ApiResponse.Failure(409, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }
}