using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// <c>formula_references_known_measures</c>: every measure a formula or compound rate names (<c>[m.key]</c>) is a measure of the version.
/// </summary>
public sealed class FormulaReferencesKnownMeasuresRule : MeasureRule
{
    /// <summary><c>formula_references_known_measures</c>.</summary>
    protected override string Code => "formula_references_known_measures";

    /// <summary>Reports every unknown measure reference.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per unknown reference.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var keys = snapshot.Measures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
        return ByKey(snapshot)
            .Where(measure => MeasureKinds.ComposesMeasures(measure.MeasureKind))
            .SelectMany(formula => ExpressionInspector.Inspect(formula.ExpressionText!).MeasureKeys
                .Where(key => !keys.Contains(key))
                .Select(key => Violation(formula, $"The expression of {formula.MeasureKind} measure '{formula.MeasureKey}' references measure '{key}', which is not a measure of this version.")));
    }
}
