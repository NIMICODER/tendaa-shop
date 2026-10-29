using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tendaa.Agent;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Inbox;
using Tendaa.Infrastructure;
using Tendaa.Infrastructure.Context;
using Tendaa.Infrastructure.Data.Implementations;
using Tendaa.Infrastructure.Data.Interfaces;
using Tendaa.Web.HostedServices;
using Tendaa.Web.Tests.Fakes;

namespace Tendaa.Web.Tests;

/// <summary>Tests the startup "requeue Pending events" step directly, rather than spinning up
/// the full hosted-service lifecycle — faster and deterministic, and it's the one piece of crash
/// recovery (PLAN.md's "killing the app after a 200 and restarting still answers the message")
/// that can be verified without an actual process restart.</summary>
public sealed class InboundEventProcessingServiceTests : IDisposable
{
    private const string RealisticTextMessagePayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.send-failure-test"}],"messages":[{"from":"15550001234","from_user_id":"US.send-failure-test","id":"wamid.send-failure-test","timestamp":"1793298823","text":{"body":"Hello"},"type":"text"}]},"field":"messages"}]}]}
        """;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    public readonly FakeWhatsAppSender WhatsAppSender = new();
    public readonly FakeAgentTurnRunner AgentTurnRunner = new("Hi Tolu, which product do you want?");

    public InboundEventProcessingServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddSingleton<ICurrentShopAccessor, CurrentShopAccessor>();
        services.AddDbContext<TendaaDbContext>(options => options.UseSqlite(_connection));
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IInboundEventService, InboundEventService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IHandoffService, HandoffService>();
        services.AddSingleton<IAgentTurnRunner>(AgentTurnRunner);
        services.AddSingleton<IWhatsAppSender>(WhatsAppSender);

        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TendaaDbContext>().Database.EnsureCreated();
    }

    [Fact]
    public async Task RequeuePendingEventsAsync_EnqueuesEventsStoredAsPending()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            db.InboundEvents.Add(new InboundEvent
            {
                Id = Guid.NewGuid(),
                ShopId = Guid.NewGuid(),
                WhatsAppMessageId = "wamid.1",
                Bsuid = "US.1",
                RawPayloadJson = "{}",
                Status = InboundEventStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var channel = new InboundEventChannel();
        var service = new InboundEventProcessingService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            channel,
            NullLogger<InboundEventProcessingService>.Instance);

        await service.RequeuePendingEventsAsync(CancellationToken.None);

        var reader = channel.Reader(InboundEventChannel.GetPartition("US.1"));
        Assert.True(reader.TryRead(out var requeued));
        Assert.Equal("wamid.1", requeued!.WhatsAppMessageId);
    }

    [Fact]
    public async Task RequeuePendingEventsAsync_DoesNotRequeueAlreadyProcessedEvents()
    {
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            db.InboundEvents.Add(new InboundEvent
            {
                Id = Guid.NewGuid(),
                ShopId = Guid.NewGuid(),
                WhatsAppMessageId = "wamid.done",
                Bsuid = "US.2",
                RawPayloadJson = "{}",
                Status = InboundEventStatus.Processed,
                CreatedAt = DateTimeOffset.UtcNow,
                ProcessedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var channel = new InboundEventChannel();
        var service = new InboundEventProcessingService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            channel,
            NullLogger<InboundEventProcessingService>.Instance);

        await service.RequeuePendingEventsAsync(CancellationToken.None);

        var reader = channel.Reader(InboundEventChannel.GetPartition("US.2"));
        Assert.False(reader.TryRead(out _));
    }

    [Fact]
    public async Task ProcessWithRetryAsync_SendFails_MarksFailedImmediately_DoesNotRerunAgent()
    {
        var shopId = Guid.NewGuid();
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            db.Shops.Add(new Shop
            {
                Id = shopId,
                Name = "Test Shop",
                Country = "Nigeria",
                Currency = "NGN",
                TimeZone = "Africa/Lagos",
                WhatsAppPhoneNumberId = "1234567890",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        WhatsAppSender.ThrowOnSendText = new WhatsAppApiException(
            System.Net.HttpStatusCode.BadRequest, 131030, "Recipient phone number not in allowed list", "{}");

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.send-failure-test",
            Bsuid = "US.send-failure-test",
            RawPayloadJson = RealisticTextMessagePayload,
            Status = InboundEventStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        using (var scope = _provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            db.InboundEvents.Add(inboundEvent);
            await db.SaveChangesAsync();
        }

        var service = new InboundEventProcessingService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            new InboundEventChannel(),
            NullLogger<InboundEventProcessingService>.Instance);

        await service.ProcessWithRetryAsync(inboundEvent, CancellationToken.None);

        // The real bug: a send failure used to retry the whole event, re-running the agent turn
        // (and so re-persisting messages) up to 3 times. CallCount == 1 proves that no longer
        // happens — the turn ran once, only the (failed) send was the actual failure.
        Assert.Equal(1, AgentTurnRunner.CallCount);
        Assert.Empty(WhatsAppSender.SentTexts);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var persisted = await verifyDb.InboundEvents.FirstAsync(e => e.Id == inboundEvent.Id);
        Assert.Equal(InboundEventStatus.Failed, persisted.Status);
        Assert.Equal(1, persisted.Attempts);
        Assert.Contains("131030", persisted.Error);
        Assert.Contains("not in allowed list", persisted.Error);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
