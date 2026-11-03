using System.Text.Json;

namespace Tendaa.Evals;

public sealed record EvalReportEntry(string Category, string Name, bool Passed, string Note);

/// <summary>Runs every <see cref="EvalCase"/> sequentially against one shared
/// <see cref="EvalFixture"/>, prints a table, and writes a JSON report next to the executable so
/// a score can be quoted in the submission README without re-running the suite.</summary>
public static class EvalRunner
{
    public static async Task<int> RunAsync(IReadOnlyList<EvalCase> cases)
    {
        await using var fixture = await EvalFixture.CreateAsync();

        var entries = new List<EvalReportEntry>();
        foreach (var @case in cases)
        {
            EvalResult result;
            try
            {
                result = await @case.Run(fixture);
            }
            catch (Exception ex)
            {
                result = new EvalResult(false, $"threw {ex.GetType().Name}: {ex.Message}");
            }

            entries.Add(new EvalReportEntry(@case.Category, @case.Name, result.Passed, result.Note));

            Console.ForegroundColor = result.Passed ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine($"[{(result.Passed ? "PASS" : "FAIL")}] {@case.Category} / {@case.Name}");
            Console.ResetColor();
            Console.WriteLine($"       {result.Note}");
        }

        var passed = entries.Count(e => e.Passed);
        Console.WriteLine();
        Console.WriteLine($"{passed}/{entries.Count} eval cases passed.");

        var reportPath = Path.Combine(AppContext.BaseDirectory, "eval-report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report written to {reportPath}");

        return passed == entries.Count ? 0 : 1;
    }
}
