using Tendaa.Core;

namespace Tendaa.Infrastructure;

/// <summary>Mutable default implementation of <see cref="ICurrentShopAccessor"/>. Registered
/// scoped; whatever resolves the shop for this request/event calls
/// <see cref="ICurrentShopAccessor.SetShopId"/> once (the dev chat endpoint for now; Phase 3's
/// webhook processor and the Phase 5 dashboard auth middleware later).</summary>
public sealed class CurrentShopAccessor : ICurrentShopAccessor
{
    public Guid? ShopId { get; private set; }

    public void SetShopId(Guid shopId) => ShopId = shopId;
}
