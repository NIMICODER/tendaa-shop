namespace Tendaa.Core;

/// <summary>Resolves which shop the current unit of work (an inbound WhatsApp event, a dashboard
/// request) belongs to. `TendaaDbContext` uses this to apply a global query filter to every
/// <see cref="IShopScoped"/> entity, so a missing `WHERE ShopId = ...` can't leak another shop's
/// data. Null means no shop is scoped yet (migrations, seeding, startup).</summary>
public interface ICurrentShopAccessor
{
    Guid? ShopId { get; }
}
