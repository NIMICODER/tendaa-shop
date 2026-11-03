namespace Tendaa.Evals;

/// <summary>One eval case: a short label for the report plus the async check itself. Cases run
/// sequentially against one shared <see cref="EvalFixture"/>, each under its own unique bsuId, so
/// a case never needs to clean up after itself.</summary>
public sealed record EvalCase(string Category, string Name, Func<EvalFixture, Task<EvalResult>> Run);

/// <summary><paramref name="Note"/> is the one line explaining why it passed or failed — always
/// filled in, pass or fail, so a judge reading the report understands the check without re-reading
/// the case code.</summary>
public sealed record EvalResult(bool Passed, string Note);
