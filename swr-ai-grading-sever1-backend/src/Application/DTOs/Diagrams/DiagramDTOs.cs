namespace Application.DTOs.Diagrams;

public class DiagramDTO
{
    public Guid DiagramId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileTemplateUrl { get; set; }
    public int ElementCount { get; set; }
}

public sealed class DiagramDetailDTO : DiagramDTO
{
    public IReadOnlyList<DiagramElementDTO> Elements { get; set; } = [];
}

public class CreateDiagramRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FileTemplateUrl { get; set; }
    public List<CreateDiagramElementRequest>? Elements { get; set; }
}

public class UpdateDiagramRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? FileTemplateUrl { get; set; }
}

public class DiagramElementDTO
{
    public Guid ElementId { get; set; }
    public Guid DiagramId { get; set; }
    public string ElementCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShapeType { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string? FileTemplateUrl { get; set; }
    public Guid? ElementReferenceId { get; set; }
    public string? ElementReferenceName { get; set; }
    public string? ElementReferenceType { get; set; }
    public string? SoftwareType { get; set; }
}

public class CreateDiagramElementRequest
{
    public Guid? DiagramId { get; set; }
    public string ElementCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShapeType { get; set; }
    public string? Description { get; set; }
    public bool IsRequired { get; set; }
    public string? FileTemplateUrl { get; set; }
    public Guid? ElementReferenceId { get; set; }
    public string? ElementReferenceType { get; set; }
    public string? SoftwareType { get; set; }
}

public class UpdateDiagramElementRequest
{
    public string? ElementCode { get; set; }
    public string? Name { get; set; }
    public string? ShapeType { get; set; }
    public string? Description { get; set; }
    public bool? IsRequired { get; set; }
    public string? FileTemplateUrl { get; set; }
    public Guid? ElementReferenceId { get; set; }
    public string? ElementReferenceType { get; set; }
    public string? SoftwareType { get; set; }
}
