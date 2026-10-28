using Tendaa.Core;

namespace Tendaa.Infrastructure;

/// <summary>Mutable default implementation of <see cref="ICurrentShopAccessor"/>. Registered
/// scoped; Phase 3's webhook processor and the Phase 5 dashboard auth middleware set
/// <see cref="ShopId"/> once per request/event. Tests and the seeder set it directly.</summary>
public sealed class CurrentShopAccessor : ICurrentShopAccessor
{
    public Guid? ShopId { get; set; }
}
