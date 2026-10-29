using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Tendaa.Application.Inbox;
using Tendaa.Application.WhatsApp;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Inbox;
using Tendaa.Infrastructure.Context;
using Tendaa.Web.HostedServices;

namespace Tendaa.Web.Tests;

public class WebhooksControllerTests
{
    [Fact]
    public async Task Verify_WithCorrectVerifyToken_ReturnsChallenge()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={WebhooksTestFactory.VerifyToken}&hub.challenge=abc123");

        response.EnsureSuccessStatusCode();
        Assert.Equal("abc123", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Verify_WithWrongVerifyToken_ReturnsForbidden()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            "/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong-token&hub.challenge=abc123");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Receive_WithMissingSignature_Returns401AndCreatesNoInboundEvent()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var body = BuildTextMessagePayload(bsuid: "US.1", phone: "16505551234", messageId: "wamid.1", text: "Hi");
        var response = await client.PostAsync("/webhooks/whatsapp", JsonContent(body));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountInboundEventsAsync(factory));
    }

    [Fact]
    public async Task Receive_WithTamperedSignature_Returns401AndCreatesNoInboundEvent()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var body = BuildTextMessagePayload(bsuid: "US.1", phone: "16505551234", messageId: "wamid.1", text: "Hi");
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = JsonContent(body) };
        request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", "sha256=" + new string('0', 64));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountInboundEventsAsync(factory));
    }

    [Fact]
    public async Task Receive_WithValidSignature_CreatesInboundEvent()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var body = BuildTextMessagePayload(bsuid: "US.1", phone: "16505551234", messageId: "wamid.1", text: "Hi");
        var response = await SendSignedAsync(client, body);

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, await CountInboundEventsAsync(factory));
    }

    [Fact]
    public async Task Receive_PayloadWithNoPhone_StillParsesAndCreatesInboundEvent()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        // No `wa_id`/`from` at all — phone is optional once a customer has a username (ARCHITECTURE.md
        // "Customer identity"). Only the BSUID is required.
        var body = BuildTextMessagePayload(bsuid: "US.2", phone: null, messageId: "wamid.2", text: "Hi");
        var response = await SendSignedAsync(client, body);

        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var inboundEvent = await db.InboundEvents.SingleAsync();
        Assert.Equal("US.2", inboundEvent.Bsuid);
    }

    [Fact]
    public async Task Receive_DuplicateMessageId_OnlyCreatesOneInboundEvent()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        var body = BuildTextMessagePayload(bsuid: "US.1", phone: "16505551234", messageId: "wamid.1", text: "Hi");

        (await SendSignedAsync(client, body)).EnsureSuccessStatusCode();
        (await SendSignedAsync(client, body)).EnsureSuccessStatusCode();

        Assert.Equal(1, await CountInboundEventsAsync(factory));
    }

    [Fact]
    public async Task Receive_UserChangedUserIdPayload_LinksExistingCustomer_NeverCreatesDuplicate()
    {
        await using var factory = new WebhooksTestFactory();
        await factory.InitializeDatabaseAsync();
        var client = factory.CreateClient();

        Guid shopId;
        using (var seedScope = factory.Services.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            var shop = await db.Shops.SingleAsync();
            shopId = shop.Id;
            db.Customers.Add(new Customer
            {
                Id = Guid.NewGuid(), ShopId = shopId, BsuId = "US.old", PhoneNumber = "16505551234",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var body = BuildBsuidChangePayload(previousBsuid: "US.old", newBsuid: "US.new", messageId: "wamid.sys.1");
        (await SendSignedAsync(client, body)).EnsureSuccessStatusCode();

        // This message type never calls the agent or WhatsApp sender — safe to run the real
        // processor directly (the hosted service itself stays removed in this factory, per its
        // own doc comment, specifically to keep every other test from risking a real API call).
        InboundEvent inboundEvent;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
            inboundEvent = await db.InboundEvents.SingleAsync();
        }

        var processor = new InboundEventProcessingService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new InboundEventChannel(),
            NullLogger<InboundEventProcessingService>.Instance);
        await processor.ProcessWithRetryAsync(inboundEvent, CancellationToken.None);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        var customerCount = await verifyDb.Customers.CountAsync();
        Assert.Equal(1, customerCount);

        var persisted = await verifyDb.Customers.SingleAsync();
        Assert.Equal("US.new", persisted.BsuId);
    }

    private static string BuildBsuidChangePayload(string previousBsuid, string newBsuid, string messageId)
    {
        var payload = new WhatsAppWebhookPayload(
            Entry:
            [
                new WhatsAppEntry(
                    Changes:
                    [
                        new WhatsAppChange(
                            Value: new WhatsAppChangeValue(
                                Metadata: new WhatsAppMetadata(PhoneNumberId: "1234567890"),
                                Contacts: [],
                                Messages:
                                [
                                    new WhatsAppMessage(
                                        messageId, From: null, FromUserId: null, "system", Text: null,
                                        System: new WhatsAppSystem(
                                            Type: "user_changed_user_id", WaId: "16505551234",
                                            UserId: newBsuid, PreviousUserId: previousBsuid)),
                                ])),
                    ]),
            ]);

        return JsonSerializer.Serialize(payload);
    }

    private static async Task<HttpResponseMessage> SendSignedAsync(HttpClient client, string body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/whatsapp") { Content = JsonContent(body) };
        request.Headers.TryAddWithoutValidation("X-Hub-Signature-256", ComputeSignature(body, WebhooksTestFactory.AppSecret));
        return await client.SendAsync(request);
    }

    private static async Task<int> CountInboundEventsAsync(WebhooksTestFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TendaaDbContext>();
        return await db.InboundEvents.CountAsync();
    }

    private static StringContent JsonContent(string body)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static string ComputeSignature(string rawBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildTextMessagePayload(string bsuid, string? phone, string messageId, string text)
    {
        var payload = new WhatsAppWebhookPayload(
            Entry:
            [
                new WhatsAppEntry(
                    Changes:
                    [
                        new WhatsAppChange(
                            Value: new WhatsAppChangeValue(
                                Metadata: new WhatsAppMetadata(PhoneNumberId: "1234567890"),
                                Contacts: [new WhatsAppContact(phone, bsuid, new WhatsAppProfile("Tolu", null))],
                                Messages:
                                [
                                    new WhatsAppMessage(
                                        messageId, phone, bsuid, "text", new WhatsAppTextBody(text), System: null),
                                ])),
                    ]),
            ]);

        return JsonSerializer.Serialize(payload);
    }
}
