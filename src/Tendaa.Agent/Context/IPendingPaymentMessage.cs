namespace Tendaa.Agent.Context;

/// <summary>Scoped per-turn slot for the deterministic payment template a successful
/// <c>ConfirmOrderSummary</c> call must produce. The model's own reply text is never trusted to
/// carry the bank account number verbatim (SPEC.md: prices and payment details come only from the
/// database) — the tool sets this, and <c>AgentTurnRunner</c> appends it to the turn's reply after
/// <c>agent.RunAsync</c> completes, regardless of what the model said.</summary>
public interface IPendingPaymentMessage
{
    void Set(string text);

    string? TakeAndClear();
}
