using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Tests.Fakes;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;

namespace Tendaa.Application.Tests.Services;

public class CustomerMessengerTests : SqliteApplicationTestBase
{
    private readonly Guid _shopId = Guid.NewGuid();
    private readonly Guid _customerId = Guid.NewGuid();
    private readonly FakeWhatsAppSender _sender = new();

    private CustomerMessenger CreateService() => new(
        CreateRepository<Customer>(),
        CreateRepository<Conversation>(),
        CreateRepository<Message>(),
        _sender);

    private async Task SeedCustomerAsync()
    {
        Db.Customers.Add(new Customer
        {
            Id = _customerId, ShopId = _shopId, BsuId = "bsuid-1", PhoneNumber = "16505551234",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();
        ShopAccessor.SetShopId(_shopId);
    }

    private async Task AddInboundMessageAsync(DateTimeOffset createdAt)
    {
        var conversationId = Guid.NewGuid();
        Db.Conversations.Add(new Conversation
        {
            Id = conversationId, ShopId = _shopId, CustomerId = _customerId, CreatedAt = createdAt,
        });
        Db.Messages.Add(new Message
        {
            Id = Guid.NewGuid(), ShopId = _shopId, ConversationId = conversationId,
            Direction = MessageDirection.Inbound, Type = MessageType.Text, Text = "hi", CreatedAt = createdAt,
        });
        await Db.SaveChangesAsync();
    }

    [Fact]
    public async Task TrySend_RecentInbound_SendsAndReturnsTrue()
    {
        await SeedCustomerAsync();
        await AddInboundMessageAsync(DateTimeOffset.UtcNow.AddHours(-1));

        var sent = await CreateService().TrySendAsync(_shopId, _customerId, "hello");

        Assert.True(sent);
        var recorded = Assert.Single(_sender.SentTexts);
        Assert.Equal("hello", recorded.Text);
        Assert.Equal(_customerId, recorded.Customer.Id);
    }

    [Fact]
    public async Task TrySend_StaleInbound_SkipsAndReturnsFalse()
    {
        await SeedCustomerAsync();
        await AddInboundMessageAsync(DateTimeOffset.UtcNow.AddHours(-25));

        var sent = await CreateService().TrySendAsync(_shopId, _customerId, "hello");

        Assert.False(sent);
        Assert.Empty(_sender.SentTexts);
    }

    [Fact]
    public async Task TrySend_NoInboundHistory_SkipsAndReturnsFalse()
    {
        await SeedCustomerAsync();

        var sent = await CreateService().TrySendAsync(_shopId, _customerId, "hello");

        Assert.False(sent);
        Assert.Empty(_sender.SentTexts);
    }

    [Fact]
    public async Task TrySend_UnknownCustomer_ReturnsFalse()
    {
        ShopAccessor.SetShopId(_shopId);

        var sent = await CreateService().TrySendAsync(_shopId, Guid.NewGuid(), "hello");

        Assert.False(sent);
        Assert.Empty(_sender.SentTexts);
    }
}
