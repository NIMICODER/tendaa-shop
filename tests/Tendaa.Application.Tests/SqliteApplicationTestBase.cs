using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Implementations;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Tests;

/// <summary>Backs <see cref="IRepository{TEntity}"/> with a real <see cref="TendaaDbContext"/>
/// against a SQLite in-memory database, rather than a hand-rolled fake — Application services
/// call EF Core's async LINQ extensions (<c>FirstOrDefaultAsync</c>, <c>ToListAsync</c>, ...)
/// directly on <c>Query()</c>, and those throw against a plain <c>List&lt;T&gt;.AsQueryable()</c>
/// (its provider doesn't implement EF Core's async query provider interface). SQLite in-memory
/// still enforces unique indexes and foreign keys, unlike EF's InMemory provider, so it catches
/// more of what Postgres would catch.</summary>
public abstract class SqliteApplicationTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    protected readonly CurrentShopAccessor ShopAccessor = new();
    protected readonly TendaaDbContext Db;

    protected SqliteApplicationTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TendaaDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new TendaaDbContext(options, ShopAccessor);
        Db.Database.EnsureCreated();
    }

    protected IRepository<TEntity> CreateRepository<TEntity>() where TEntity : class => new Repository<TEntity>(Db);

    protected IUnitOfWork CreateUnitOfWork() => new UnitOfWork(Db);

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
