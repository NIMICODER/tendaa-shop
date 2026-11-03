using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Evals;

/// <summary>Evals run the agent against a real model but never send anything over the real
/// WhatsApp API — these record what would have been sent so a case can assert on it without any
/// network call, same hand-written-fake-no-mocking-library convention as the test projects.</summary>
public sealed class NoOpWhatsAppSender : IWhatsAppSender
{
    public List<(Customer Customer, string Text)> SentTexts { get; } = [];

    public Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default)
    {
        SentTexts.Add((customer, text));
        return Task.CompletedTask;
    }

    public Task SendImageAsync(
        Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

public sealed class NoOpDashboardNotifier : IDashboardNotifier
{
    public Task NotifyOrdersChangedAsync(Guid shopId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task NotifyHandoffsChangedAsync(Guid shopId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
