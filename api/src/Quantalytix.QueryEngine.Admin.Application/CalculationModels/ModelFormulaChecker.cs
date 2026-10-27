using System.Text.RegularExpressions;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// Checks one formula against a saved model as the formula editor types it (OI-43): its syntax with the query engine's
/// <see cref="ExpressionParser"/>, then the model rules a formula answers for alone (C2-C4): the metrics it reads resolve, a
/// calculated measure does not read itself, the parameters it reads are declared and read where their types belong, and the model's
/// formula language has what it uses. A reference's error covers its text; the others cover the whole formula.
/// </summary>
public static class ModelFormulaChecker
{
    /// <summary>Checks a formula.</summary>
    /// <param name="model">The model as saved.</param>
    /// <param name="formula">The formula.</param>
    /// <param name="selfKey">The metric that owns the formula; null for a new metric.</param>
    /// <returns>The errors, the metrics and parameters the formula reads, and the language it needs.</returns>
    public static ModelFormulaCheck Check(ModelGraph model, string formula, string? selfKey)
    {
        var inspection = ExpressionInspector.Inspect(formula);
        if (!inspection.IsValid)
        {
            var at = Math.Min(inspection.Position!.Value, formula.Length);
            return new ModelFormulaCheck
            {
                Errors = [new ModelFormulaError { Message = inspection.Error!, Start = at, End = Math.Min(at + 1, formula.Length) }],
                Reads = [],
                Parameters = [],
                RequiresLanguage = FormulaLanguageVersions.Version10
            };
        }

        var expression = ExpressionParser.Parse(formula);
        var parameters = FormulaParametersResolveRule.Declared(model.Parameters);
        var declared = parameters.Keys.ToHashSet(StringComparer.Ordinal);
        var uses = FormulasFitLanguageRule.Uses(inspection, declared).ToList();
        var language = model.Model.FormulaLanguageVersion;
        var metricKeys = model.MetricKeys;
        List<ModelFormulaError> errors =
        [
            .. inspection.FieldKeys.Count + inspection.DimensionKeys.Count > 0
                ? [Whole(formula, "A model formula reads variables and calculated measures through [m.KEY], not dataset fields or dimension members.")]
                : Array.Empty<ModelFormulaError>(),
            .. inspection.MeasureKeys.Where(key => !metricKeys.Contains(key))
                .SelectMany(key => At(formula, Reference(key), $"[m.{key}] is no variable or calculated measure of the model.")),
            .. SelfReads(model, inspection.MeasureKeys, selfKey)
                .SelectMany(key => At(formula, Reference(key), key == selfKey ? "A calculated measure cannot read itself." : $"[m.{key}] reads this calculated measure, so the formula would read itself.")),
            .. FormulaParametersResolveRule.Undeclared(expression, parameters).Distinct(StringComparer.Ordinal)
                .SelectMany(name => At(formula, Parameter(name), $"The formula {FormulaParametersResolveRule.UndeclaredProblem(name)}.")),
            .. FormulaParametersResolveRule.Misplaced(expression, parameters).Distinct(StringComparer.Ordinal)
                .Select(problem => Whole(formula, $"The formula {problem}.")),
            .. uses.Where(use => !FormulaLanguageVersions.Includes(language, use.Version))
                .Select(use => Whole(formula, $"The formula {FormulasFitLanguageRule.Lacks(use, language)}."))
        ];

        return new ModelFormulaCheck
        {
            Errors = errors,
            Reads = inspection.MeasureKeys,
            Parameters = [.. inspection.ParameterNames.Where(declared.Contains)],
            RequiresLanguage = uses.Aggregate(FormulaLanguageVersions.Version10, (needed, use) => FormulaLanguageVersions.Includes(needed, use.Version) ? needed : use.Version)
        };
    }

    /// <summary>
    /// The metrics the formula reads that lead back to the calculated measure that owns it, directly or through the saved formulas of
    /// other calculated measures. A variable's attribution expression may read anything, so a variable never reads itself.
    /// </summary>
    private static IEnumerable<string> SelfReads(ModelGraph model, IReadOnlyList<string> reads, string? selfKey)
    {
        if (selfKey is null || model.VariableKeys.Contains(selfKey)) return [];
        var formulas = model.CalculatedMeasures.ToDictionary(
            measure => measure.MeasureKey, measure => ExpressionInspector.Inspect(measure.ExpressionText).MeasureKeys, StringComparer.Ordinal);
        formulas[selfKey] = reads;
        return reads.Where(key => key == selfKey || (formulas.ContainsKey(key) && FormulasAcyclicRule.Reaches(key, selfKey, formulas, [])));
    }

    /// <summary>An error over each occurrence of a reference in the formula.</summary>
    private static IEnumerable<ModelFormulaError> At(string formula, Regex reference, string message)
    {
        return reference.Matches(formula).Select(match => new ModelFormulaError { Message = message, Start = match.Index, End = match.Index + match.Length });
    }

    /// <summary>An error over the whole formula.</summary>
    private static ModelFormulaError Whole(string formula, string message)
    {
        return new ModelFormulaError { Message = message, Start = 0, End = formula.Length };
    }

    /// <summary>The text of a <c>[m.KEY]</c> reference.</summary>
    private static Regex Reference(string key)
    {
        return new Regex(Regex.Escape($"[m.{key}]"), RegexOptions.CultureInvariant);
    }

    /// <summary>The text of an <c>@name</c> reference, which ends at the first character a name cannot hold.</summary>
    private static Regex Parameter(string name)
    {
        return new Regex($"@{Regex.Escape(name)}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant);
    }
}
