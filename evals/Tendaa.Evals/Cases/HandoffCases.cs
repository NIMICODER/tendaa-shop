using Microsoft.Extensions.DependencyInjection;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Evals.Cases;

/// <summary>AgentInstructions "Handoffs": complaints/refunds/"let me speak to a person" must call
/// RequestHandoff, not be answered by the agent. Asserts on the real DB row the tool call
/// produced, not just reply wording - the tool call itself is the thing that matters (it silences
/// the agent on this conversation per SPEC.md rule 7).</summary>
public static class HandoffCases
{
    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new EvalCase("Handoff", "angry-refund-request-opens-handoff", async fixture =>
        {
            const string bsuId = "eval-handoff-refund";
            var reply = await fixture.SendMessageAsync(bsuId,
                "This is unacceptable, the shoes I got last week fell apart after two days. " +
                "I want a refund right now and I want to speak to a real person.");

            var conversationId = await fixture.GetConversationIdAsync(bsuId);
            var hasHandoff = await fixture.RunScopedAsync(sp =>
                sp.GetRequiredService<IHandoffService>().HasOpenHandoffAsync(fixture.ShopId, conversationId));

            var noPromise = !reply.Contains("refund", StringComparison.OrdinalIgnoreCase)
                || reply.Contains("seller", StringComparison.OrdinalIgnoreCase)
                || reply.Contains("team", StringComparison.OrdinalIgnoreCase);

            return new EvalResult(hasHandoff,
                $"expected an open Handoff row after a refund complaint, found={hasHandoff}. " +
                $"Reply should defer to the seller rather than promise the refund itself ({noPromise}). " +
                $"Reply: \"{reply}\"");
        }),
    ];
}
