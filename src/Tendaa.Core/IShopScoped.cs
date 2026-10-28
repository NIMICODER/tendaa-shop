namespace Tendaa.Core;

/// <summary>Marks an entity that belongs to exactly one shop. Every implementer gets a global
/// query filter in <c>TendaaDbContext</c> so tenant isolation can't be forgotten per-query.</summary>
public interface IShopScoped
{
    Guid ShopId { get; set; }
}
