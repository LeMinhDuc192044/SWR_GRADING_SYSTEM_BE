using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Application.Common.Interfaces;
using Application.DTOs.Submissions;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

[ApiController]
[Route("api/submission-parsed-answers")]
[Authorize(Roles = "Examiner,Admin")]
public sealed class SubmissionParsedAnswersController : ControllerBase
{
    private readonly ISubmissionParsedAnswerService _service;
    private readonly IImageUrlProvider _imageUrls;

    public SubmissionParsedAnswersController(
        ISubmissionParsedAnswerService service, IImageUrlProvider imageUrls)
    {
        _service = service;
        _imageUrls = imageUrls;
    }

    [HttpGet("submission/{submissionId:guid}")]
    public async Task<IActionResult> GetBySubmission(Guid submissionId, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.GetBySubmissionIdAsync(submissionId, userId, IsAdmin(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : Failure(result.ErrorCode, result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.GetByIdAsync(id, userId, IsAdmin(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : Failure(result.ErrorCode, result.Error);
    }

    /// <summary>
    /// Streams the (decrypted) image of a parsed answer. Accepts either a signed link
    /// (the URL returned in ExtractedImageUrl, usable directly in a browser/img tag)
    /// or a privileged bearer token.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetImage(
        Guid id,
        [FromQuery] long? expires,
        [FromQuery] string? signature,
        CancellationToken ct)
    {
        var hasValidSignature = expires.HasValue && _imageUrls.IsValidSignature(id, expires.Value, signature);
        var isPrivileged = User.Identity?.IsAuthenticated == true && (IsAdmin() || User.IsInRole("Examiner") || User.IsInRole("Monitor"));

        if (!hasValidSignature && !isPrivileged)
            return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));

        if (!TryGetCurrentUserId(out var userId) && !hasValidSignature)
            return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));

        var result = await _service.GetImageAsync(id, userId, hasValidSignature || IsAdmin(), ct);
        if (!result.IsSuccess) return Failure(result.ErrorCode, result.Error);

        var image = result.Data!;
        return File(image.Content, image.ContentType);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SaveSubmissionParsedAnswerRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.CreateAsync(request, userId, IsAdmin(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : Failure(result.ErrorCode, result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveSubmissionParsedAnswerRequest request, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.UpdateAsync(id, request, userId, IsAdmin(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(result.Data)) : Failure(result.ErrorCode, result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized(ApiResponse.Failure(401, "Unauthorized."));
        var result = await _service.DeleteAsync(id, userId, IsAdmin(), ct);
        return result.IsSuccess ? Ok(ApiResponse.Success(true)) : Failure(result.ErrorCode, result.Error);
    }

    private IActionResult Failure(string? code, string? message) => code switch
    {
        "PARSED_ANSWER_NOT_FOUND" or "SUBMISSION_NOT_FOUND" or "ELEMENT_NOT_FOUND" or "IMAGE_NOT_FOUND" =>
            NotFound(ApiResponse.Failure(404, message ?? "Not found.")),
        "FORBIDDEN" => StatusCode(403, ApiResponse.Failure(403, message ?? "Forbidden.")),
        _ => BadRequest(ApiResponse.Failure(400, message ?? "Invalid request."))
    };

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(id, out userId);
    }

    private bool IsAdmin() => User.IsInRole("Admin") || User.FindFirstValue("discriminator") == "Admin";
}