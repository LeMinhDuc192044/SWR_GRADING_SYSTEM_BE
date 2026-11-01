using Application.Common;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class DiagramService : IDiagramService
{
    private readonly IDiagramRepository _diagramRepository;
    private readonly IDiagramElementRepository _elementRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DiagramService(
        IDiagramRepository diagramRepository,
        IDiagramElementRepository elementRepository,
        IUnitOfWork unitOfWork)
    {
        _diagramRepository = diagramRepository;
        _elementRepository = elementRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<DiagramDTO>> GetPagedAsync(PagedRequest request, string? search = null, CancellationToken ct = default)
    {
        var allDiagrams = await _diagramRepository.GetAllAsync(ct);

        var query = allDiagrams.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(d =>
                d.Name.ToLower().Contains(searchLower) ||
                (d.Description != null && d.Description.ToLower().Contains(searchLower)));
        }

        var list = query.OrderBy(d => d.Name).ToList();
        var totalCount = list.Count;
        var pagedItems = list
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        var dtoList = new List<DiagramDTO>();
        foreach (var diagram in pagedItems)
        {
            var elementCount = await _elementRepository.CountAsync(e => e.DiagramId == diagram.DiagramId, ct);
            dtoList.Add(ToDto(diagram, elementCount));
        }

        return new PagedResult<DiagramDTO>
        {
            Items = dtoList,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<Result<DiagramDetailDTO>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var diagram = await _diagramRepository.GetWithElementsByIdAsync(id, ct);
        if (diagram is null)
            return Result<DiagramDetailDTO>.Failure("Diagram not found.", "DIAGRAM_NOT_FOUND");

        return Result<DiagramDetailDTO>.Success(ToDetailDto(diagram));
    }

    public async Task<Result<DiagramDetailDTO>> CreateAsync(CreateDiagramRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<DiagramDetailDTO>.Failure("Tên diagram không được để trống.", "INVALID_DIAGRAM_NAME");

        var trimmedName = request.Name.Trim();
        if (await _diagramRepository.IsNameExistsAsync(trimmedName, null, ct))
            return Result<DiagramDetailDTO>.Failure("Tên diagram đã tồn tại.", "DIAGRAM_NAME_EXISTS");

        var diagram = new Diagram
        {
            DiagramId = Guid.NewGuid(),
            Name = trimmedName,
            Description = request.Description?.Trim()
        };

        if (request.Elements is { Count: > 0 })
        {
            var elementCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var elReq in request.Elements)
            {
                if (string.IsNullOrWhiteSpace(elReq.ElementCode) || string.IsNullOrWhiteSpace(elReq.Name))
                    return Result<DiagramDetailDTO>.Failure("Element code và tên element không được để trống.", "INVALID_ELEMENT_DATA");

                var code = elReq.ElementCode.Trim();
                if (!elementCodes.Add(code))
                    return Result<DiagramDetailDTO>.Failure($"Mã element '{code}' bị trùng lặp trong danh sách.", "DUPLICATE_ELEMENT_CODE");

                diagram.Elements.Add(new DiagramElement
                {
                    ElementId = Guid.NewGuid(),
                    DiagramId = diagram.DiagramId,
                    ElementCode = code,
                    Name = elReq.Name.Trim(),
                    ShapeType = elReq.ShapeType?.Trim(),
                    Description = elReq.Description?.Trim(),
                    IsRequirement = elReq.IsRequired,
                    FileTemplateUrl = elReq.FileTemplateUrl?.Trim(),
                    ElementReferenceId = elReq.ElementReferenceId,
                    ElementReferenceType = elReq.ElementReferenceType?.Trim(),
                    SoftwareType = elReq.SoftwareType?.Trim()
                });
            }
        }

        await _diagramRepository.AddAsync(diagram, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<DiagramDetailDTO>.Success(ToDetailDto(diagram));
    }

    public async Task<Result<DiagramDTO>> UpdateAsync(Guid id, UpdateDiagramRequest request, CancellationToken ct = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(id, ct);
        if (diagram is null)
            return Result<DiagramDTO>.Failure("Diagram not found.", "DIAGRAM_NOT_FOUND");

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<DiagramDTO>.Failure("Tên diagram không được để trống.", "INVALID_DIAGRAM_NAME");

            var trimmedName = request.Name.Trim();
            if (await _diagramRepository.IsNameExistsAsync(trimmedName, id, ct))
                return Result<DiagramDTO>.Failure("Tên diagram đã tồn tại.", "DIAGRAM_NAME_EXISTS");

            diagram.Name = trimmedName;
        }

        if (request.Description is not null)
            diagram.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        _diagramRepository.Update(diagram);
        await _unitOfWork.SaveChangesAsync(ct);

        var elementCount = await _elementRepository.CountAsync(e => e.DiagramId == diagram.DiagramId, ct);
        return Result<DiagramDTO>.Success(ToDto(diagram, elementCount));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var diagram = await _diagramRepository.GetByIdAsync(id, ct);
        if (diagram is null)
            return Result.Failure("Diagram not found.", "DIAGRAM_NOT_FOUND");

        if (await _diagramRepository.HasRubricsAsync(id, ct))
            return Result.Failure("Diagram đang được liên kết với rubric/đề thi, không thể xóa.", "DIAGRAM_IN_USE");

        _diagramRepository.Remove(diagram);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static DiagramDTO ToDto(Diagram d, int elementCount) => new()
    {
        DiagramId = d.DiagramId,
        Name = d.Name,
        Description = d.Description,
        ElementCount = elementCount
    };

    private static DiagramDetailDTO ToDetailDto(Diagram d) => new()
    {
        DiagramId = d.DiagramId,
        Name = d.Name,
        Description = d.Description,
        ElementCount = d.Elements.Count,
        Elements = d.Elements.OrderBy(e => e.ElementCode).Select(ToElementDto).ToList()
    };

    private static DiagramElementDTO ToElementDto(DiagramElement e) => new()
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
