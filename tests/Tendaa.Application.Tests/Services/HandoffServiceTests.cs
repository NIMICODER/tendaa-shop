using Microsoft.EntityFrameworkCore;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Tests.Fakes;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;

namespace Tendaa.Application.Tests.Services;

public class HandoffServiceTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly FakeDashboardNotifier _notifier = new();
    private readonly FakeCustomerMessenger _messenger = new();

    private HandoffService CreateService() => new(
        CreateRepository<Handoff>(),
        CreateRepository<Conversation>(),
        CreateRepository<Customer>(),
        CreateRepository<Message>(),
        CreateUnitOfWork(),
        _notifier,
        _messenger);

    private async Task<(Guid CustomerId, Guid ConversationId)> SeedConversationAsync(string customerName = "Tolu")
    {
        var customerId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        Db.Customers.Add(new Customer
        {
            Id = customerId, ShopId = _shopId, BsuId = $"bsuid-{customerId:N}",
            Name = customerName, PhoneNumber = "16505551234", CreatedAt = DateTimeOffset.UtcNow,
        });
        Db.Conversations.Add(new Conversation
        {
            Id = conversationId, ShopId = _shopId, CustomerId = customerId, CreatedAt = DateTimeOffset.UtcNow,
        });
        Db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(), ShopId = _shopId, ConversationId = conversationId,
            Direction = MessageDirection.Inbound, Type = MessageType.Text, Text = "I need help",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
        return (customerId, conversationId);
    }

    [Fact]
    public async Task RequestHandoffAsync_CreatesOpenHandoffAndNotifies()
    {
        var (_, conversationId) = await SeedConversationAsync();

        var result = await CreateService().RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");

        Assert.True(result.IsSuccessful);
        var persisted = await Db.Handoffs.SingleAsync();
        Assert.Equal(HandoffStatus.Open, persisted.Status);
        Assert.Equal("refund", persisted.Reason);
        Assert.Single(_notifier.HandoffsChangedShopIds, _shopId);
    }

    [Fact]
    public async Task RequestHandoffAsync_TwiceForSameConversation_CreatesOnlyOne()
    {
        var (_, conversationId) = await SeedConversationAsync();
        var service = CreateService();

        await service.RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");
        await service.RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");

        Assert.Equal(1, await Db.Handoffs.CountAsync());
    }

    [Fact]
    public async Task ResolveHandoff_WithReply_SendsPersistsMessageAndResolves()
    {
        var (customerId, conversationId) = await SeedConversationAsync();
        var service = CreateService();
        await service.RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");
        var handoffId = await Db.Handoffs.Select(h => h.Id).SingleAsync();

        var result = await service.ResolveHandoffAsync(_shopId, handoffId, "Seller", "We'll refund you today.");

        Assert.True(result.IsSuccessful);
        Assert.Equal(HandoffStatus.Resolved, result.Data!.Status);
        Assert.Equal("Seller", result.Data!.ResolvedBy);
        Assert.Equal("Tolu", result.Data!.CustomerName);

        var sent = Assert.Single(_messenger.Sent);
        Assert.Equal(customerId, sent.CustomerId);
        Assert.Equal("We'll refund you today.", sent.Text);

        var outbound = await Db.Messages.Where(m => m.Direction == MessageDirection.Outbound).SingleAsync();
        Assert.Equal("We'll refund you today.", outbound.Text);

        Assert.Contains("reply was sent", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task ResolveHandoff_OutsideWindow_ResolvesWithoutPersistingReply()
    {
        var (_, conversationId) = await SeedConversationAsync();
        _messenger.Result = false;
        var service = CreateService();
        await service.RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");
        var handoffId = await Db.Handoffs.Select(h => h.Id).SingleAsync();

        var result = await service.ResolveHandoffAsync(_shopId, handoffId, "Seller", "We'll refund you today.");

        Assert.True(result.IsSuccessful);
        Assert.Equal(HandoffStatus.Resolved, (await Db.Handoffs.SingleAsync()).Status);
        Assert.Empty(await Db.Messages.Where(m => m.Direction == MessageDirection.Outbound).ToListAsync());
        Assert.Contains("24-hour window", result.Message ?? string.Empty);
    }

    [Fact]
    public async Task ResolveHandoff_AlreadyResolved_ReturnsBadRequest()
    {
        var (_, conversationId) = await SeedConversationAsync();
        var service = CreateService();
        await service.RequestHandoffAsync(_shopId, conversationId, "refund", "Wants a refund.");
        var handoffId = await Db.Handoffs.Select(h => h.Id).SingleAsync();
        await service.ResolveHandoffAsync(_shopId, handoffId, "Seller", null);

        var result = await service.ResolveHandoffAsync(_shopId, handoffId, "Seller", null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        var (_, first) = await SeedConversationAsync("Ada");
        var (_, second) = await SeedConversationAsync("Tolu");
        var service = CreateService();
        await service.RequestHandoffAsync(_shopId, first, "refund", "Wants a refund.");
        await service.RequestHandoffAsync(_shopId, second, "sizing", "Needs sizing help.");
        var secondId = await Db.Handoffs.Where(h => h.ConversationId == second).Select(h => h.Id).SingleAsync();
        await service.ResolveHandoffAsync(_shopId, secondId, "Seller", null);

        var open = await service.ListAsync(_shopId, HandoffStatus.Open);

        Assert.True(open.IsSuccessful);
        var item = Assert.Single(open.Data!);
        Assert.Equal(first, item.ConversationId);
        Assert.Equal("Ada", item.CustomerName);
    }
}
