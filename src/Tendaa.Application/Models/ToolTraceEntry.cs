namespace Tendaa.Application.Models;

public sealed record ToolTraceEntry(string ToolName, string ArgumentsJson, string ResultJson, int DurationMs);
