using Tendaa.Application.Services.Implementations;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Application.Tests.Services;

public class ConversationServiceDashboardTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();

    private ConversationService CreateService() => new(
        CreateRepository<Conversation>(),
        CreateRepository<Message>(),
        CreateRepository<Customer>(),
        CreateRepository<Handoff>(),
        CreateUnitOfWork());

    private async Task<Guid> SeedConversationAsync(string customerName, int inboundCount, bool withHandoff)
    {
        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Db.Customers.Add(new Customer
        {
            Id = customerId, ShopId = _shopId, BsuId = $"bsuid-{customerId:N}",
            Name = customerName, CreatedAt = now,
        });
        Db.Conversations.Add(new Conversation
        {
            Id = conversationId, ShopId = _shopId, CustomerId = customerId,
            LastMessageAt = now, CreatedAt = now.AddHours(-1),
        });
        for (var i = 0; i < inboundCount; i++)
        {
            Db.Messages.Add(new Message
            {
                Id = Guid.NewGuid(), ShopId = _shopId, ConversationId = conversationId,
                Direction = MessageDirection.Inbound, Type = MessageType.Text, Text = $"hello {i}",
                CreatedAt = now.AddMinutes(-i),
            });
        }

        if (withHandoff)
        {
            Db.Handoffs.Add(new Handoff
            {
                Id = Guid.NewGuid(), ShopId = _shopId, ConversationId = conversationId,
                Reason = "refund", Summary = "Wants a refund.", Status = HandoffStatus.Open, CreatedAt = now,
            });
        }

        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
        return conversationId;
    }

    [Fact]
    public async Task ListForDashboard_ReturnsConversationsWithCountsAndHandoffFlags()
    {
        await SeedConversationAsync("Ada", inboundCount: 2, withHandoff: false);
        await SeedConversationAsync("Tolu", inboundCount: 1, withHandoff: true);

        var result = await CreateService().ListForDashboardAsync(_shopId);

        Assert.True(result.IsSuccessful);
        Assert.Equal(2, result.Data!.Count);

        var tolu = result.Data!.Single(c => c.CustomerName == "Tolu");
        Assert.Equal(1, tolu.MessageCount);
        Assert.True(tolu.HasOpenHandoff);

        var ada = result.Data!.Single(c => c.CustomerName == "Ada");
        Assert.Equal(2, ada.MessageCount);
        Assert.False(ada.HasOpenHandoff);
    }

    [Fact]
    public async Task GetThread_ReturnsMessagesInOrderWithTraces()
    {
        var conversationId = await SeedConversationAsync("Tolu", inboundCount: 1, withHandoff: false);
        var outboundId = Guid.NewGuid();
        Db.Messages.Add(new Message
        {
            Id = outboundId, ShopId = _shopId, ConversationId = conversationId,
            Direction = MessageDirection.Outbound, Type = MessageType.Text, Text = "We have it in stock.",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
            ToolTraces =
            [
                new ToolTrace
                {
                    Id = Guid.NewGuid(), ShopId = _shopId, MessageId = outboundId,
                    ToolName = "SearchProducts", ArgumentsJson = """{"query":"sneakers"}""",
                    ResultJson = """{"count":2}""", DurationMs = 120, CreatedAt = DateTimeOffset.UtcNow,
                },
            ],
        });
        await Db.SaveChangesAsync();

        var result = await CreateService().GetThreadAsync(_shopId, conversationId);

        Assert.True(result.IsSuccessful);
        Assert.Equal("Tolu", result.Data!.CustomerName);
        Assert.Equal(2, result.Data!.Messages.Count);
        Assert.Equal(MessageDirection.Inbound, result.Data!.Messages[0].Direction);
        Assert.Equal(MessageDirection.Outbound, result.Data!.Messages[1].Direction);

        var trace = Assert.Single(result.Data!.Messages[1].ToolTraces);
        Assert.Equal("SearchProducts", trace.ToolName);
        Assert.Contains("sneakers", trace.ArgumentsJson);
    }

    [Fact]
    public async Task GetThread_MissingConversation_ReturnsNotFound()
    {
        ShopAccessor.SetShopId(_shopId);

        var result = await CreateService().GetThreadAsync(_shopId, Guid.NewGuid());

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }
}
