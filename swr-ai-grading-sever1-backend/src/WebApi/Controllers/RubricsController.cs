using Application.Common;
using Application.DTOs.Rubrics;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/rubrics")]
[Authorize(Roles = "Lecturer,Admin")]
public sealed class RubricsController : ControllerBase
{
    private readonly IPaperSetService _service;

    public RubricsController(IPaperSetService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetRubricByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpGet]
    public async Task<IActionResult> GetByPaperSetId([FromQuery] Guid? paperSetId, CancellationToken ct)
    {
        if (!paperSetId.HasValue)
            return BadRequest(ApiResponse.Failure(400, "paperSetId query parameter is required."));

        var result = await _service.GetRubricsByPaperSetIdAsync(paperSetId.Value, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!));
    }
}
