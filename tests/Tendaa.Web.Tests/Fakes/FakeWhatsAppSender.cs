using Tendaa.Core;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Hand-written fake — no mocking library, no real HTTP. Records every call so tests can
/// assert on it (e.g. "the reply was only sent once" for the dedupe test).</summary>
public sealed class FakeWhatsAppSender : IWhatsAppSender
{
    public List<(Customer Customer, string Text)> SentTexts { get; } = [];
    public List<(Customer Customer, string ImageUrl, string? Caption)> SentImages { get; } = [];
    public List<string> MarkedAsRead { get; } = [];

    /// <summary>When set, <see cref="SendTextAsync"/> throws this instead of recording the call —
    /// for tests of send-failure handling (e.g. the processor must not re-run the agent turn).</summary>
    public Exception? ThrowOnSendText { get; set; }

    public Task SendTextAsync(Customer customer, string text, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSendText is { } exception)
        {
            throw exception;
        }

        SentTexts.Add((customer, text));
        return Task.CompletedTask;
    }

    public Task SendImageAsync(
        Customer customer, string imageUrl, string? caption, CancellationToken cancellationToken = default)
    {
        SentImages.Add((customer, imageUrl, caption));
        return Task.CompletedTask;
    }

    public Task MarkAsReadAsync(string whatsAppMessageId, CancellationToken cancellationToken = default)
    {
        MarkedAsRead.Add(whatsAppMessageId);
        return Task.CompletedTask;
    }
}
