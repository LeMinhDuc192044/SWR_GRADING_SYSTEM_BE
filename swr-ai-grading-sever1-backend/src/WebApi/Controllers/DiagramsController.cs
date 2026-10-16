using Application.Common;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/diagrams")]
[Authorize(Roles = "Lecturer,Admin")]
public class DiagramsController : ControllerBase
{
    private readonly IDiagramService _diagramService;
    private readonly IDiagramElementService _elementService;

    public DiagramsController(
        IDiagramService diagramService,
        IDiagramElementService elementService)
    {
        _diagramService = diagramService;
        _elementService = elementService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedRequest request,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _diagramService.GetPagedAsync(request, search, ct);
        return Ok(ApiResponse.Success(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _diagramService.GetByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiagramRequest request,
        CancellationToken ct)
    {
        var result = await _diagramService.CreateAsync(request, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.DiagramId }, ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "DIAGRAM_NAME_EXISTS" or "DUPLICATE_ELEMENT_CODE" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateDiagramRequest request,
        CancellationToken ct)
    {
        var result = await _diagramService.UpdateAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "DIAGRAM_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "DIAGRAM_NAME_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _diagramService.DeleteAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));

        return result.ErrorCode switch
        {
            "DIAGRAM_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "DIAGRAM_IN_USE" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }

    [HttpGet("{diagramId:guid}/elements")]
    public async Task<IActionResult> GetElements(Guid diagramId, CancellationToken ct)
    {
        var elements = await _elementService.GetByDiagramIdAsync(diagramId, ct);
        return Ok(ApiResponse.Success(elements));
    }

    [HttpPost("{diagramId:guid}/elements")]
    public async Task<IActionResult> CreateElement(
        Guid diagramId,
        [FromBody] CreateDiagramElementRequest request,
        CancellationToken ct)
    {
        request.DiagramId = diagramId;
        var result = await _elementService.CreateAsync(request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "DIAGRAM_NOT_FOUND" or "REFERENCE_ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!)),
            "ELEMENT_CODE_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!))
        };
    }
}
