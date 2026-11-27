using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Domain.Entities;

namespace Application.Services;

public class DiagramElementService : IDiagramElementService
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private readonly IDiagramElementRepository _elementRepository;
    private readonly IDiagramRepository _diagramRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISupabaseStorage _storage;
    private readonly IImageUrlProvider _imageUrls;

    public DiagramElementService(
        IDiagramElementRepository elementRepository,
        IDiagramRepository diagramRepository,
        IUnitOfWork unitOfWork,
        ISupabaseStorage storage,
        IImageUrlProvider imageUrls)
    {
        _elementRepository = elementRepository;
        _diagramRepository = diagramRepository;
        _unitOfWork = unitOfWork;
        _storage = storage;
        _imageUrls = imageUrls;
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

    public async Task<Result<DiagramElementDTO>> CreateAsync(
        CreateDiagramElementRequest request,
        DiagramElementImageUpload? image = null,
        CancellationToken ct = default)
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

        ImageMeta? imageMeta = null;
        if (image is not null)
        {
            var imageValidation = ValidateImage(image);
            if (!imageValidation.IsSuccess)
                return Result<DiagramElementDTO>.Failure(imageValidation.Error!, imageValidation.ErrorCode);

            imageMeta = imageValidation.Data;
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
            ElementReferenceId = request.ElementReferenceId,
            ElementReferenceType = request.ElementReferenceType?.Trim(),
            SoftwareType = request.SoftwareType?.Trim()
        };

        // The image path is derived from the element id, so it can be stored before the insert.
        if (imageMeta is not null)
            element.FileTemplateUrl = await StoreImageAsync(element.ElementId, image!, imageMeta, ct);

        await _elementRepository.AddAsync(element, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var created = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(created ?? element));
    }

    public async Task<Result<DiagramElementDTO>> AssignToDiagramAsync(
        Guid diagramId, AssignDiagramElementRequest request, CancellationToken ct = default)
    {
        if (diagramId == Guid.Empty)
            return Result<DiagramElementDTO>.Failure("DiagramId là bắt buộc.", "DIAGRAM_ID_REQUIRED");

        if (request.ElementId == Guid.Empty)
            return Result<DiagramElementDTO>.Failure("ElementId là bắt buộc.", "ELEMENT_ID_REQUIRED");

        var diagram = await _diagramRepository.GetByIdAsync(diagramId, ct);
        if (diagram is null)
            return Result<DiagramElementDTO>.Failure("Diagram không tồn tại.", "DIAGRAM_NOT_FOUND");

        var element = await _elementRepository.GetWithDetailsByIdAsync(request.ElementId, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (element.DiagramId == diagramId)
            return Result<DiagramElementDTO>.Success(ToDto(element));

        if (await _elementRepository.IsElementCodeExistsAsync(diagramId, element.ElementCode, element.ElementId, ct))
            return Result<DiagramElementDTO>.Failure(
                $"Mã element '{element.ElementCode}' đã tồn tại trong diagram này.", "ELEMENT_CODE_EXISTS");

        // Answers and connections are tied to a diagram, so a used element cannot be moved.
        if (await _elementRepository.HasChildReferencesAsync(element.ElementId, ct))
            return Result<DiagramElementDTO>.Failure(
                "Element đang được tham chiếu bởi element khác, không thể chuyển diagram.", "ELEMENT_HAS_REFERENCES");

        if (await _elementRepository.HasParsedAnswersAsync(element.ElementId, ct))
            return Result<DiagramElementDTO>.Failure(
                "Element đang có câu trả lời bài thi liên kết, không thể chuyển diagram.", "ELEMENT_IN_USE");

        // An outgoing connection to the previous diagram would become cross-diagram, so drop it.
        if (element.ElementReferenceId.HasValue)
        {
            var referenced = await _elementRepository.GetByIdAsync(element.ElementReferenceId.Value, ct);
            if (referenced is null || referenced.DiagramId != diagramId)
            {
                element.ElementReferenceId = null;
                element.ElementReferenceType = null;
            }
        }

        element.DiagramId = diagramId;
        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(updated ?? element));
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
        {
            // The column also holds the uploaded element image, so a normal update must never
            // replace it; the image has to be deleted explicitly first.
            var currentImage = DiagramElementImagePath.Matches(element.ElementId, element.FileTemplateUrl)
                ? element.FileTemplateUrl
                : null;
            var newTemplateUrl = string.IsNullOrWhiteSpace(request.FileTemplateUrl)
                ? null
                : request.FileTemplateUrl.Trim();

            if (currentImage is not null &&
                !string.Equals(currentImage, newTemplateUrl, StringComparison.Ordinal))
            {
                return Result<DiagramElementDTO>.Failure(
                    "Element đã có ảnh được tải lên; hãy xóa ảnh trước khi đổi FileTemplateUrl.",
                    "ELEMENT_IMAGE_PRESENT");
            }

            element.FileTemplateUrl = newTemplateUrl;
        }

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

        var storedImagePath = DiagramElementImagePath.Matches(element.ElementId, element.FileTemplateUrl)
            ? element.FileTemplateUrl
            : null;

        _elementRepository.Remove(element);
        await _unitOfWork.SaveChangesAsync(ct);

        // The element row is gone only once the commit succeeded, so its image is removed after it.
        if (storedImagePath is not null)
            await _storage.DeleteAsync(storedImagePath, ct);

        return Result.Success();
    }

    public async Task<Result<DiagramElementDTO>> UploadImageAsync(
        Guid id, DiagramElementImageUpload upload, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        var validation = ValidateImage(upload);
        if (!validation.IsSuccess)
            return Result<DiagramElementDTO>.Failure(validation.Error!, validation.ErrorCode);

        // The path is deterministic so re-uploading replaces the old element image.
        var path = await StoreImageAsync(element.ElementId, upload, validation.Data!, ct);

        // An older upload may have had another extension: remove that stale object only. A
        // template link in the same column is not a storage object and must not be deleted.
        if (DiagramElementImagePath.Matches(element.ElementId, element.FileTemplateUrl) &&
            !string.Equals(element.FileTemplateUrl, path, StringComparison.OrdinalIgnoreCase))
        {
            await _storage.DeleteAsync(element.FileTemplateUrl!, ct);
        }

        element.FileTemplateUrl = path;
        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(updated ?? element));
    }

    public async Task<Result> DeleteImageAsync(Guid id, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (!DiagramElementImagePath.Matches(element.ElementId, element.FileTemplateUrl))
            return Result.Failure("This element has no stored image.", "IMAGE_NOT_FOUND");

        await _storage.DeleteAsync(element.FileTemplateUrl!, ct);
        element.FileTemplateUrl = null;
        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<StoredFileDownload>> GetImageAsync(Guid id, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetByIdAsync(id, ct);
        if (element is null)
            return Result<StoredFileDownload>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (!DiagramElementImagePath.Matches(element.ElementId, element.FileTemplateUrl))
            return Result<StoredFileDownload>.Failure("This element has no stored image.", "IMAGE_NOT_FOUND");

        var image = await _storage.DownloadAsync(
            element.FileTemplateUrl!, Path.GetFileName(element.FileTemplateUrl!), ct);
        return Result<StoredFileDownload>.Success(image);
    }

    public async Task<Result<DiagramElementDTO>> ConnectAsync(
        Guid id, ConnectDiagramElementRequest request, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        if (request.TargetElementId == Guid.Empty)
            return Result<DiagramElementDTO>.Failure("TargetElementId is required.", "TARGET_ELEMENT_REQUIRED");

        if (request.TargetElementId == id)
            return Result<DiagramElementDTO>.Failure("An element cannot connect to itself.", "INVALID_SELF_REFERENCE");

        var target = await _elementRepository.GetByIdAsync(request.TargetElementId, ct);
        if (target is null)
            return Result<DiagramElementDTO>.Failure("Target element not found.", "TARGET_ELEMENT_NOT_FOUND");

        if (target.DiagramId != element.DiagramId)
            return Result<DiagramElementDTO>.Failure("Both elements must belong to the same diagram.", "REFERENCE_ELEMENT_MISMATCH");

        if (await CreatesCycleAsync(id, target, ct))
            return Result<DiagramElementDTO>.Failure("This connection would create a cycle.", "CIRCULAR_CONNECTION");

        element.ElementReferenceId = target.ElementId;
        element.ElementReferenceType = string.IsNullOrWhiteSpace(request.ConnectionType)
            ? null
            : request.ConnectionType.Trim();

        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(updated ?? element));
    }

    public async Task<Result<DiagramElementDTO>> DisconnectAsync(Guid id, CancellationToken ct = default)
    {
        var element = await _elementRepository.GetWithDetailsByIdAsync(id, ct);
        if (element is null)
            return Result<DiagramElementDTO>.Failure("Diagram element not found.", "ELEMENT_NOT_FOUND");

        element.ElementReferenceId = null;
        element.ElementReferenceType = null;
        _elementRepository.Update(element);
        await _unitOfWork.SaveChangesAsync(ct);

        var updated = await _elementRepository.GetWithDetailsByIdAsync(element.ElementId, ct);
        return Result<DiagramElementDTO>.Success(ToDto(updated ?? element));
    }

    // Walking the target's reference chain must never reach this element again.
    private async Task<bool> CreatesCycleAsync(Guid sourceElementId, DiagramElement target, CancellationToken ct)
    {
        var visited = new HashSet<Guid>();
        var current = target;

        while (current.ElementReferenceId.HasValue && visited.Add(current.ElementId))
        {
            if (current.ElementReferenceId.Value == sourceElementId)
                return true;

            var next = await _elementRepository.GetByIdAsync(current.ElementReferenceId.Value, ct);
            if (next is null)
                return false;

            current = next;
        }

        return false;
    }

    private sealed record ImageMeta(string ContentType, string Extension);

    private static Result<ImageMeta> ValidateImage(DiagramElementImageUpload upload)
    {
        if (upload.Length <= 0)
            return Result<ImageMeta>.Failure("The image file is empty.", "EMPTY_IMAGE_FILE");

        if (upload.Length > MaxImageBytes)
            return Result<ImageMeta>.Failure("The image exceeds the 5 MB limit.", "IMAGE_TOO_LARGE");

        if (!TryResolveImageContentType(upload, out var contentType, out var extension))
            return Result<ImageMeta>.Failure(
                "Only PNG, JPEG, WEBP or GIF images are supported.", "UNSUPPORTED_IMAGE_TYPE");

        return Result<ImageMeta>.Success(new ImageMeta(contentType, extension));
    }

    private async Task<string> StoreImageAsync(
        Guid elementId, DiagramElementImageUpload upload, ImageMeta meta, CancellationToken ct)
    {
        var path = DiagramElementImagePath.Build(elementId, meta.Extension);
        await _storage.UploadAsync(path, upload.Content, meta.ContentType, ct);
        return path;
    }

    private static bool TryResolveImageContentType(
        DiagramElementImageUpload upload, out string contentType, out string extension)
    {
        var declared = upload.ContentType?.Split(';')[0].Trim() ?? string.Empty;
        if (AllowedImageTypes.TryGetValue(declared, out extension!))
        {
            contentType = declared.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : declared;
            return true;
        }

        var fileExtension = Path.GetExtension(upload.FileName);
        if (AllowedImageTypes.TryGetValue($"image/{fileExtension.TrimStart('.').ToLowerInvariant()}", out extension!))
        {
            contentType = $"image/{fileExtension.TrimStart('.').ToLowerInvariant()}";
            return true;
        }

        contentType = "application/octet-stream";
        extension = string.Empty;
        return false;
    }

    private DiagramElementDTO ToDto(DiagramElement e) => new()
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
        SoftwareType = e.SoftwareType,
        ImageUrl = _imageUrls.BuildDiagramElementImageUrl(e.ElementId, e.FileTemplateUrl)
    };
}
