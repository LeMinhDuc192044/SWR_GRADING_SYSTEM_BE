using Application.Common;
using Application.Common.Interfaces;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Moq;

namespace Application.UnitTests;

public class DiagramElementServiceTests
{
    private readonly FakeDiagramElementRepository _elementRepo = new();
    private readonly FakeDiagramRepository _diagramRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly Mock<ISupabaseStorage> _storage = new();
    private readonly DiagramElementService _service;
    private readonly Guid _diagramId = Guid.NewGuid();

    public DiagramElementServiceTests()
    {
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = _diagramId, Name = "Class Diagram" });
        _service = new DiagramElementService(
            _elementRepo,
            _diagramRepo,
            _uow,
            _storage.Object,
            new Mock<IImageUrlProvider>().Object);
    }

    [Fact]
    public async Task ConnectAsync_TargetInSameDiagram_LinksElements()
    {
        var source = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "Source" };
        var target = new DiagramElement { DiagramId = _diagramId, ElementCode = "B", Name = "Target" };
        _elementRepo.Elements.AddRange([source, target]);

        var result = await _service.ConnectAsync(
            source.ElementId, new ConnectDiagramElementRequest { TargetElementId = target.ElementId, ConnectionType = "association" });

        Assert.True(result.IsSuccess);
        Assert.Equal(target.ElementId, source.ElementReferenceId);
        Assert.Equal("association", source.ElementReferenceType);
    }

    [Fact]
    public async Task ConnectAsync_TargetInAnotherDiagram_ReturnsMismatch()
    {
        var source = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "Source" };
        var target = new DiagramElement { DiagramId = Guid.NewGuid(), ElementCode = "B", Name = "Target" };
        _elementRepo.Elements.AddRange([source, target]);

        var result = await _service.ConnectAsync(
            source.ElementId, new ConnectDiagramElementRequest { TargetElementId = target.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("REFERENCE_ELEMENT_MISMATCH", result.ErrorCode);
    }

    [Fact]
    public async Task ConnectAsync_SelfReference_ReturnsFailure()
    {
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "Source" };
        _elementRepo.Elements.Add(element);

        var result = await _service.ConnectAsync(
            element.ElementId, new ConnectDiagramElementRequest { TargetElementId = element.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("INVALID_SELF_REFERENCE", result.ErrorCode);
    }

    [Fact]
    public async Task ConnectAsync_CircularChain_ReturnsFailure()
    {
        var first = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "First" };
        var second = new DiagramElement { DiagramId = _diagramId, ElementCode = "B", Name = "Second" };
        first.ElementReferenceId = second.ElementId;
        _elementRepo.Elements.AddRange([first, second]);

        var result = await _service.ConnectAsync(
            second.ElementId, new ConnectDiagramElementRequest { TargetElementId = first.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("CIRCULAR_CONNECTION", result.ErrorCode);
    }

    [Fact]
    public async Task DisconnectAsync_LinkingElement_ClearsReference()
    {
        var source = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "Source" };
        var target = new DiagramElement { DiagramId = _diagramId, ElementCode = "B", Name = "Target" };
        source.ElementReferenceId = target.ElementId;
        source.ElementReferenceType = "association";
        _elementRepo.Elements.AddRange([source, target]);

        var result = await _service.DisconnectAsync(source.ElementId);

        Assert.True(result.IsSuccess);
        Assert.Null(source.ElementReferenceId);
        Assert.Null(source.ElementReferenceType);
    }

    [Fact]
    public async Task UploadImageAsync_UnsupportedType_ReturnsFailure()
    {
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "A", Name = "Source" };
        _elementRepo.Elements.Add(element);

        var result = await _service.UploadImageAsync(element.ElementId, new DiagramElementImageUpload
        {
            Content = new MemoryStream([1, 2, 3]),
            FileName = "element.pdf",
            ContentType = "application/pdf",
            Length = 3
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("UNSUPPORTED_IMAGE_TYPE", result.ErrorCode);
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
    public async Task CreateAsync_WithImage_StoresImageAndFillsFileTemplateUrl()
    {
        var request = new CreateDiagramElementRequest
        {
            DiagramId = _diagramId,
            ElementCode = "C02",
            Name = "Element With Image"
        };

        var result = await _service.CreateAsync(request, new DiagramElementImageUpload
        {
            Content = new MemoryStream([1, 2, 3]),
            FileName = "element.png",
            ContentType = "image/png",
            Length = 3
        });

        Assert.True(result.IsSuccess);
        var element = Assert.Single(_elementRepo.Elements);
        var expectedPath = DiagramElementImagePath.Build(element.ElementId, ".png");
        Assert.Equal(expectedPath, element.FileTemplateUrl);
        _storage.Verify(
            storage => storage.UploadAsync(
                expectedPath, It.IsAny<Stream>(), "image/png", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithUnsupportedImage_ReturnsFailureWithoutCreating()
    {
        var request = new CreateDiagramElementRequest
        {
            DiagramId = _diagramId,
            ElementCode = "C02",
            Name = "Element With Image"
        };

        var result = await _service.CreateAsync(request, new DiagramElementImageUpload
        {
            Content = new MemoryStream([1, 2, 3]),
            FileName = "element.pdf",
            ContentType = "application/pdf",
            Length = 3
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("UNSUPPORTED_IMAGE_TYPE", result.ErrorCode);
        Assert.Empty(_elementRepo.Elements);
        _storage.Verify(
            storage => storage.UploadAsync(
                It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
    public async Task UpdateAsync_ElementHasUploadedImage_KeepsImageAndReturnsFailure()
    {
        var elementId = Guid.NewGuid();
        var imagePath = DiagramElementImagePath.Build(elementId, ".png");
        _elementRepo.Elements.Add(new DiagramElement
        {
            ElementId = elementId,
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A",
            FileTemplateUrl = imagePath
        });

        var result = await _service.UpdateAsync(elementId, new UpdateDiagramElementRequest
        {
            FileTemplateUrl = "https://example.com/template.puml"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_IMAGE_PRESENT", result.ErrorCode);
        Assert.Equal(imagePath, _elementRepo.Elements.Single().FileTemplateUrl);
    }

    [Fact]
    public async Task DeleteImageAsync_TemplateUrlIsNotAnImage_KeepsUrl()
    {
        var element = new DiagramElement
        {
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A",
            FileTemplateUrl = "https://example.com/template.png"
        };
        _elementRepo.Elements.Add(element);

        var result = await _service.DeleteImageAsync(element.ElementId);

        Assert.False(result.IsSuccess);
        Assert.Equal("IMAGE_NOT_FOUND", result.ErrorCode);
        Assert.Equal("https://example.com/template.png", element.FileTemplateUrl);
        _storage.Verify(
            storage => storage.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UploadImageAsync_TemplateUrlIsNotDeleted()
    {
        var element = new DiagramElement
        {
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A",
            FileTemplateUrl = "https://example.com/template.png"
        };
        _elementRepo.Elements.Add(element);

        var result = await _service.UploadImageAsync(element.ElementId, new DiagramElementImageUpload
        {
            Content = new MemoryStream([1, 2, 3]),
            FileName = "diagram.png",
            ContentType = "image/png",
            Length = 3
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(DiagramElementImagePath.Build(element.ElementId, ".png"), element.FileTemplateUrl);
        _storage.Verify(
            storage => storage.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_ElementWithStoredImage_DeletesImageObject()
    {
        var elementId = Guid.NewGuid();
        var imagePath = DiagramElementImagePath.Build(elementId, ".png");
        _elementRepo.Elements.Add(new DiagramElement
        {
            ElementId = elementId,
            DiagramId = _diagramId,
            ElementCode = "C01",
            Name = "Class A",
            FileTemplateUrl = imagePath
        });

        var result = await _service.DeleteAsync(elementId);

        Assert.True(result.IsSuccess);
        Assert.Empty(_elementRepo.Elements);
        _storage.Verify(
            storage => storage.DeleteAsync(imagePath, It.IsAny<CancellationToken>()),
            Times.Once);
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

    [Fact]
    public async Task AssignToDiagramAsync_ElementInAnotherDiagram_MovesElement()
    {
        var targetDiagramId = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = targetDiagramId, Name = "Use Case Diagram" });
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "C01", Name = "Class A" };
        _elementRepo.Elements.Add(element);

        var result = await _service.AssignToDiagramAsync(
            targetDiagramId, new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.True(result.IsSuccess);
        Assert.Equal(targetDiagramId, element.DiagramId);
        Assert.Equal(targetDiagramId, result.Data!.DiagramId);
        Assert.Equal(1, _uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignToDiagramAsync_CodeAlreadyInTargetDiagram_ReturnsFailure()
    {
        var targetDiagramId = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = targetDiagramId, Name = "Use Case Diagram" });
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "C01", Name = "Class A" };
        _elementRepo.Elements.Add(element);
        _elementRepo.Elements.Add(new DiagramElement
        {
            DiagramId = targetDiagramId,
            ElementCode = "C01",
            Name = "Class B"
        });

        var result = await _service.AssignToDiagramAsync(
            targetDiagramId, new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_CODE_EXISTS", result.ErrorCode);
        Assert.Equal(_diagramId, element.DiagramId);
    }

    [Fact]
    public async Task AssignToDiagramAsync_ElementWithReferences_ReturnsFailure()
    {
        var targetDiagramId = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = targetDiagramId, Name = "Use Case Diagram" });
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "C01", Name = "Class A" };
        _elementRepo.Elements.Add(element);
        _elementRepo.HasChildReferences = true;

        var result = await _service.AssignToDiagramAsync(
            targetDiagramId, new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("ELEMENT_HAS_REFERENCES", result.ErrorCode);
        Assert.Equal(_diagramId, element.DiagramId);
    }

    [Fact]
    public async Task AssignToDiagramAsync_ConnectionToOldDiagram_IsCleared()
    {
        var targetDiagramId = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = targetDiagramId, Name = "Use Case Diagram" });
        var oldTarget = new DiagramElement { DiagramId = _diagramId, ElementCode = "B", Name = "Old Target" };
        var element = new DiagramElement
        {
            DiagramId = _diagramId,
            ElementCode = "A",
            Name = "Source",
            ElementReferenceId = oldTarget.ElementId,
            ElementReferenceType = "association"
        };
        _elementRepo.Elements.AddRange([element, oldTarget]);

        var result = await _service.AssignToDiagramAsync(
            targetDiagramId, new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.True(result.IsSuccess);
        Assert.Equal(targetDiagramId, element.DiagramId);
        Assert.Null(element.ElementReferenceId);
        Assert.Null(element.ElementReferenceType);
    }

    [Fact]
    public async Task AssignToDiagramAsync_SameDiagram_ReturnsSuccessWithoutSaving()
    {
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "C01", Name = "Class A" };
        _elementRepo.Elements.Add(element);

        var result = await _service.AssignToDiagramAsync(
            _diagramId, new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignToDiagramAsync_UnknownDiagram_ReturnsFailure()
    {
        var element = new DiagramElement { DiagramId = _diagramId, ElementCode = "C01", Name = "Class A" };
        _elementRepo.Elements.Add(element);

        var result = await _service.AssignToDiagramAsync(
            Guid.NewGuid(), new AssignDiagramElementRequest { ElementId = element.ElementId });

        Assert.False(result.IsSuccess);
        Assert.Equal("DIAGRAM_NOT_FOUND", result.ErrorCode);
    }
}
