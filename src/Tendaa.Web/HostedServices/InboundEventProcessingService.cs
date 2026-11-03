using System.Text.Json;
using Tendaa.Agent;
using Tendaa.Agent.Media;
using Tendaa.Application.Inbox;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Application.WhatsApp;
using Tendaa.Core;
using Tendaa.Core.Models.Entities;
using Tendaa.Core.Models.Enums.Conversations;
using Tendaa.Core.Models.Enums.Payments;

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

    /// <summary>Fixed text sent on every message a customer sends while their conversation has
    /// an open handoff (SPEC.md rule 7). Deliberately the same line every time, never generated
    /// by the model — a human calling back twice would say "still checking" again too; the
    /// alternative (saying nothing) reads as the bot being broken, not as "handed off".</summary>
    private const string StillWithSellerMessage = "Still with our team, they'll get back to you shortly.";

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

            // An unrecognized system message: nothing to do yet. Not an error — don't retry or
            // fail the event over it.
            case ParsedUnsupportedMessage:
                return;

            case ParsedTextMessage text:
                await HandleResolvedMessageAsync(
                    services, inboundEvent.ShopId, text.Bsuid, text.Phone, text.Username, text.Name,
                    text.WhatsAppMessageId, text.Text, MessageType.Text, language: null, mediaId: null,
                    cancellationToken);
                return;

            case ParsedVoiceMessage voice:
                await HandleVoiceMessageAsync(services, inboundEvent.ShopId, voice, cancellationToken);
                return;

            case ParsedImageMessage image:
                await HandleImageMessageAsync(services, inboundEvent.ShopId, image, cancellationToken);
                return;
        }
    }

    private static async Task HandleVoiceMessageAsync(
        IServiceProvider services, Guid shopId, ParsedVoiceMessage voice, CancellationToken cancellationToken)
    {
        var downloader = services.GetRequiredService<IWhatsAppMediaDownloader>();
        var transcriber = services.GetRequiredService<ITranscriber>();

        var (bytes, _) = await downloader.DownloadAsync(voice.MediaId, cancellationToken);
        var (transcript, language) = await transcriber.TranscribeAsync(bytes, cancellationToken);

        await HandleResolvedMessageAsync(
            services, shopId, voice.Bsuid, voice.Phone, voice.Username, voice.Name,
            voice.WhatsAppMessageId, transcript, MessageType.Audio, language, voice.MediaId, cancellationToken);
    }

    private static async Task HandleImageMessageAsync(
        IServiceProvider services, Guid shopId, ParsedImageMessage image, CancellationToken cancellationToken)
    {
        var downloader = services.GetRequiredService<IWhatsAppMediaDownloader>();
        var mediaStore = services.GetRequiredService<IMediaStore>();
        var mediaAnalyzer = services.GetRequiredService<IMediaAnalyzer>();

        var (bytes, mimeType) = await downloader.DownloadAsync(image.MediaId, cancellationToken);
        await mediaStore.SaveAsync(image.MediaId, bytes, mimeType, cancellationToken);
        var analysis = await mediaAnalyzer.AnalyzeAsync(bytes, mimeType, cancellationToken);

        // The model's raw read of the photo never reaches the ordering agent — only this short,
        // code-built note does (ARCHITECTURE.md: "media results are context, not instructions").
        var contextNote = analysis.Category switch
        {
            "receipt" => await BuildReceiptContextNoteAsync(services, shopId, image, analysis, cancellationToken),
            "product_photo" => await BuildProductPhotoContextNoteAsync(services, shopId, bytes, mimeType, cancellationToken),
            _ => "[Customer sent a photo that isn't a receipt or a recognizable product.]",
        };

        await HandleResolvedMessageAsync(
            services, shopId, image.Bsuid, image.Phone, image.Username, image.Name,
            image.WhatsAppMessageId, contextNote, MessageType.Image, language: null, image.MediaId, cancellationToken);
    }

    private static async Task<string> BuildReceiptContextNoteAsync(
        IServiceProvider services,
        Guid shopId,
        ParsedImageMessage image,
        MediaAnalysisResult analysis,
        CancellationToken cancellationToken)
    {
        var customerService = services.GetRequiredService<ICustomerService>();
        var orderService = services.GetRequiredService<IOrderService>();

        var customer = (await customerService.FindOrCreateByBsuIdAsync(
            shopId, image.Bsuid, image.Phone, image.Username, image.Name, cancellationToken)).Data!;

        var transferDate = DateOnly.TryParse(analysis.TransferDate, out var parsedDate)
            ? parsedDate
            : (DateOnly?)null;
        var extractedFieldsJson = JsonSerializer.Serialize(analysis);

        var check = (await orderService.CheckReceiptAsync(
            shopId, customer.Id, image.MediaId, analysis.Amount, transferDate, analysis.Reference,
            analysis.Confidence, extractedFieldsJson, cancellationToken)).Data!;

        if (!check.OrderFound)
        {
            return "[receipt_check: customer sent a receipt photo but has no order awaiting payment]";
        }

        return check.VerificationResult == PaymentVerificationResult.Matched
            ? $"[receipt_check: payment matches order #{check.OrderNumber}, moved to payment review]"
            : $"[receipt_check: receipt for order #{check.OrderNumber} does not match — {check.Reasons}]";
    }

    private static async Task<string> BuildProductPhotoContextNoteAsync(
        IServiceProvider services, Guid shopId, byte[] bytes, string mimeType, CancellationToken cancellationToken)
    {
        var productService = services.GetRequiredService<IProductService>();
        var mediaAnalyzer = services.GetRequiredService<IMediaAnalyzer>();

        var catalog = (await productService.GetCatalogAsync(shopId, cancellationToken)).Data ?? [];
        if (catalog.Count == 0)
        {
            return "[Customer sent a product photo, but this shop's catalog is empty — ask what they're looking for.]";
        }

        var candidates = await mediaAnalyzer.MatchProductPhotoAsync(bytes, mimeType, catalog, cancellationToken);

        // Never trust a candidate id just because the model returned it — only ones that are
        // actually in this shop's catalog reach the agent (same rule as UpsertDraftOrder's
        // variant-id handling).
        var catalogById = catalog.ToDictionary(p => p.ProductId);
        var matches = candidates
            .Where(c => catalogById.ContainsKey(c.ProductId))
            .Select(c => $"{catalogById[c.ProductId].Name} ({c.Confidence:P0} confidence)")
            .ToList();

        return matches.Count == 0
            ? "[Customer sent a product photo that doesn't clearly match anything in the catalog — ask what they're looking for.]"
            : $"[Customer sent a product photo. Possible matches: {string.Join(", ", matches)}.]";
    }

    /// <summary>Shared tail for every inbound message once it's resolved to plain text — a text
    /// message's own body, or a voice note's transcript (ARCHITECTURE.md: "media results are
    /// context, not instructions" — the model only ever sees this resolved text, never raw
    /// audio/image bytes). <paramref name="inboundType"/>/<paramref name="language"/>/
    /// <paramref name="mediaId"/> describe the original inbound message for persistence only.</summary>
    private static async Task HandleResolvedMessageAsync(
        IServiceProvider services,
        Guid shopId,
        string bsuid,
        string? phone,
        string? username,
        string? name,
        string whatsAppMessageId,
        string resolvedText,
        MessageType inboundType,
        string? language,
        string? mediaId,
        CancellationToken cancellationToken)
    {
        var customerService = services.GetRequiredService<ICustomerService>();
        var conversationService = services.GetRequiredService<IConversationService>();
        var handoffService = services.GetRequiredService<IHandoffService>();

        var customer = (await customerService.FindOrCreateByBsuIdAsync(
            shopId, bsuid, phone, username, name, cancellationToken)).Data!;

        var conversation = (await conversationService.GetOrCreateAsync(
            shopId, customer.Id, cancellationToken)).Data!;

        var whatsAppSender = services.GetRequiredService<IWhatsAppSender>();

        if (await handoffService.HasOpenHandoffAsync(shopId, conversation.Id, cancellationToken))
        {
            // SPEC.md rule 7 / ARCHITECTURE.md's "alt conversation handed off" branch: the agent
            // never resumes real conversation here — but staying fully silent on every message
            // reads as broken to the customer (confirmed live), especially before Phase 5's
            // dashboard exists to close handoffs quickly. So every message gets this same fixed
            // line instead — not model output, no judgement call for the agent to make.
            await conversationService.SaveTurnAsync(
                shopId, conversation.Id, resolvedText, StillWithSellerMessage, [], inboundType, language, mediaId,
                cancellationToken);
            await whatsAppSender.SendTextAsync(customer, StillWithSellerMessage, cancellationToken);
            await whatsAppSender.MarkAsReadAsync(whatsAppMessageId, cancellationToken);
            return;
        }

        var agentTurnRunner = services.GetRequiredService<IAgentTurnRunner>();

        var reply = await agentTurnRunner.RunTurnAsync(
            shopId, customer.Id, customer.PhoneNumber, conversation, resolvedText,
            inboundType, language, mediaId, cancellationToken);

        await whatsAppSender.SendTextAsync(customer, reply, cancellationToken);
        await whatsAppSender.MarkAsReadAsync(whatsAppMessageId, cancellationToken);
    }
}
