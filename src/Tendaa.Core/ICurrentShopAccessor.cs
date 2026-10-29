namespace Tendaa.Core;

/// <summary>Resolves which shop the current unit of work (an inbound WhatsApp event, a dashboard
/// request) belongs to. `TendaaDbContext` uses this to apply a global query filter to every
/// <see cref="IShopScoped"/> entity, so a missing `WHERE ShopId = ...` can't leak another shop's
/// data. Null means no shop is scoped yet (migrations, seeding, startup).</summary>
public interface ICurrentShopAccessor
{
    Guid? ShopId { get; }

    /// <summary>Scopes the rest of this request/event to <paramref name="shopId"/>. Called once,
    /// by whatever resolved the shop (the dev chat endpoint for now; Phase 3's webhook processor
    /// and the Phase 5 dashboard auth middleware later) — never by a tool or service mid-request.</summary>
    void SetShopId(Guid shopId);
}
