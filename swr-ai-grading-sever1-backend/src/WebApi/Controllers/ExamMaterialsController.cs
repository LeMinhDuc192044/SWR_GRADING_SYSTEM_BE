using Application.Common;
using Application.DTOs.ExamMaterials;
using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/exam-materials")]
public sealed class ExamMaterialsController : ControllerBase
{
    private readonly IExamMaterialService _service;

    public ExamMaterialsController(IExamMaterialService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ExamMaterialMetadataDTO>>> GetMetadata(
        [FromQuery] PagedRequest request,
        CancellationToken ct) =>
        Ok(await _service.GetPagedAsync(request, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExamMaterialDetailDTO>> GetDetail(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.IsSuccess ? Ok(result.Data) : NotFound(result);
    }

    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> GetContent(Guid id, [FromQuery] ExamMaterialFileType fileType, CancellationToken ct)
    {
        var result = await _service.DownloadAsync(id, fileType, ct);
        if (!result.IsSuccess) return NotFound(result);
        return File(result.Data!.Content, result.Data.ContentType, result.Data.FileName);
    }

    [HttpPost]
    [Authorize]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<ExamMaterialMetadataDTO>>> Create(
        [FromForm] CreateExamMaterialRequest request,
        CancellationToken ct)
    {
        var uploads = BuildUploads(request.Question, request.AnswerRubric, request.AnswerTemplate);
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var result = await _service.CreateAsync(request.ExaminationId, uploads, userId, ct);

        if (result.IsSuccess) return Ok(result.Data);
        if (result.ErrorCode == "EXAMINATION_NOT_FOUND") return NotFound(result);
        if (result.ErrorCode == "LECTURER_REQUIRED") return Forbid();
        return BadRequest(result);
    }

    [HttpPost("batch")]
    [Authorize]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateMany(
       [FromForm] CreateExamMaterialsRequest request,
       CancellationToken ct)
    {
       var materials = request.Materials
           .Select(item => (IReadOnlyList<MaterialFileUpload>)BuildUploads(item.Question, item.AnswerRubric, item.AnswerTemplate))
           .ToList();

         if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
         var result = await _service.CreateManyAsync(request.ExaminationId, materials, userId, ct);

       if (result.IsSuccess) return Ok(result.Data);
       if (result.ErrorCode == "EXAMINATION_NOT_FOUND") return NotFound(result);
         if (result.ErrorCode == "LECTURER_REQUIRED") return Forbid();
       return BadRequest(result);
    }

    // [HttpPost("batch")]
    // [RequestSizeLimit(524_288_000)]
    // [Consumes("multipart/form-data")]
    // public async Task<ActionResult<IReadOnlyList<ExamMaterialMetadataDTO>>> CreateMany(
    // [FromForm] Guid examinationId,
    // [FromForm] List<IFormFile> questions,
    // [FromForm] List<IFormFile> answerRubrics,
    // [FromForm] List<IFormFile> answerTemplates,
    // CancellationToken ct)
    // {
    //     if (questions.Count != answerRubrics.Count ||
    //         questions.Count != answerTemplates.Count)
    //     {
    //         return BadRequest(
    //             "Each question must have exactly one rubric and one answer template.");
    //     }

    //     var materials = new List<IReadOnlyList<MaterialFileUpload>>();

    //     for (var i = 0; i < questions.Count; i++)
    //     {
    //         materials.Add(BuildUploads(
    //             questions[i],
    //             answerRubrics[i],
    //             answerTemplates[i]));
    //     }

    //     var result = await _service.CreateManyAsync(
    //         examinationId,
    //         materials,
    //         ct);

    //     if (result.IsSuccess)
    //         return Ok(result.Data);

    //     if (result.ErrorCode == "EXAMINATION_NOT_FOUND")
    //         return NotFound(result);

    //     return BadRequest(result);
    // }

    [HttpPost("{id:guid}/files")]
    [RequestSizeLimit(524_288_000)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ExamMaterialDetailDTO>> AddFiles(
        Guid id,
        [FromForm] AddExamMaterialFilesRequest request,
        CancellationToken ct)
    {
        var result = await _service.AddFilesAsync(
            id,
            BuildUploads(request.Question, request.AnswerRubric, request.AnswerTemplate),
            ct);

        if (result.IsSuccess) return Ok(result.Data);
        return result.ErrorCode == "EXAM_MATERIAL_NOT_FOUND" ? NotFound(result) : BadRequest(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExamMaterialDetailDTO>> Update(
        Guid id,
        [FromBody] UpdateExamMaterialRequest request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.IsSuccess
            ? Ok(result.Data)
            : result.ErrorCode == "EXAM_MATERIAL_NOT_FOUND" ? NotFound(result) : BadRequest(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.IsSuccess
            ? NoContent()
            : result.ErrorCode == "EXAM_MATERIAL_NOT_FOUND" ? NotFound(result) : BadRequest(result);
    }

    private static List<MaterialFileUpload> BuildUploads(
        IFormFile? question, IFormFile? answerRubric, IFormFile? answerTemplate) =>
        new[]
        {
            ToUpload(question, ExamMaterialFileType.Question),
            ToUpload(answerRubric, ExamMaterialFileType.AnswerRubric),
            ToUpload(answerTemplate, ExamMaterialFileType.AnswerTemplate)
        }
        .Where(upload => upload is not null)
        .Select(upload => upload!)
        .ToList();

    private static MaterialFileUpload? ToUpload(IFormFile? file, ExamMaterialFileType type) =>
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

    private bool TryGetCurrentUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}