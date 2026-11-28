using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/diagram-elements")]
[Authorize(Roles = "Examiner,Admin,Manager")]
public class DiagramElementsController : ControllerBase
{
    private readonly IDiagramElementService _service;
    private readonly IImageUrlProvider _imageUrls;

    public DiagramElementsController(IDiagramElementService service, IImageUrlProvider imageUrls)
    {
        _service = service;
        _imageUrls = imageUrls;
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
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }

    /// <summary>Creates an element from a JSON body, without an image.</summary>
    /// <remarks>
    /// Kept for JSON clients, but hidden from Swagger: it shares path and method with the
    /// multipart action, which Swashbuckle cannot document twice.
    /// </remarks>
    [HttpPost]
    [Consumes("application/json")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Create(
        [FromBody] CreateDiagramElementRequest request,
        CancellationToken ct) =>
        await CreateElementAsync(request, null, ct);

    /// <summary>
    /// Creates an element from a multipart form. The optional <c>image</c> part is stored in
    /// Supabase and its path fills <c>fileTemplateUrl</c>, so the client never sends that field.
    /// </summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    // Slightly above the service's 5 MB limit so an oversized image still gets IMAGE_TOO_LARGE
    // instead of the server rejecting the whole multipart request.
    [RequestSizeLimit(6_291_456)]
    public async Task<IActionResult> CreateWithImage(
        [FromForm] CreateDiagramElementRequest request,
        IFormFile? image,
        CancellationToken ct)
    {
        if (image is null)
            return await CreateElementAsync(request, null, ct);

        await using var stream = image.OpenReadStream();
        return await CreateElementAsync(request, new DiagramElementImageUpload
        {
            Content = stream,
            FileName = image.FileName,
            ContentType = image.ContentType ?? "application/octet-stream",
            Length = image.Length
        }, ct);
    }

    private async Task<IActionResult> CreateElementAsync(
        CreateDiagramElementRequest request,
        DiagramElementImageUpload? image,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, image, ct);
        if (result.IsSuccess)
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.ElementId }, ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "DIAGRAM_NOT_FOUND" or "REFERENCE_ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            "ELEMENT_CODE_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
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
            "ELEMENT_NOT_FOUND" or "REFERENCE_ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            "ELEMENT_CODE_EXISTS" => Conflict(ApiResponse.Failure(409, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
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
            "ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            "ELEMENT_HAS_REFERENCES" or "ELEMENT_IN_USE" => Conflict(ApiResponse.Failure(409, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
        };
    }

    /// <summary>Uploads (or replaces) the image of a diagram element in Supabase storage.</summary>
    [HttpPost("{id:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> UploadImage(Guid id, IFormFile? image, CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return BadRequest(ApiResponse.Failure(400, "An image file is required.", "FILE_REQUIRED"));

        await using var stream = image.OpenReadStream();
        var result = await _service.UploadImageAsync(id, new DiagramElementImageUpload
        {
            Content = stream,
            FileName = image.FileName,
            ContentType = image.ContentType ?? "application/octet-stream",
            Length = image.Length
        }, ct);

        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "ELEMENT_NOT_FOUND" => NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
        };
    }

    /// <summary>Streams the (decrypted) image of a diagram element using a signed link or a bearer token.</summary>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(
        Guid id,
        [FromQuery] long? expires,
        [FromQuery] string? signature,
        CancellationToken ct)
    {
        var hasValidSignature = expires.HasValue && _imageUrls.IsValidDiagramElementSignature(id, expires.Value, signature);
        var isPrivileged = User.Identity?.IsAuthenticated == true &&
                           (User.IsInRole("Examiner") || User.IsInRole("Admin"));

        if (!hasValidSignature && !isPrivileged)
            return Unauthorized(ApiResponse.Failure(401, "Unauthorized.", "UNAUTHORIZED"));

        var result = await _service.GetImageAsync(id, ct);
        if (!result.IsSuccess)
            return NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));

        return File(result.Data!.Content, result.Data.ContentType);
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteImageAsync(id, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(null));

        return result.ErrorCode == "IMAGE_NOT_FOUND"
            ? NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode))
            : BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode));
    }

    /// <summary>Connects this element to another element of the same diagram.</summary>
    [HttpPost("{id:guid}/connections")]
    public async Task<IActionResult> Connect(Guid id, [FromBody] ConnectDiagramElementRequest request, CancellationToken ct)
    {
        var result = await _service.ConnectAsync(id, request, ct);
        if (result.IsSuccess)
            return Ok(ApiResponse.Success(result.Data));

        return result.ErrorCode switch
        {
            "ELEMENT_NOT_FOUND" or "TARGET_ELEMENT_NOT_FOUND" =>
                NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode)),
            _ => BadRequest(ApiResponse.Failure(400, result.Error!, result.ErrorCode))
        };
    }

    /// <summary>Removes the connection of this element to another element.</summary>
    [HttpDelete("{id:guid}/connections")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken ct)
    {
        var result = await _service.DisconnectAsync(id, ct);
        return result.IsSuccess
            ? Ok(ApiResponse.Success(result.Data))
            : NotFound(ApiResponse.Failure(404, result.Error!, result.ErrorCode));
    }
}
