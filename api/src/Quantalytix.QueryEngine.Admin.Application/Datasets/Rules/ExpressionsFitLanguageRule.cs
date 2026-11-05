using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Domain.Expressions;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary>
/// <c>expressions_fit_language</c>: the version's field expressions, measure row filters, measure formulas and compound rates use
/// only what its expression language has. <c>MONTHS_BETWEEN</c> and <c>POWER</c> need language 1.1; a formula's period context,
/// <c>@periodDays</c>, <c>@yearDays</c> and <c>YEARFRAC</c>, needs 1.2. The language can change after an expression is written,
/// so the rule checks every expression on every write.
/// </summary>
public sealed class ExpressionsFitLanguageRule : DatasetRule
{
    /// <summary>The functions that not every language has, as the engine spells them, with the version that adds each.</summary>
    private static readonly (ExpressionFunction Function, string Name, string Version)[] VersionedFunctions =
    [
        (ExpressionFunction.MonthsBetween, "MONTHS_BETWEEN", ExpressionLanguageVersions.Version11),
        (ExpressionFunction.Power, "POWER", ExpressionLanguageVersions.Version11),
        (ExpressionFunction.YearFrac, "YEARFRAC", ExpressionLanguageVersions.Version12)
    ];

    /// <summary><c>expressions_fit_language</c>.</summary>
    protected override string Code => "expressions_fit_language";

    /// <summary>Reports each use of something the version's language lacks, once per expression.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per expression and use.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var version = snapshot.Version.ExpressionLanguageVersion;
        var fields = snapshot.Fields
            .Where(field => field.FieldSourceKind == FieldSourceKinds.Expression)
            .OrderBy(field => field.FieldKey, StringComparer.Ordinal)
            .SelectMany(field => Lacking(field.ExpressionText!, version).Select(use =>
                Violation(DatasetTabs.Fields, $"Expression field '{field.FieldKey}' {Lacks(use, version)}", field.FieldId, field.FieldKey)));
        var measures = snapshot.Measures.OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal).ToList();
        var rowFilters = measures
            .Where(measure => measure.RowFilterExpression is not null)
            .SelectMany(measure => Lacking(measure.RowFilterExpression!, version).Select(use =>
                Violation(DatasetTabs.Measures, $"The row filter of measure '{measure.MeasureKey}' {Lacks(use, version)}", measure.MeasureId, measure.MeasureKey)));
        var expressions = measures
            .Where(measure => MeasureKinds.ComposesMeasures(measure.MeasureKind))
            .SelectMany(measure => Lacking(measure.ExpressionText!, version).Select(use =>
                Violation(DatasetTabs.Measures, $"The {ExpressionOf(measure)} of measure '{measure.MeasureKey}' {Lacks(use, version)}", measure.MeasureId, measure.MeasureKey)));
        return fields.Concat(rowFilters).Concat(expressions);
    }

    /// <summary>What an expression uses that the language lacks, each with the version that adds it.</summary>
    private static IEnumerable<(string What, string Version)> Lacking(string expression, string version)
    {
        var inspection = ExpressionInspector.Inspect(expression);
        return VersionedFunctions
            .Where(item => inspection.Functions.Contains(item.Function))
            .Select(item => (What: $"calls {item.Name}", item.Version))
            .Concat(inspection.ParameterNames.Where(FormulaContext.ParameterNames.Contains).Select(name => (What: $"reads @{name}", Version: ExpressionLanguageVersions.Version12)))
            .Where(use => !ExpressionLanguageVersions.Includes(version, use.Version));
    }

    /// <summary>What a measure's expression is: a formula, or a compound measure's per-step rate.</summary>
    private static string ExpressionOf(MeasureRecord measure)
    {
        return measure.MeasureKind == MeasureKinds.Formula ? "formula" : "rate";
    }

    private static string Lacks((string What, string Version) use, string version)
    {
        return $"{use.What}, which expression language {version} lacks; the version needs expression language {use.Version}.";
    }
}
