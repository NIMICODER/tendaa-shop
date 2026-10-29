using Tendaa.Agent;
using Tendaa.Core.Models.Entities;

namespace Tendaa.Web.Tests.Fakes;

/// <summary>Stands in for the real agent (which needs a live Muse Spark connection) so the
/// processor's retry/error-handling logic can be tested without any network call. Records how
/// many times it was invoked, so a test can assert a send failure never re-runs the turn.</summary>
public sealed class FakeAgentTurnRunner(string reply) : IAgentTurnRunner
{
    public int CallCount { get; private set; }

    public Task<string> RunTurnAsync(
        Guid shopId,
        Guid customerId,
        string? phone,
        Conversation conversation,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(reply);
    }
}
