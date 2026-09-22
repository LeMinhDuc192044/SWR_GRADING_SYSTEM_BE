using System.Security.Claims;
using Application.Common;
using Application.DTOs.PaperSets;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/paper-sets")]
public sealed class PaperSetsController : ControllerBase
{
    private readonly IPaperSetService _service;

    public PaperSetsController(IPaperSetService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetMetadata(
        [FromQuery] PagedRequest request,
        CancellationToken ct) =>
        Ok(ApiResponse.Success(await _service.GetPagedAsync(request, ct)));

    [HttpGet("lecturer/{lecturerId:guid}")]
    public async Task<IActionResult> GetLecturerById(
        Guid lecturerId,
        [FromQuery] PagedRequest request,
        CancellationToken ct) =>
        Ok(ApiResponse.Success(await _service.GetLecturerByIdAsync(lecturerId, request, ct)));

    [HttpPost("preview-questions")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> PreviewQuestions(IFormFile question, CancellationToken ct)
    {
        if (question is null || question.Length == 0)
            return BadRequest(ApiResponse.Failure(400, "A question file is required."));

        var upload = ToUpload(question, PaperSetFileType.Question)!;
        var result = await _service.PreviewQuestionsAsync(upload, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : NotFound(ApiResponse.Failure(404, result.Error!));
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, CancellationToken ct)
    {
        var result = await _service.DownloadAsync(id, ct);
        if (!result.IsSuccess) return NotFound(ApiResponse.Failure(404, result.Error!));
        return File(result.Data!.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create(
        [FromForm] CreatePaperSetRequest request,
        CancellationToken ct)
    {
        var uploads = BuildUploads(request.Question, request.AnswerRubric, request.AnswerTemplate);
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.CreateAsync(request.SemesterId, request.Description, request.Questions.Select(ToQuestionInput).ToList(), uploads, userId, ct);

        if (result.IsSuccess) return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND") return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "LECTURER_REQUIRED") return StatusCode(403, ApiResponse.Failure(403, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPost("batch")]
    [Authorize]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> CreateMany(
       [FromForm] CreatePaperSetsRequest request,
       CancellationToken ct)
    {
       var materials = request.Materials
           .Select(item => new CreatePaperSetInput
           {
               Description = item.Description,
               Questions = item.Questions.Select(ToQuestionInput).ToList(),
               Files = BuildUploads(item.Question, item.AnswerRubric, item.AnswerTemplate)
           })
           .ToList();

        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
            var result = await _service.CreateManyAsync(request.SemesterId, materials, userId, ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));
        if (result.ErrorCode == "SEMESTER_NOT_FOUND")
            return NotFound(ApiResponse.Failure(404, result.Error!));
        if (result.ErrorCode == "LECTURER_REQUIRED")
            return StatusCode(403, ApiResponse.Failure(403, result.Error!));
        return BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    

    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AddFiles(
        Guid id,
        [FromForm] AddPaperSetFilesRequest request,
        CancellationToken ct)
    {
        var result = await _service.AddFilesAsync(
            id,
            BuildUploads(request.Question, request.AnswerRubric, request.AnswerTemplate),
            ct);

        if (result.IsSuccess) return Ok(ApiResponse.Success(result.Data));
        return result.ErrorCode == "PAPER_SET_NOT_FOUND" ? NotFound(ApiResponse.Failure(404, result.Error!)) : BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdatePaperSetRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : result.ErrorCode == "PAPER_SET_NOT_FOUND" ? NotFound(ApiResponse.Failure(404, result.Error!)) : BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(null))
            : result.ErrorCode == "PAPER_SET_NOT_FOUND" ? NotFound(ApiResponse.Failure(404, result.Error!)) : BadRequest(ApiResponse.Failure(400, result.Error!));
    }

    private static List<MaterialFileUpload> BuildUploads(
        IFormFile? question, IFormFile? answerRubric, IFormFile? answerTemplate) =>
        new[]
        {
            ToUpload(question, PaperSetFileType.Question),
            ToUpload(answerRubric, PaperSetFileType.AnswerRubric),
            ToUpload(answerTemplate, PaperSetFileType.AnswerTemplate)
        }
        .Where(upload => upload is not null)
        .Select(upload => upload!)
        .ToList();

    private static MaterialFileUpload? ToUpload(IFormFile? file, PaperSetFileType type) =>
        file is null
            ? null
            : new MaterialFileUpload
            {
                FileType = type,
                Content = file.OpenReadStream(),
                FileName = file.FileName,
                ContentType = file.ContentType,
                Length = file.Length
            };

    private static CreateQuestionInput ToQuestionInput(CreateQuestionRequest question) => new()
    {
        Title = question.Title,
        Content = question.Content,
        Point = question.Point
    };

    private bool TryGetCurrentUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}