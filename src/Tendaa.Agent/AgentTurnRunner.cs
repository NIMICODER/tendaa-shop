using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;
using Tendaa.Agent.Context;
using Tendaa.Agent.Tracing;
using Tendaa.Application.Services.Interfaces;
using Tendaa.Core.Configurations.ConfigModels;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Agent;

public sealed class AgentTurnRunner(
    AIAgent agent,
    IAgentRequestContext requestContext,
    IToolTraceCollector trace,
    IPendingPaymentMessage pendingPayment,
    IConversationService conversationService,
    IOptions<AgentOptions> agentOptions) : IAgentTurnRunner
{
    public async Task<string> RunTurnAsync(
        Guid shopId,
        Guid customerId,
        string? phone,
        Conversation conversation,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        requestContext.SetIdentity(shopId, customerId, phone, conversation.Id);

        // A first-time conversation (SessionJson null) already gets a fresh session via
        // LoadSessionAsync's own null-check below. This only short-circuits *that* for a
        // returning customer whose conversation has gone stale — their next message starts a
        // new session instead of replaying a long-abandoned transcript. Order state lives in the
        // database, not the session, so nothing is lost; AgentInstructions tells the model to
        // check GetMyOrders/GetCustomerProfile at the start of an empty conversation either way.
        var isStale = conversation.LastMessageAt is { } last
            && DateTimeOffset.UtcNow - last > TimeSpan.FromHours(agentOptions.Value.StaleConversationHours);

        var session = isStale
            ? await agent.CreateSessionAsync(cancellationToken)
            : await conversationService.LoadSessionAsync(agent, conversation, cancellationToken);

        var response = await agent.RunAsync(messageText, session, cancellationToken: cancellationToken);

        // AgentResponse.Text concatenates every TextContent across every message in this run,
        // including any pre-tool-call narration ("let me check...") — only the final assistant
        // message is the actual reply to send.
        var replyText = response.Messages.Count > 0 ? response.Messages[^1].Text : response.Text;

        var paymentMessage = pendingPayment.TakeAndClear();
        if (paymentMessage is not null)
        {
            replyText = string.IsNullOrWhiteSpace(replyText)
                ? paymentMessage
                : $"{replyText.TrimEnd()}\n\n{paymentMessage}";
        }

        await conversationService.SaveSessionAsync(agent, conversation, session, cancellationToken);
        await conversationService.SaveTurnAsync(
            shopId, conversation.Id, messageText, replyText, trace.Entries, cancellationToken);

        return replyText;
    }
}
