namespace Tendaa.Application.Models;

/// <summary><see cref="PaymentMessage"/> is the exact, deterministic text the customer must
/// receive — built in code from the shop record, never typed or paraphrased by the model (SPEC.md:
/// the agent can move an order to AwaitingPayment, never claim a payment is confirmed). The caller
/// appends it to the model's reply verbatim; see <c>AgentTurnRunner</c>.</summary>
public sealed record ConfirmOrderResult(
    int Number,
    decimal GrandTotal,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    string FormattedGrandTotal,
    string PaymentMessage);
