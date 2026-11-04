using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>expressions_fit_language</c>: the version's field expressions, measure row filters and measure formulas use only what its
/// expression language has. <c>MONTHS_BETWEEN</c> and <c>POWER</c> need language 1.1. The language can change after an expression
/// is written, so the rule checks every expression on every write.
/// </summary>
public sealed class ExpressionsFitLanguageRule : DatasetRule
{
    /// <summary>The functions expression language 1.1 adds, as the engine spells them.</summary>
    private static readonly (ExpressionFunction Function, string Name)[] Version11Functions =
        [(ExpressionFunction.MonthsBetween, "MONTHS_BETWEEN"), (ExpressionFunction.Power, "POWER")];

    /// <summary><c>expressions_fit_language</c>.</summary>
    protected override string Code => "expressions_fit_language";

    /// <summary>Reports each field expression, row filter and formula that calls a language 1.1 function on a version of language 1.0.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per expression and function.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var version = snapshot.Version.ExpressionLanguageVersion;
        if (version != ExpressionLanguageVersions.Version10) return [];
        var fields = snapshot.Fields
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression)
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .SelectMany(field => Lacking(field.ExpressionText!).Select(name =>
                Violation(DatasetTabs.Fields, $"Expression field '{field.FieldKey}' {Lacks(name, version)}", field.FieldId, field.FieldKey)));
        var measures = snapshot.Measures.OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal).ToList();
        var rowFilters = measures
            .Where(measure => measure.RowFilterExpression is not null)
            .SelectMany(measure => Lacking(measure.RowFilterExpression!).Select(name =>
                Violation(DatasetTabs.Measures, $"The row filter of measure '{measure.MeasureKey}' {Lacks(name, version)}", measure.MeasureId, measure.MeasureKey)));
        var formulas = measures
            .Where(measure => measure.MeasureKind == MeasureKinds.Formula)
            .SelectMany(measure => Lacking(measure.ExpressionText!).Select(name =>
                Violation(DatasetTabs.Measures, $"The formula of measure '{measure.MeasureKey}' {Lacks(name, version)}", measure.MeasureId, measure.MeasureKey)));
        return fields.Concat(rowFilters).Concat(formulas);
    }

    private static IEnumerable<string> Lacking(string expression)
    {
        var functions = ExpressionInspector.Inspect(expression).Functions;
        return Version11Functions.Where(item => functions.Contains(item.Function)).Select(item => item.Name);
    }

    private static string Lacks(string function, string version)
    {
        return $"calls {function}, which expression language {version} lacks; the version needs expression language {ExpressionLanguageVersions.Version11}.";
    }
}
