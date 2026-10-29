using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Services;

public class ConversationServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();

    private ConversationService CreateService() =>
        new(CreateRepository<Conversation>(), CreateRepository<Message>(), CreateUnitOfWork());

    [Fact]
    public async Task SaveTurnAsync_SetsLastMessageAt_EvenWhenHandedOffWithNoAssistantReply()
    {
        var conversationId = Guid.NewGuid();
        Db.Conversations.Add(new Conversation
        {
            Id = conversationId,
            ShopId = _shopId,
            CustomerId = _customerId,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);

        var before = DateTimeOffset.UtcNow;
        await CreateService().SaveTurnAsync(_shopId, conversationId, "Hello", assistantText: null, []);

        var persisted = await Db.Conversations.SingleAsync();
        Assert.NotNull(persisted.LastMessageAt);
        Assert.True(persisted.LastMessageAt >= before);
    }
}
