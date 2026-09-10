using System.Linq.Expressions;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Generic repository implementation — dùng EF Core DbContext.
/// Tất cả logic CRUD chung nằm ở đây, các repository cụ thể kế thừa để thêm method nghiệp vụ.
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    // ---------- Read ----------
    public virtual async Task<T?> GetByIdAsync(object id, CancellationToken ct = default)
        => await _set.FindAsync(new[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _set.AsNoTracking().ToListAsync(ct);

    public virtual async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AsNoTracking().Where(predicate).ToListAsync(ct);

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AsNoTracking().FirstOrDefaultAsync(predicate, ct);

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await _set.AnyAsync(predicate, ct);

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate == null ? await _set.CountAsync(ct) : await _set.CountAsync(predicate, ct);

    // ---------- Write ----------
    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await _set.AddAsync(entity, ct);

    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        => await _set.AddRangeAsync(entities, ct);

    public virtual void Update(T entity)
        => _set.Update(entity);

    public virtual void Remove(T entity)
        => _set.Remove(entity);

    public virtual void RemoveRange(IEnumerable<T> entities)
        => _set.RemoveRange(entities);
}

/// <summary>
/// Unit of Work implementation — gọi SaveChanges của DbContext.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _db;
    public UnitOfWork(AppDbContext db) { _db = db; }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
