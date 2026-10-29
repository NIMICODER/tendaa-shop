using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Common;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Handoffs;
using Tendaa.Infrastructure.Data.Interfaces;

namespace Tendaa.Application.Services.Implementations;

public sealed class HandoffService(IRepository<Handoff> handoffs, IUnitOfWork unitOfWork) : IHandoffService
{
    public async Task<TendaaResult> RequestHandoffAsync(
        Guid shopId,
        Guid conversationId,
        string reason,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var alreadyOpen = await handoffs.Query()
            .AnyAsync(h => h.ShopId == shopId && h.ConversationId == conversationId && h.Status == HandoffStatus.Open, cancellationToken);

        if (alreadyOpen)
        {
            return TendaaResult.Success("A handoff is already open for this conversation.");
        }

        var handoff = new Handoff
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            ConversationId = conversationId,
            Reason = reason,
            Summary = summary,
            Status = HandoffStatus.Open,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await handoffs.AddAsync(handoff, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return TendaaResult.Success();
    }

    public async Task<bool> HasOpenHandoffAsync(
        Guid shopId, Guid conversationId, CancellationToken cancellationToken = default) =>
        await handoffs.Query()
            .AnyAsync(h => h.ShopId == shopId && h.ConversationId == conversationId && h.Status == HandoffStatus.Open, cancellationToken);
}
