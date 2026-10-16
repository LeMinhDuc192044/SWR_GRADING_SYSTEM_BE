using Application.Common;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/diagram-elements")]
[Authorize(Roles = "Lecturer,Admin")]
public class DiagramElementsController : ControllerBase
{
    private readonly IDiagramElementService _service;

    public DiagramElementsController(IDiagramElementService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        [FromQuery] Guid? diagramId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(request, diagramId, search, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiagramElementRequest request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.ElementId }, ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "DIAGRAM_NOT_FOUND" or "REFERENCE_ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "ELEMENT_CODE_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDiagramElementRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "ELEMENT_NOT_FOUND" or "REFERENCE_ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "ELEMENT_CODE_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));

        return result.ErrorCode switch
        {
            "ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "ELEMENT_HAS_REFERENCES" or "ELEMENT_IN_USE" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }
}
