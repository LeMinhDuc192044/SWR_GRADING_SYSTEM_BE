using AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.CreateSemester;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.DeleteSemester;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemester;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Commands.UpdateSemesterStatus;
using AI_Assisted_SWR_Grading_System.Application.Semesters.DTOs;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterByCode;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesterById;
using AI_Assisted_SWR_Grading_System.Application.Semesters.Queries.GetSemesters;
using AI_Assisted_SWR_Grading_System.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AI_Assisted_SWR_Grading_System.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SemestersController : ControllerBase
{
    private readonly ISender _mediator;

    public SemestersController(ISender mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<ActionResult<List<SemesterDTO>>> GetAll([FromQuery] SemesterStatus? status)
    {
        var result = await _mediator.Send(new GetSemestersQuery { Status = status });
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SemesterDTO>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetSemesterByIdQuery(id));
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SemesterDTO>> Create([FromBody] CreateSemesterCommand command)
    {
        var result = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = result.SemesterId }, result);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSemesterStatusCommand command)
    {
        if (id != command.SemesterId)
            return BadRequest("Route id and body id do not match.");

        await _mediator.Send(command);
        return NoContent();
    }

    [HttpGet("by-code/{code}")]
    public async Task<ActionResult<SemesterDTO>> GetByCode(string code)
    {
        var result = await _mediator.Send(new GetSemesterByCodeQuery(code));
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSemesterCommand command)
    {
        if (id != command.SemesterId)
            return BadRequest("Route id and body id do not match.");

        await _mediator.Send(command);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteSemesterCommand(id));
        return NoContent();
    }
}