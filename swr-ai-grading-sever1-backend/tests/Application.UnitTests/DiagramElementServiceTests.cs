using Application.DTOs.Diagrams;
using Application.Services;
using Domain.Entities;

namespace Application.UnitTests;

public class DiagramElementServiceTests
{
    private readonly FakeDiagramElementRepository _elementRepo = new();
    private readonly FakeDiagramRepository _diagramRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly DiagramElementService _service;
    private readonly Guid _diagramId = Guid.NewGuid();

    public DiagramElementServiceTests()
    {
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = _diagramId, Name = "Class Diagram" });
        _service = new DiagramElementService(_elementRepo, _diagramRepo, _uow);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesElement()
    {
        var request = new CreateDiagramElementRequest
        {
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Student Class",
            ShapeType = "Class",
            Description = "Represents student entity",
            IsRequired = true
        };

        var result = await _service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("C01", result.Data.ElementCode);
        Assert.Equal("Student Class", result.Data.Name);
        Assert.True(result.Data.IsRequired);
        Assert.Single(_elementRepo.Elements);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCodeInSameDiagram_ReturnsFailure()
    {
        _elementRepo.Elements.Add(new DiagramElement
        {
            ElementId = Guid.NewGuid(),
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Existing Class"
        });

        var request = new CreateDiagramElementRequest
        {
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Another Class"
        };

        var result = await _service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_CODE_EXISTS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_SelfReference_ReturnsFailure()
    {
        var elementId = Guid.NewGuid();
        _elementRepo.Elements.Add(new DiagramElement
        {
            ElementId = elementId,
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A"
        });

        var request = new UpdateDiagramElementRequest
        {
            ElementReferenceId = elementId // Self reference
        };

        var result = await _service.UpdateAsync(elementId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SELF_REFERENCE", result.ErrorCode);
    }

    [Fact]
    public async Task DeleteAsync_ElementWithReferences_ReturnsFailure()
    {
        var elementId = Guid.NewGuid();
        _elementRepo.Elements.Add(new DiagramElement
        {
            ElementId = elementId,
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A"
        });
        _elementRepo.HasChildReferences = true;

        var result = await _service.DeleteAsync(elementId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_HAS_REFERENCES", result.ErrorCode);
    }
}
