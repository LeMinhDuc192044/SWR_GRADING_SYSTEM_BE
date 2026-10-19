using System.Linq.Expressions;
using Application.Common;
using Application.DTOs.Diagrams;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;

namespace Application.UnitTests;

public class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}

public class FakeDiagramRepository : IDiagramRepository
{
    public readonly List<Diagram> Diagrams = [];

    public Task<Diagram?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        var guid = (Guid)id;
        return Task.FromResult(Diagrams.FirstOrDefault(d => d.DiagramId == guid));
    }

    public Task<IReadOnlyList<Diagram>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Diagram>>(Diagrams);

    public Task<IReadOnlyList<Diagram>> FindAsync(Expression<Func<Diagram, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Diagram>>(Diagrams.AsQueryable().Where(predicate).ToList());

    public Task<Diagram?> FirstOrDefaultAsync(Expression<Func<Diagram, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Diagrams.AsQueryable().FirstOrDefault(predicate));

    public Task<bool> AnyAsync(Expression<Func<Diagram, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Diagrams.AsQueryable().Any(predicate));

    public Task<int> CountAsync(Expression<Func<Diagram, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate == null ? Diagrams.Count : Diagrams.AsQueryable().Count(predicate));

    public Task AddAsync(Diagram entity, CancellationToken ct = default)
    {
        Diagrams.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<Diagram> entities, CancellationToken ct = default)
    {
        Diagrams.AddRange(entities);
        return Task.CompletedTask;
    }

    public void Update(Diagram entity) { }
    public void Remove(Diagram entity) => Diagrams.Remove(entity);
    public void RemoveRange(IEnumerable<Diagram> entities)
    {
        foreach (var e in entities) Diagrams.Remove(e);
    }

    public Task<Diagram?> GetWithElementsByIdAsync(Guid diagramId, CancellationToken ct = default)
        => Task.FromResult(Diagrams.FirstOrDefault(d => d.DiagramId == diagramId));

    public Task<bool> IsNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return Task.FromResult(Diagrams.Any(d =>
            d.Name.ToLower() == normalized &&
            (!excludeId.HasValue || d.DiagramId != excludeId.Value)));
    }

    public bool HasRubrics { get; set; } = false;
    public Task<bool> HasRubricsAsync(Guid diagramId, CancellationToken ct = default)
        => Task.FromResult(HasRubrics);
}

public class FakeDiagramElementRepository : IDiagramElementRepository
{
    public readonly List<DiagramElement> Elements = [];

    public Task<DiagramElement?> GetByIdAsync(object id, CancellationToken ct = default)
    {
        var guid = (Guid)id;
        return Task.FromResult(Elements.FirstOrDefault(e => e.ElementId == guid));
    }

    public Task<IReadOnlyList<DiagramElement>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DiagramElement>>(Elements);

    public Task<IReadOnlyList<DiagramElement>> FindAsync(Expression<Func<DiagramElement, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DiagramElement>>(Elements.AsQueryable().Where(predicate).ToList());

    public Task<DiagramElement?> FirstOrDefaultAsync(Expression<Func<DiagramElement, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Elements.AsQueryable().FirstOrDefault(predicate));

    public Task<bool> AnyAsync(Expression<Func<DiagramElement, bool>> predicate, CancellationToken ct = default)
        => Task.FromResult(Elements.AsQueryable().Any(predicate));

    public Task<int> CountAsync(Expression<Func<DiagramElement, bool>>? predicate = null, CancellationToken ct = default)
        => Task.FromResult(predicate == null ? Elements.Count : Elements.AsQueryable().Count(predicate));

    public Task AddAsync(DiagramElement entity, CancellationToken ct = default)
    {
        Elements.Add(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<DiagramElement> entities, CancellationToken ct = default)
    {
        Elements.AddRange(entities);
        return Task.CompletedTask;
    }

    public void Update(DiagramElement entity) { }
    public void Remove(DiagramElement entity) => Elements.Remove(entity);
    public void RemoveRange(IEnumerable<DiagramElement> entities)
    {
        foreach (var e in entities) Elements.Remove(e);
    }

    public Task<IReadOnlyList<DiagramElement>> GetByDiagramIdAsync(Guid diagramId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DiagramElement>>(Elements.Where(e => e.DiagramId == diagramId).ToList());

    public Task<DiagramElement?> GetWithDetailsByIdAsync(Guid elementId, CancellationToken ct = default)
        => Task.FromResult(Elements.FirstOrDefault(e => e.ElementId == elementId));

    public Task<bool> IsElementCodeExistsAsync(Guid diagramId, string elementCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var normalized = elementCode.Trim().ToLower();
        return Task.FromResult(Elements.Any(e =>
            e.DiagramId == diagramId &&
            e.ElementCode.ToLower() == normalized &&
            (!excludeId.HasValue || e.ElementId != excludeId.Value)));
    }

    public bool HasParsedAnswers { get; set; } = false;
    public Task<bool> HasParsedAnswersAsync(Guid elementId, CancellationToken ct = default)
        => Task.FromResult(HasParsedAnswers);

    public bool HasChildReferences { get; set; } = false;
    public Task<bool> HasChildReferencesAsync(Guid elementId, CancellationToken ct = default)
        => Task.FromResult(HasChildReferences);
}

public class DiagramServiceTests
{
    private readonly FakeDiagramRepository _diagramRepo = new();
    private readonly FakeDiagramElementRepository _elementRepo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly DiagramService _service;

    public DiagramServiceTests()
    {
        _service = new DiagramService(_diagramRepo, _elementRepo, _uow);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesDiagram()
    {
        var request = new CreateDiagramRequest
        {
            Name = "Class Diagram",
            Description = "Standard UML Class Diagram",
            FileTemplateUrl = "https://example.com/template.png"
        };

        var result = await _service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("Class Diagram", result.Data.Name);
        Assert.Single(_diagramRepo.Diagrams);
        Assert.Equal(1, _uow.SaveChangesCallCount);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ReturnsFailure()
    {
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = Guid.NewGuid(), Name = "Use Case Diagram" });

        var request = new CreateDiagramRequest { Name = "Use Case Diagram" };
        var result = await _service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal("DIAGRAM_NAME_EXISTS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAsync_ExistingDiagram_UpdatesProperties()
    {
        var id = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = id, Name = "Old Name" });

        var request = new UpdateDiagramRequest { Name = "New Name", Description = "Updated description" };
        var result = await _service.UpdateAsync(id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Data!.Name);
        Assert.Equal("Updated description", result.Data.Description);
    }

    [Fact]
    public async Task DeleteAsync_DiagramInUse_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        _diagramRepo.Diagrams.Add(new Diagram { DiagramId = id, Name = "Activity Diagram" });
        _diagramRepo.HasRubrics = true;

        var result = await _service.DeleteAsync(id);

        Assert.False(result.IsSuccess);
        Assert.Equal("DIAGRAM_IN_USE", result.ErrorCode);
    }
}
