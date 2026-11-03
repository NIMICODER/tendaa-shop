using Microsoft.Extensions.DependencyInjection;
using Tendaa.Application.Services.Interfaces;

namespace Tendaa.Evals.Cases;

/// <summary>Exercises the real agent + real tools + real Muse Spark end to end — the core flow
/// judges will see in the demo. Each case uses its own bsuId so cases never share a conversation.</summary>
public static class OrderingCases
{
    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new EvalCase("Ordering", "single-item-order-reaches-awaiting-payment", async fixture =>
        {
            const string bsuId = "eval-order-single";
            await fixture.SendMessageAsync(bsuId, "hi, I want to order a graphic t-shirt, size M");
            await fixture.SendMessageAsync(bsuId, "my name is Ada, deliver to Lekki");
            var reply = await fixture.SendMessageAsync(bsuId, "yes that's correct, go ahead");

            var customer = await fixture.ResolveCustomerAsync(bsuId);
            var orders = await fixture.RunScopedAsync(sp =>
                sp.GetRequiredService<IOrderService>().GetMyOrdersAsync(fixture.ShopId, customer.Id));
            var latest = orders.Data!.OrderBy(o => o.Number).Last();

            var statusOk = latest.Status == "AwaitingPayment";
            var totalOk = latest.GrandTotal == 9500m; // 8000 item + 1500 Lekki delivery, from the DB, not the model
            var noPremature = !reply.Contains("confirmed", StringComparison.OrdinalIgnoreCase);

            return new EvalResult(statusOk && totalOk && noPremature,
                $"status={latest.Status} (want AwaitingPayment), total={latest.GrandTotal} (want 9500), " +
                $"final reply avoided 'confirmed': {noPremature}. Reply: \"{reply}\"");
        }),

        new EvalCase("Ordering", "price-and-stock-come-from-db-not-model", async fixture =>
        {
            const string bsuId = "eval-order-oos";
            // Classic Low Sneakers only stocks sizes 40-44 (DemoDataSeeder) - size 46 doesn't exist.
            var reply = await fixture.SendMessageAsync(bsuId, "do you have classic low sneakers in size 46?");

            // The real price (25000, formatted) must never appear attached to a claim that the
            // out-of-stock size is available - a loose heuristic, the reply itself is kept in the
            // report for a human to read too.
            var claimsAvailable = reply.Contains("yes", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("don't", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("not available", StringComparison.OrdinalIgnoreCase)
                && !reply.Contains("out of stock", StringComparison.OrdinalIgnoreCase);

            return new EvalResult(!claimsAvailable,
                $"agent must not claim an unstocked size is available. Reply: \"{reply}\"");
        }),

        new EvalCase("Ordering", "missing-field-asks-one-question-at-a-time", async fixture =>
        {
            const string bsuId = "eval-order-onequestion";
            var reply = await fixture.SendMessageAsync(bsuId, "I want a crossbody bag");

            // SPEC/AgentInstructions: ask for exactly one missing thing per message - a loose
            // heuristic (counting question marks) rather than a hard parse, kept honest by
            // printing the actual reply in the report.
            var questionMarks = reply.Count(c => c == '?');

            return new EvalResult(questionMarks <= 1,
                $"expected at most one question in the reply, found {questionMarks}. Reply: \"{reply}\"");
        }),
    ];
}
