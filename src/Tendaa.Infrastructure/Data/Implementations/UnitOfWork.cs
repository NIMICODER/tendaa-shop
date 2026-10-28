using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Infrastructure.Data.Implementations;

public sealed class UnitOfWork(TendaaDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);
}
