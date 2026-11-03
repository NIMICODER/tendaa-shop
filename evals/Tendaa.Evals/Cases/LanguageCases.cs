using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace Tendaa.Evals.Cases;

/// <summary>Tests the one rule a deterministic check can't verify (did the reply actually read as
/// natural Nigerian Pidgin, not just "contains a Pidgin word") — an LLM-as-judge case, per
/// PLAN.md's "LLM-as-judge only where needed." Uses the same singleton <see cref="ChatClient"/>
/// as the ordering agent (see <c>AgentServiceCollectionExtensions.AddTendaaAgent</c>) for the
/// judge call too, so this is still one real Meta API client, not a second one.</summary>
public static class LanguageCases
{
    public static IReadOnlyList<EvalCase> All { get; } =
    [
        new EvalCase("Language", "replies-in-pidgin-when-addressed-in-pidgin", async fixture =>
        {
            const string bsuId = "eval-language-pidgin";
            var reply = await fixture.SendMessageAsync(bsuId,
                "Abeg I wan buy one graphic t-shirt, how much e cost?");

            var verdict = await fixture.RunScopedAsync(async sp =>
            {
                var chat = sp.GetRequiredService<ChatClient>();
                var judgePrompt = $"""
                    A customer wrote to a shop assistant in Nigerian Pidgin: "Abeg I wan buy one
                    graphic t-shirt, how much e cost?"

                    The assistant replied: "{reply}"

                    Does the reply read as natural Nigerian Pidgin (or at least not jarringly
                    formal/robotic English that ignores the customer's register)? Answer with
                    exactly one word: YES or NO.
                    """;
                var completion = await chat.CompleteChatAsync(judgePrompt);
                return completion.Value.Content[0].Text.Trim();
            });

            var passed = verdict.StartsWith("YES", StringComparison.OrdinalIgnoreCase);
            return new EvalResult(passed,
                $"LLM judge verdict: {verdict}. Agent reply: \"{reply}\"");
        }),
    ];
}
