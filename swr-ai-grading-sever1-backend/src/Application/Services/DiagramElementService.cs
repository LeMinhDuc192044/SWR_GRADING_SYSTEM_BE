using Application.Common;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class DiagramElementService : IDiagramElementService
{
    private readonly IDiagramElementRepository _elementRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DiagramElementService(
        IDiagramElementRepository elementRepository,
        IDiagramRepository diagramRepository,
        IUnitOfWork unitOfWork)
    {
        _elementRepository = elementRepository;
        _diagramRepository = diagramRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DiagramElementDTO>> GetPagedAsync(
        PagedRequest request,
        Guid? diagramId = null,
        string? search = null,
        CancellationToken ct = default)
    {
        IReadOnlyList<DiagramElement> elements;
        if (diagramId.HasValue)
        {
            elements = await _elementRepository.GetByDiagramIdAsync(diagramId.Value, ct);
        }
        else
        {
            elements = await _elementRepository.GetAllAsync(ct);
        }

        var query = elements.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e =>
                e.Name.ToLower().Contains(searchLower) ||
                e.ElementCode.ToLower().Contains(searchLower) ||
                (e.ShapeType != null && e.ShapeType.ToLower().Contains(searchLower)) ||
                (e.Description != null && e.Description.ToLower().Contains(searchLower)));
        }

        var list = query.OrderBy(e => e.ElementCode).ToList();
        var totalCount = list.Count;
        var pagedItems = list
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(ToDto)
            .ToList();

        return new PagedResult<DiagramElementDTO>
        {
            Items = pagedItems,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<IReadOnlyList<DiagramElementDTO>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default)
    {
        var elements = await _elementRepository.GetByDiagramIdAsync(diagramId, ct);
        return elements.Select(ToDto).ToList();
    }

    public async Task<Result<DiagramElementDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        return Result<DiagramElementDTO>.Success(ToDto(element));
    }

    public async Task<Result<DiagramElementDTO>> CreateAsync(CreateDiagramElementRequest request, CancellationToken ct = default)
    {
        if (!request.DiagramId.HasValue || request.DiagramId.Value == Guid.Empty)
            return Result<DiagramElementDTO>.Failure("DiagramId là bắt buộc.", "DIAGRAM_ID_REQUIRED");

        var diagram = await _diagramRepository.GetByIdAsync(request.DiagramId.Value, ct);
        if (diagram is null)
            return Result<DiagramElementDTO>.Failure("Diagram không tồn tại.", "DIAGRAM_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.ElementCode))
            return Result<DiagramElementDTO>.Failure("Mã element không được để trống.", "INVALID_ELEMENT_CODE");

        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<DiagramElementDTO>.Failure("Tên element không được để trống.", "INVALID_ELEMENT_NAME");

        var trimmedCode = request.ElementCode.Trim();
        if (await _elementRepository.IsElementCodeExistsAsync(request.DiagramId.Value, trimmedCode, null, ct))
            return Result<DiagramElementDTO>.Failure($"Mã element '{trimmedCode}' đã tồn tại trong diagram này.", "ELEMENT_CODE_EXISTS");

        if (request.ElementReferenceId.HasValue)
        {
            var refElement = await _elementRepository.GetByIdAsync(request.ElementReferenceId.Value, ct);
            if (refElement is null)
                return Result<DiagramElementDTO>.Failure("Element tham chiếu không tồn tại.", "REFERENCE_ELEMENT_NOT_FOUND");

            if (refElement.DiagramId != request.DiagramId.Value)
                return Result<DiagramElementDTO>.Failure("Element tham chiếu phải thuộc cùng một diagram.", "REFERENCE_ELEMENT_MISMATCH");
        }

        var element = new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = request.DiagramId.Value,
            ElementCode = trimmedCode,
            Name = request.Name.Trim(),
            ShapeType = request.ShapeType?.Trim(),
            Description = request.Description?.Trim(),
            IsRequirement = request.IsRequired,
            FileTemplateUrl = request.FileTemplateUrl?.Trim(),
            ElementReferenceId = request.ElementReferenceId,
            ElementReferenceType = request.ElementReferenceType?.Trim(),
            SoftwareType = request.SoftwareType?.Trim()
        };

        await _elementRepository.AddAsync(element, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var created = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(created ?? element));
    }

    public async Task<Result<DiagramElementDTO>> UpdateAsync(Guid id, UpdateDiagramElementRequest request, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (request.ElementCode is not null)
        {
            if (string.IsNullOrWhiteSpace(request.ElementCode))
                return Result<DiagramElementDTO>.Failure("Mã element không được để trống.", "INVALID_ELEMENT_CODE");

            var trimmedCode = request.ElementCode.Trim();
            if (await _elementRepository.IsElementCodeExistsAsync(element.DiagramId, trimmedCode, id, ct))
                return Result<DiagramElementDTO>.Failure($"Mã element '{trimmedCode}' đã tồn tại trong diagram này.", "ELEMENT_CODE_EXISTS");

            element.ElementCode = trimmedCode;
        }

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<DiagramElementDTO>.Failure("Tên element không được để trống.", "INVALID_ELEMENT_NAME");

            element.Name = request.Name.Trim();
        }

        if (request.ShapeType is not null)
            element.ShapeType = string.IsNullOrWhiteSpace(request.ShapeType) ? null : request.ShapeType.Trim();

        if (request.Description is not null)
            element.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.IsRequired.HasValue)
            element.IsRequirement = request.IsRequired.Value;

        if (request.FileTemplateUrl is not null)
            element.FileTemplateUrl = string.IsNullOrWhiteSpace(request.FileTemplateUrl) ? null : request.FileTemplateUrl.Trim();

        if (request.ElementReferenceId.HasValue)
        {
            if (request.ElementReferenceId.Value == id)
                return Result<DiagramElementDTO>.Failure("Element không thể tự tham chiếu chính nó.", "INVALID_SELF_REFERENCE");

            var refElement = await _elementRepository.GetByIdAsync(request.ElementReferenceId.Value, ct);
            if (refElement is null)
                return Result<DiagramElementDTO>.Failure("Element tham chiếu không tồn tại.", "REFERENCE_ELEMENT_NOT_FOUND");

            if (refElement.DiagramId != element.DiagramId)
                return Result<DiagramElementDTO>.Failure("Element tham chiếu phải thuộc cùng một diagram.", "REFERENCE_ELEMENT_MISMATCH");

            element.ElementReferenceId = request.ElementReferenceId.Value;
        }

        if (request.ElementReferenceType is not null)
            element.ElementReferenceType = string.IsNullOrWhiteSpace(request.ElementReferenceType) ? null : request.ElementReferenceType.Trim();

        if (request.SoftwareType is not null)
            element.SoftwareType = string.IsNullOrWhiteSpace(request.SoftwareType) ? null : request.SoftwareType.Trim();

        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(updated ?? element));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetByIdAsync(id, ct);
        if (element is null)
            return Result.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (await _elementRepository.HasChildReferencesAsync(id, ct))
            return Result.Failure("Element đang được tham chiếu bởi element khác, không thể xóa.", "ELEMENT_HAS_REFERENCES");

        if (await _elementRepository.HasParsedAnswersAsync(id, ct))
            return Result.Failure("Element đang có câu trả lời bài thi liên kết, không thể xóa.", "ELEMENT_IN_USE");

        _elementRepository.Remove(element);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static DiagramElementDTO ToDto(DiagramElement e) => new()
    {
        ElementId = e.ElementId,
        DiagramId = e.DiagramId,
        ElementCode = e.ElementCode,
        Name = e.Name,
        ShapeType = e.ShapeType,
        Description = e.Description,
        IsRequired = e.IsRequirement,
        FileTemplateUrl = e.FileTemplateUrl,
        ElementReferenceId = e.ElementReferenceId,
        ElementReferenceName = e.ReferenceElement?.Name,
        ElementReferenceType = e.ElementReferenceType,
        SoftwareType = e.SoftwareType
    };
}
