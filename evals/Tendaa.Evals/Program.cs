using Tendaa.Evals;
using Tendaa.Evals.Cases;

List<EvalCase> cases =
[
    .. OrderingCases.All,
    .. PromptInjectionCases.All,
    .. LanguageCases.All,
    .. HandoffCases.All,
    .. ReceiptCases.All,
];

return await EvalRunner.RunAsync(cases);
