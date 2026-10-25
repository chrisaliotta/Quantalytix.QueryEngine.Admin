using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_sources_resolve</c>: every source of a report reads a dataset the release pins, and every key it names resolves in
/// that dataset version. A direct source's field is enabled, and a bridge source's role has an enabled binding into members, which
/// its bridge remaps; its time field has a time binding; and each variable it supplies reads an enabled measure through a filter
/// over enabled, filterable fields. The engine trusts these keys, so a key that does not resolve would fail the report at run time.
/// </summary>
public sealed class ReportSourcesResolveRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_sources_resolve";

    /// <summary>Reports every source key that does not resolve in the release.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the release's dataset versions.</param>
    /// <returns>One violation per unresolved key.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var variablesOf = report.SourceVariables.ToLookup(variable => variable.ReportSourceId);
        foreach (var source in report.Sources.OrderBy(source => source.SourceKey, StringComparer.Ordinal))
        {
            var subject = $"Report '{report.ReportKey}' source '{source.SourceKey}'";
            if (!scope.Datasets.TryGetValue(source.DatasetId, out var dataset))
            {
                yield return Violation($"{subject} reads dataset '{scope.DatasetKeys[source.DatasetId]}', which the release does not pin.");
                continue;
            }

            if (source.DimensionRoleId is { } dimensionRoleId)
            {
                if (!SourceBindings.BridgesFrom(dataset, dimensionRoleId))
                    yield return Violation($"{subject} bridges from role '{scope.RoleKeys[dimensionRoleId]}', which '{dataset.DatasetKey}' binds into no members with an enabled binding.");
            }
            else if (dataset.Fields.GetValueOrDefault(source.FieldKey!) is not { IsEnabled: true })
            {
                yield return Violation($"{subject} maps rows by '{source.FieldKey}', which is no enabled field of '{dataset.DatasetKey}'.");
            }
            if (!dataset.IsTimeField(source.TimeFieldKey))
                yield return Violation($"{subject}: time field '{source.TimeFieldKey}' of '{dataset.DatasetKey}' has no time binding.");
            foreach (var variable in variablesOf[source.ReportSourceId].OrderBy(variable => variable.VariableKey, StringComparer.Ordinal))
            {
                if (dataset.Measures.GetValueOrDefault(variable.MeasureKey) is not { IsEnabled: true })
                    yield return Violation($"{subject}: variable '{variable.VariableKey}' reads '{variable.MeasureKey}', which is no enabled measure of '{dataset.DatasetKey}'.");
                if (variable.SourceFilterJson is not { } filter) continue;
                foreach (var field in FilterNodes.Inspect(filter).Operands.Select(operand => operand.Id).Distinct(StringComparer.Ordinal)
                    .Where(key => dataset.Fields.GetValueOrDefault(key) is not { IsEnabled: true, IsFilterable: true }))
                    yield return Violation($"{subject}: variable '{variable.VariableKey}' filters on '{field}', which is no enabled, filterable field of '{dataset.DatasetKey}'.");
            }
        }
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
