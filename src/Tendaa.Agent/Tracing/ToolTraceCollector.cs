using Tendaa.Application.Models;

namespace Tendaa.Agent.Tracing;

/// <summary>Mutable default implementation of <see cref="IToolTraceCollector"/>. Registered
/// scoped so one turn's recorded calls never leak into the next.</summary>
public sealed class ToolTraceCollector : IToolTraceCollector
{
    private readonly List<ToolTraceEntry> _entries = [];

    public IReadOnlyList<ToolTraceEntry> Entries => _entries;

    public void Record(string toolName, string argumentsJson, string resultJson, int durationMs) =>
        _entries.Add(new ToolTraceEntry(toolName, argumentsJson, resultJson, durationMs));
}
