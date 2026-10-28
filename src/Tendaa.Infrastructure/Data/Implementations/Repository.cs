using Microsoft.EntityFrameworkCore;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Infrastructure.Data.Implementations;

public sealed class Repository<TEntity>(TendaaDbContext db) : IRepository<TEntity>
    where TEntity : class
{
    private readonly DbSet<TEntity> _set = db.Set<TEntity>();

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await _set.FindAsync([id], cancellationToken);

    public IQueryable<TEntity> Query() => _set.AsQueryable();

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        await _set.AddAsync(entity, cancellationToken);

    public void Remove(TEntity entity) => _set.Remove(entity);
}
