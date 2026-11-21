using Application.Common;
using Application.DTOs.AiModels;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Common;

namespace WebApi.Controllers;

/// <summary>
/// Quản lý các AI model dùng để chấm bài. Chỉ một model được bật tại một thời điểm.
/// </summary>
[ApiController]
[Route("api/ai-models")]
[Authorize(Roles = "Admin")]
public sealed class AiModelsController : ControllerBase
{
    private readonly IAiModelService _service;

    public AiModelsController(IAiModelService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct)
    {
        var result = await _service.GetAllAsync(request, ct);
        return result.ToActionResult(this);
    }

    [HttpGet("supported")]
    public IActionResult GetSupportedModels([FromQuery] PagedRequest request)
    {
        request ??= new PagedRequest();
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, 100);

        var all = SupportedAiModels.Names.ToList();
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Ok(ApiResponse.Success(new PagedResult<string>
        {
            Items = items,
            TotalCount = all.Count,
            Page = page,
            PageSize = pageSize
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return result.ToActionResult(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAiModelRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return result.ToActionResult(this, StatusCodes.Status201Created, "Tạo AI model thành công.");
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAiModelRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return result.ToActionResult(this, successMessage: "Cập nhật AI model thành công.");
    }

    /// <summary>
    /// Bật/tắt model. Khi status = true, mọi model khác bị tắt và model này được dùng để chấm bài.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateAiModelStatusRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateStatusAsync(id, request, ct);
        return result.ToActionResult(this, successMessage: "Cập nhật trạng thái AI model thành công.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        return result.ToActionResult(this, "Xóa AI model thành công.");
    }
}
