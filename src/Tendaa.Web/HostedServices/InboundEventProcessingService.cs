using Tendaa.Agent;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Application.WhatsApp;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Web.HostedServices;

/// <summary>Drains <see cref="InboundEventChannel"/>, one worker per partition — ARCHITECTURE.md's
/// sequence diagram "Processor". On startup, requeues events left <c>Pending</c> by a previous
/// run that never finished (crash recovery). Each event gets its own DI scope, since all the
/// services it touches are scoped (EF Core's unit-of-work lifetime).</summary>
public sealed class InboundEventProcessingService(
    IServiceScopeFactory scopeFactory,
    InboundEventChannel channel,
    ILogger<InboundEventProcessingService> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingEventsAsync(stoppingToken);

        var workers = Enumerable.Range(0, InboundEventChannel.PartitionCount)
            .Select(partition => RunPartitionWorkerAsync(partition, stoppingToken));

        await Task.WhenAll(workers);
    }

    internal async Task RequeuePendingEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var inboundEventService = scope.ServiceProvider.GetRequiredService<IInboundEventService>();

        var pending = await inboundEventService.GetPendingAsync(cancellationToken);
        foreach (var inboundEvent in pending)
        {
            channel.TryEnqueue(inboundEvent);
        }

        if (pending.Count > 0)
        {
            logger.LogInformation("Re-queued {Count} pending inbound event(s) on startup.", pending.Count);
        }
    }

    private async Task RunPartitionWorkerAsync(int partition, CancellationToken stoppingToken)
    {
        await foreach (var inboundEvent in channel.Reader(partition).ReadAllAsync(stoppingToken))
        {
            await ProcessWithRetryAsync(inboundEvent, stoppingToken);
        }
    }

    internal async Task ProcessWithRetryAsync(InboundEvent inboundEvent, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var scope = scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                await HandleEventAsync(services, inboundEvent, cancellationToken);
                await services.GetRequiredService<IInboundEventService>()
                    .MarkProcessedAsync(inboundEvent.Id, cancellationToken);
                return;
            }
            catch (WhatsAppApiException ex)
            {
                // The agent already ran and the reply is already persisted by this point — only
                // the WhatsApp send itself failed. Retrying the whole event would re-run the agent
                // and duplicate the Message rows for no benefit, so this fails the event outright
                // on the first attempt instead of looping, recording Meta's real error.
                logger.LogError(
                    ex, "WhatsApp send failed for inbound event {EventId}: {Error}", inboundEvent.Id, ex.Message);

                await services.GetRequiredService<IInboundEventService>()
                    .RecordFailedAttemptAsync(inboundEvent.Id, FormatError(ex), maxAttempts: 0, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "Error processing inbound event {EventId}, attempt {Attempt}.", inboundEvent.Id, attempt);

                var shouldRetry = await services.GetRequiredService<IInboundEventService>()
                    .RecordFailedAttemptAsync(inboundEvent.Id, ex.Message, MaxAttempts, cancellationToken);

                // Retry immediately within this same partition's loop — never skip ahead to the
                // next event for this customer, so per-customer ordering is preserved even while
                // retrying. After MaxAttempts, give up and move on so one stuck message can't
                // block that customer's queue forever.
                if (!shouldRetry)
                {
                    return;
                }
            }
        }
    }

    private static string FormatError(WhatsAppApiException ex) =>
        ex.ErrorCode is { } code
            ? $"WhatsApp send failed: {code} {ex.ErrorMessage}"
            : $"WhatsApp send failed: {ex.Message}";

    private static async Task HandleEventAsync(
        IServiceProvider services, InboundEvent inboundEvent, CancellationToken cancellationToken)
    {
        services.GetRequiredService<ICurrentShopAccessor>().SetShopId(inboundEvent.ShopId);

        var parsed = WhatsAppWebhookParser.Parse(inboundEvent.RawPayloadJson);
        var message = parsed.Messages.FirstOrDefault(m => m.WhatsAppMessageId == inboundEvent.WhatsAppMessageId);
        if (message is null)
        {
            return;
        }

        var customerService = services.GetRequiredService<ICustomerService>();

        switch (message)
        {
            case ParsedBsuidChangeMessage change:
                await customerService.LinkBsuIdChangeAsync(
                    inboundEvent.ShopId, change.PreviousBsuid, change.Bsuid, change.NewPhone, cancellationToken);
                return;

            // Voice/image: Phase 4a/4b's job. An unrecognized system message: nothing to do yet.
            // Neither is an error — don't retry or fail the event over it.
            case ParsedUnsupportedMessage:
                return;

            case ParsedTextMessage text:
                await HandleTextMessageAsync(services, inboundEvent.ShopId, text, cancellationToken);
                return;
        }
    }

    private static async Task HandleTextMessageAsync(
        IServiceProvider services, Guid shopId, ParsedTextMessage text, CancellationToken cancellationToken)
    {
        var customerService = services.GetRequiredService<ICustomerService>();
        var conversationService = services.GetRequiredService<IConversationService>();
        var handoffService = services.GetRequiredService<IHandoffService>();

        var customer = (await customerService.FindOrCreateByBsuIdAsync(
            shopId, text.Bsuid, text.Phone, text.Username, text.Name, cancellationToken)).Data!;

        var conversation = (await conversationService.GetOrCreateAsync(
            shopId, customer.Id, cancellationToken)).Data!;

        if (await handoffService.HasOpenHandoffAsync(shopId, conversation.Id, cancellationToken))
        {
            // SPEC.md rule 7 / ARCHITECTURE.md's "alt conversation handed off" branch: store the
            // message, never call the agent. "Seller notified" is Phase 5's SignalR hub — a log
            // line stands in for now so the gap isn't silently dropped.
            await conversationService.SaveTurnAsync(shopId, conversation.Id, text.Text, null, [], cancellationToken);
            return;
        }

        var agentTurnRunner = services.GetRequiredService<IAgentTurnRunner>();
        var whatsAppSender = services.GetRequiredService<IWhatsAppSender>();

        var reply = await agentTurnRunner.RunTurnAsync(
            shopId, customer.Id, customer.PhoneNumber, conversation, text.Text, cancellationToken);

        await whatsAppSender.SendTextAsync(customer, reply, cancellationToken);
        await whatsAppSender.MarkAsReadAsync(text.WhatsAppMessageId, cancellationToken);
    }
}
