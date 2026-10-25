using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formulas_fit_language</c>: the model's formulas and attribution expressions use only what its formula language has. <c>POWER</c>
/// needs language 1.1; <c>YEARFRAC</c>, string literals and the model's declared parameters need 1.2. <c>@periodDays</c> and
/// <c>@yearDays</c> are in every language. The engine evaluates a model's formulas in the model's language, and the language can
/// change after a formula is written, so the rule checks every formula on every write.
/// </summary>
public static class FormulasFitLanguageRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formulas_fit_language";

    /// <summary>Reports each use of something the model's formula language lacks, once per formula.</summary>
    /// <param name="model">The model, its metrics, attributions and parameters.</param>
    /// <returns>One violation per formula and missing feature.</returns>
    public static IEnumerable<RuleViolation> Evaluate(ModelGraph model)
    {
        var version = model.Model.FormulaLanguageVersion;
        var declared = model.Parameters.Select(parameter => parameter.ParameterKey).ToHashSet(StringComparer.Ordinal);
        return model.Formulas
            .OrderBy(formula => formula.Subject, StringComparer.Ordinal)
            .SelectMany(formula => Uses(ExpressionInspector.Inspect(formula.Expression), declared)
                .Where(use => !FormulaLanguageVersions.Includes(version, use.Version))
                .Select(use => new RuleViolation
                {
                    Rule = Code,
                    Message = $"{formula.Subject} {use.What}, which formula language {version} lacks; the model needs formula language {use.Version}."
                }));
    }

    /// <summary>What a formula uses that not every language has, with the version that adds it.</summary>
    private static IEnumerable<(string What, string Version)> Uses(ExpressionInspection inspection, IReadOnlySet<string> declared)
    {
        if (inspection.Functions.Contains(ExpressionFunction.Power)) yield return ("calls POWER", FormulaLanguageVersions.Version11);
        if (inspection.Functions.Contains(ExpressionFunction.YearFrac)) yield return ("calls YEARFRAC", FormulaLanguageVersions.Version12);
        if (inspection.TextLiterals.Count > 0) yield return ("reads string literals", FormulaLanguageVersions.Version12);
        foreach (var name in inspection.ParameterNames.Where(declared.Contains))
            yield return ($"reads parameter @{name}", FormulaLanguageVersions.Version12);
    }
}
