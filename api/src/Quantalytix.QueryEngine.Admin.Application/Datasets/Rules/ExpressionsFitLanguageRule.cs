using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>expressions_fit_language</c>: the version's field expressions and measure row filters use only what its expression language
/// has. <c>MONTHS_BETWEEN</c> needs language 1.1. The language can change after an expression is written, so the rule checks every
/// expression on every write.
/// </summary>
public sealed class ExpressionsFitLanguageRule : DatasetRule
{
    /// <summary><c>expressions_fit_language</c>.</summary>
    protected override string Code => "expressions_fit_language";

    /// <summary>Reports each field expression and row filter that calls MONTHS_BETWEEN on a version of language 1.0.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per expression.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var version = snapshot.Version.ExpressionLanguageVersion;
        if (version != ExpressionLanguageVersions.Version10) return [];
        var lacks = $"calls MONTHS_BETWEEN, which expression language {version} lacks; the version needs expression language {ExpressionLanguageVersions.Version11}.";
        var fields = snapshot.Fields
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression && CallsMonthsBetween(field.ExpressionText!))
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .Select(field => Violation(DatasetTabs.Fields, $"Expression field '{field.FieldKey}' {lacks}", field.FieldId, field.FieldKey));
        var rowFilters = snapshot.Measures
            .Where(measure => measure.RowFilterExpression is { } rowFilter && CallsMonthsBetween(rowFilter))
            .OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal)
            .Select(measure => Violation(DatasetTabs.Measures, $"The row filter of measure '{measure.MeasureKey}' {lacks}", measure.MeasureId, measure.MeasureKey));
        return fields.Concat(rowFilters);
    }

    private static bool CallsMonthsBetween(string expression)
    {
        return ExpressionInspector.Inspect(expression).Functions.Contains(ExpressionFunction.MonthsBetween);
    }
}
