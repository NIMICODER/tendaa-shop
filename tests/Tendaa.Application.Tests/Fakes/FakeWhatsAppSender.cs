using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Application.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, matching the repo convention (Agent and Web
/// tests keep their own copies of this same shape).</summary>
public sealed class FakeWhatsAppSender : IWhatsAppSender
{
    public List<(Customer Customer, string Text)> SentTexts { get; } = [];

    public Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default)
    {
        SentTexts.Add((customer, text));
        return Task.CompletedTask;
    }

    public Task SendImageAsync(
        Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by Application service tests.");

    public Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by Application service tests.");
}
