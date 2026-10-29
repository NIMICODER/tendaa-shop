using Microsoft.Extensions.AI;

namespace Tendaa.Agent.Tests.Fakes;

/// <summary>A hand-written <see cref="IChatClient"/> that plays back a fixed script of responses,
/// one per call — standing in for the real Meta Model API so the tool-calling loop can be tested
/// without any network call. <see cref="Microsoft.Agents.AI.ChatClientAgent"/> wraps whatever
/// <see cref="IChatClient"/> it's given with its own function-invocation middleware, so this fake
/// only ever needs to answer "what would the model say next" — it never has to implement the
/// tool-calling loop itself.</summary>
public sealed class ScriptedChatClient(IReadOnlyList<ChatResponse> script) : IChatClient
{
    private int _callCount;

    public List<IReadOnlyList<ChatMessage>> ReceivedMessages { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Add([.. messages]);

        if (_callCount >= script.Count)
        {
            throw new InvalidOperationException(
                $"ScriptedChatClient received a {_callCount + 1}th call, but the script only has {script.Count} response(s).");
        }

        var response = script[_callCount];
        _callCount++;
        return Task.FromResult(response);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("ScriptedChatClient is non-streaming only — this test never calls RunStreamingAsync.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
