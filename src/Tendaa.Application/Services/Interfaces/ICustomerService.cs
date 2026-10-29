using Tendaa.Application.Common;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Services.Interfaces;

public interface ICustomerService
{
    /// <summary>Finds the customer by (ShopId, BsuId), or creates one. Updates
    /// <paramref name="phone"/>/<paramref name="username"/>/<paramref name="name"/> only when a
    /// non-null value is passed — never clears a previously known value. See ARCHITECTURE.md's
    /// "Customer identity" section.</summary>
    Task<TendaaResult<Customer>> FindOrCreateByBsuIdAsync(
        Guid shopId,
        string bsuId,
        string? phone = null,
        string? username = null,
        string? name = null,
        CancellationToken cancellationToken = default);

    /// <summary>Handles a <c>user_changed_number</c>/<c>user_changed_user_id</c> system message:
    /// finds the existing customer by <paramref name="previousBsuId"/> and updates its BsuId in
    /// place (and phone, only if <paramref name="newPhone"/> is present) — never creates a second
    /// row. <see cref="TendaaResult{T}.NotFound"/> if no customer has <paramref
    /// name="previousBsuId"/>. See ARCHITECTURE.md's "Customer identity" / "BSUID changes".</summary>
    Task<TendaaResult<Customer>> LinkBsuIdChangeAsync(
        Guid shopId,
        string previousBsuId,
        string newBsuId,
        string? newPhone = null,
        CancellationToken cancellationToken = default);

    /// <summary>For a tool that must send something directly to the customer (e.g. a product
    /// photo) rather than through the agent's reply text — needs the full entity for addressing
    /// (<see cref="Customer.PhoneNumber"/> or <see cref="Customer.BsuId"/>), which
    /// <c>IAgentRequestContext</c> deliberately doesn't carry.</summary>
    Task<TendaaResult<Customer>> GetByIdAsync(
        Guid shopId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Saves a name the customer stated or corrected mid-chat onto their profile — not
    /// just the current order (<c>Order.CustomerName</c>) or the chat session's own memory — so
    /// it's available in later conversations via <c>GetCustomerProfile</c>.</summary>
    Task<TendaaResult<Customer>> UpdateNameAsync(
        Guid shopId, Guid customerId, string name, CancellationToken cancellationToken = default);
}
