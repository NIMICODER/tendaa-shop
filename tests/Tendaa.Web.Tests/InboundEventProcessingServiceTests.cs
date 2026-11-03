using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tendaa.Agent;
using Tendaa.Agent.Media;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Implementations;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Handoffs;
using Tendaa.Core.Models.Enums.Inbox;
using Tendaa.Core.Models.Enums.Orders;
using Tendaa.Core.Models.Enums.Payments;
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

    private const string RealisticAudioMessagePayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.voice-test"}],"messages":[{"from":"15550001234","from_user_id":"US.voice-test","id":"wamid.voice-test","timestamp":"1793298823","type":"audio","audio":{"id":"media-voice-1","mime_type":"audio/ogg; codecs=opus","voice":true}}]},"field":"messages"}]}]}
        """;

    private const string RealisticImageMessagePayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.image-test"}],"messages":[{"from":"15550001234","from_user_id":"US.image-test","id":"wamid.image-test","timestamp":"1793298823","type":"image","image":{"id":"media-image-1","mime_type":"image/jpeg","caption":"Paid"}}]},"field":"messages"}]}]}
        """;

    private const string RealisticProductPhotoPayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.product-photo-test"}],"messages":[{"from":"15550001234","from_user_id":"US.product-photo-test","id":"wamid.product-photo-test","timestamp":"1793298823","type":"image","image":{"id":"media-product-1","mime_type":"image/jpeg"}}]},"field":"messages"}]}]}
        """;

    private const string RealisticHandoffMessagePayload = """
        {"object":"whatsapp_business_account","entry":[{"id":"waba-1","changes":[{"value":{"messaging_product":"whatsapp","metadata":{"display_phone_number":"15551977073","phone_number_id":"1234567890"},"contacts":[{"profile":{"name":"Tolu"},"wa_id":"15550001234","user_id":"US.handoff-test"}],"messages":[{"from":"15550001234","from_user_id":"US.handoff-test","id":"wamid.handoff-test","timestamp":"1793298823","text":{"body":"Any update?"},"type":"text"}]},"field":"messages"}]}]}
        """;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    public readonly FakeWhatsAppSender WhatsAppSender = new();
    public readonly FakeAgentTurnRunner AgentTurnRunner = new("Hi Tolu, which product do you want?");
    public readonly FakeWhatsAppMediaDownloader MediaDownloader = new([1, 2, 3], "audio/ogg; codecs=opus");
    public readonly FakeTranscriber Transcriber = new("I want ten socks", "en");
    public readonly FakeMediaStore MediaStore = new();
    public readonly FakeMediaAnalyzer MediaAnalyzer = new();

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
        services.AddSingleton<IDashboardNotifier, FakeDashboardNotifier>();
        services.AddScoped<ICustomerMessenger, CustomerMessenger>();
        services.AddScoped<IDeliveryService, DeliveryService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddSingleton<IAgentTurnRunner>(AgentTurnRunner);
        services.AddSingleton<IWhatsAppSender>(WhatsAppSender);
        services.AddSingleton<IWhatsAppMediaDownloader>(MediaDownloader);
        services.AddSingleton<ITranscriber>(Transcriber);
        services.AddSingleton<IMediaStore>(MediaStore);
        services.AddSingleton<IMediaAnalyzer>(MediaAnalyzer);

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

    [Fact]
    public async Task ProcessWithRetryAsync_VoiceMessage_TranscribesAndRunsAgentTurnWithTranscript()
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

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.voice-test",
            Bsuid = "US.voice-test",
            RawPayloadJson = RealisticAudioMessagePayload,
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

        // The media id from the webhook flowed into the downloader, its bytes into the
        // transcriber, and the resulting transcript — not raw audio — into the agent turn.
        Assert.Equal("media-voice-1", MediaDownloader.LastRequestedMediaId);
        Assert.Equal("I want ten socks", AgentTurnRunner.LastMessageText);
        Assert.Equal(MessageType.Audio, AgentTurnRunner.LastInboundType);
        Assert.Single(WhatsAppSender.SentTexts);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var persisted = await verifyDb.InboundEvents.FirstAsync(e => e.Id == inboundEvent.Id);
        Assert.Equal(InboundEventStatus.Processed, persisted.Status);
    }

    [Fact]
    public async Task ProcessWithRetryAsync_ReceiptImage_MatchingReceipt_MovesOrderToPaymentReview()
    {
        var shopId = Guid.NewGuid();
        Guid customerId;
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

            scope.ServiceProvider.GetRequiredService<ICurrentShopAccessor>().SetShopId(shopId);
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            customerId = (await customerService.FindOrCreateByBsuIdAsync(
                shopId, "US.image-test", null, null, null, CancellationToken.None)).Data!.Id;

            db.Orders.Add(new Order
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                CustomerId = customerId,
                Number = 7,
                Status = OrderStatus.AwaitingPayment,
                GrandTotal = 37500m,
                AwaitingPaymentAt = DateTimeOffset.UtcNow.AddHours(-1),
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-2),
            });
            await db.SaveChangesAsync();
        }

        MediaAnalyzer.Result = new MediaAnalysisResult(
            "receipt", 37500m, "GTBank", "Tolu", DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"), "ref-999", 0.95);

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.image-test",
            Bsuid = "US.image-test",
            RawPayloadJson = RealisticImageMessagePayload,
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

        // Raw receipt bytes went to the media store and the analyzer, never to the agent — only
        // the code-built context note did.
        Assert.Single(MediaStore.Saved);
        Assert.Equal("media-image-1", MediaStore.Saved[0].MediaId);
        Assert.Contains("payment matches order #7", AgentTurnRunner.LastMessageText);
        Assert.Equal(MessageType.Image, AgentTurnRunner.LastInboundType);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var order = await verifyDb.Orders.FirstAsync(o => o.ShopId == shopId && o.CustomerId == customerId);
        Assert.Equal(OrderStatus.PaymentReview, order.Status);

        var proof = await verifyDb.PaymentProofs.FirstAsync(p => p.ShopId == shopId);
        Assert.Equal("media-image-1", proof.MediaId);
        Assert.Equal(PaymentVerificationResult.Matched, proof.VerificationResult);
    }

    [Fact]
    public async Task ProcessWithRetryAsync_ProductPhoto_MatchingCandidate_MentionsProductNameInAgentTurn()
    {
        var shopId = Guid.NewGuid();
        var productId = Guid.NewGuid();
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
            db.Products.Add(new Product
            {
                Id = productId,
                ShopId = shopId,
                Name = "Ankle Socks",
                Category = "Socks",
                Price = 1500m,
                Variants = [new ProductVariant { Id = Guid.NewGuid(), ShopId = shopId, ProductId = productId, Colour = "Black", Stock = 10 }],
            });
            await db.SaveChangesAsync();
        }

        MediaAnalyzer.Result = new MediaAnalysisResult("product_photo", null, null, null, null, null, 1.0);
        MediaAnalyzer.MatchCandidates = [new ProductPhotoMatchCandidate(productId, 0.9)];

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.product-photo-test",
            Bsuid = "US.product-photo-test",
            RawPayloadJson = RealisticProductPhotoPayload,
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

        Assert.Contains("Ankle Socks", AgentTurnRunner.LastMessageText);
        Assert.Equal(MessageType.Image, AgentTurnRunner.LastInboundType);
    }

    [Fact]
    public async Task ProcessWithRetryAsync_ProductPhoto_HallucinatedCandidateId_IsFilteredOut()
    {
        var shopId = Guid.NewGuid();
        var realProductId = Guid.NewGuid();
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
            db.Products.Add(new Product
            {
                Id = realProductId,
                ShopId = shopId,
                Name = "Ankle Socks",
                Category = "Socks",
                Price = 1500m,
                Variants = [],
            });
            await db.SaveChangesAsync();
        }

        MediaAnalyzer.Result = new MediaAnalysisResult("product_photo", null, null, null, null, null, 1.0);
        // Not in this shop's catalog at all — must never reach the agent turn (same rule as any
        // other model-supplied id in this codebase).
        MediaAnalyzer.MatchCandidates = [new ProductPhotoMatchCandidate(Guid.NewGuid(), 0.95)];

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.product-photo-test",
            Bsuid = "US.product-photo-test",
            RawPayloadJson = RealisticProductPhotoPayload,
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

        Assert.DoesNotContain("Ankle Socks", AgentTurnRunner.LastMessageText);
        Assert.Contains("doesn't clearly match", AgentTurnRunner.LastMessageText);
    }

    [Fact]
    public async Task ProcessWithRetryAsync_OpenHandoff_SendsFixedReplyInsteadOfSilence_NeverRunsAgent()
    {
        var shopId = Guid.NewGuid();
        Guid conversationId;
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

            scope.ServiceProvider.GetRequiredService<ICurrentShopAccessor>().SetShopId(shopId);
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var conversationService = scope.ServiceProvider.GetRequiredService<IConversationService>();
            var customerId = (await customerService.FindOrCreateByBsuIdAsync(
                shopId, "US.handoff-test", null, null, null, CancellationToken.None)).Data!.Id;
            conversationId = (await conversationService.GetOrCreateAsync(
                shopId, customerId, CancellationToken.None)).Data!.Id;

            db.Handoffs.Add(new Handoff
            {
                Id = Guid.NewGuid(),
                ShopId = shopId,
                ConversationId = conversationId,
                Reason = "cancel_request",
                Summary = "Customer wants to cancel an order.",
                Status = HandoffStatus.Open,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var inboundEvent = new InboundEvent
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            WhatsAppMessageId = "wamid.handoff-test",
            Bsuid = "US.handoff-test",
            RawPayloadJson = RealisticHandoffMessagePayload,
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

        // The agent/model never runs while handed off, but the customer still gets a reply —
        // not the old total silence.
        Assert.Equal(0, AgentTurnRunner.CallCount);
        var sentText = Assert.Single(WhatsAppSender.SentTexts);
        Assert.Equal("Still with our team, they'll get back to you shortly.", sentText.Text);

        using var verifyScope = _provider.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var outbound = await verifyDb.Messages
            .Where(m => m.ConversationId == conversationId && m.Direction == MessageDirection.Outbound)
            .ToListAsync();
        Assert.Single(outbound);
        Assert.Equal("Still with our team, they'll get back to you shortly.", outbound[0].Text);
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
