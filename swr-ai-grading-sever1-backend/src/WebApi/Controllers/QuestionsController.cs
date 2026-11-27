using Application.Common;
using Application.DTOs.Questions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/questions")]
[Authorize(Roles = "Examiner,Admin")]
public sealed class QuestionsController : ControllerBase
{
    private readonly IPaperSetService _service;

    public QuestionsController(IPaperSetService service)
    {
        _service = service;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetQuestionByIdAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    /// <summary>Assigns (or clears) the diagram used as the reference model for a question.</summary>
    [HttpPut("{id:guid}/diagram")]
    public async Task<IActionResult> AssignDiagram(
        Guid id, [FromBody] AssignQuestionDiagramRequest request, CancellationToken ct)
    {
        var result = await _service.AssignDiagramAsync(id, request.DiagramId, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "QUESTION_NOT_FOUND" or "DIAGRAM_NOT_FOUND" =>
                NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
        };
    }

    [HttpGet]
    public async Task<IActionResult> GetByPaperSetId([FromQuery] Guid? paperSetId, CancellationToken ct)
    {
        if (!paperSetId.HasValue)
            return BadRequest(ApiResponse.Failure(400, "paperSetId query parameter is required.", "PARAMETER_REQUIRED"));

        var result = await _service.GetQuestionsByPaperSetIdAsync(paperSetId.Value, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }
}
