using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Infrastructure.Context;

public sealed class TendaaDbContext(DbContextOptions<TendaaDbContext> options, ICurrentShopAccessor currentShop)
    : DbContext(options)
{
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ToolTrace> ToolTraces => Set<ToolTrace>();
    public DbSet<Handoff> Handoffs => Set<Handoff>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TendaaDbContext).Assembly);

        // Every entity's Id is a client-generated Guid (set via Guid.NewGuid() in application
        // code), never database-generated. Without this, EF Core's default Guid convention
        // (ValueGeneratedOnAdd) leaves it ambiguous whether an entity reached via navigation-
        // collection fixup on an already-tracked parent (e.g. `order.Items.Add(...)` on a Draft
        // order loaded from the DB) is new or pre-existing, and it can silently generate an
        // UPDATE for a row that was never inserted instead of an INSERT.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            modelBuilder.Entity(entityType.ClrType).Property("Id").ValueGeneratedNever();
        }

        // Every IShopScoped entity gets a global query filter, so a missing `WHERE ShopId = ...`
        // can't leak another shop's data. HasQueryFilter's lambda type is only known at runtime
        // per entity, hence the reflection — this is the standard EF Core pattern for applying a
        // shared filter generically. Null ShopId (migrations, seeding, startup) sees everything.
        var applyFilterMethod = typeof(TendaaDbContext).GetMethod(
            nameof(ApplyShopFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(IShopScoped).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            applyFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyShopFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, IShopScoped
    {
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => currentShop.ShopId == null || e.ShopId == currentShop.ShopId);
    }
}
