using Tendaa.Application.Models;

namespace Tendaa.Agent.Tracing;

/// <summary>Scoped, per-turn log of tool calls. Microsoft.Agents.AI 1.26 has no public tool-call
/// middleware hook (confirmed via af-api-check — only internal-looking
/// <c>FunctionInvocationDelegatingAgent</c> plumbing), so each <c>OrderingTools</c> method records
/// its own call here instead of relying on framework interception.</summary>
public interface IToolTraceCollector
{
    IReadOnlyList<ToolTraceEntry> Entries { get; }

    void Record(string toolName, string argumentsJson, string resultJson, int durationMs);
}
