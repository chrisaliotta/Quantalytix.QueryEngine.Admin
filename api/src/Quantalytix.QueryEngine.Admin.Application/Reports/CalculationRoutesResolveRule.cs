using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>calculation_routes_resolve</c>: every route of a report's model over a dataset the release pins names an enabled field that
/// produces its calculation dimension there: for a semantic route, a canonical binding into the dimension's canonical dimension
/// that allows the grouping and filtering the calculation dimension does, and never a fan-out bridge for a filterable one; for a
/// field route, a field that allows them. Report versions, calculation model versions and semantic releases all run the rule,
/// each in the scope <see cref="ReportGraphReader"/> gives its kind.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports and the release's dataset versions.</param>
public sealed class CalculationRoutesResolveRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "calculation_routes_resolve";

    /// <summary>The binding kinds that resolve canonical members.</summary>
    private static readonly string[] Canonical = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Range, BindingKinds.Bridge];

    /// <summary>The kind the rule checks: report versions, calculation model versions or semantic releases.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports every route that does not produce its dimension.</summary>
    /// <param name="versionId">The version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var scope = await reader.ScopeAsync(Kind, versionId, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var report in scope.Reports)
        {
            foreach (var dimension in report.Dimensions.OrderBy(dimension => dimension.DimensionKey, StringComparer.Ordinal))
            {
                foreach (var route in report.Routes.Where(route => route.CalculationDimensionId == dimension.CalculationDimensionId && scope.Datasets.ContainsKey(route.DatasetId)))
                {
                    var dataset = scope.Datasets[route.DatasetId];
                    var subject = $"Report '{report.ReportKey}' dimension '{dimension.DimensionKey}' routes to '{route.FieldKey}' of '{dataset.DatasetKey}'";
                    if (dataset.Fields.GetValueOrDefault(route.FieldKey) is not { IsEnabled: true } field)
                    {
                        violations.Add(Violation($"{subject}, which is no enabled field."));
                        continue;
                    }

                    if (route.RouteKind == RouteKinds.Field)
                    {
                        if ((dimension.IsGroupable && !field.IsGroupable) || (dimension.IsFilterable && !field.IsFilterable))
                            violations.Add(Violation($"{subject}, which does not allow the grouping and filtering the dimension does."));
                        continue;
                    }

                    var binding = dataset.BindingOf(route.FieldKey);
                    if (binding is null || !Canonical.Contains(binding.BindingKind) || binding.DimensionId != dimension.DimensionId)
                    {
                        violations.Add(Violation($"{subject}, which has no canonical binding into the dimension's canonical dimension."));
                        continue;
                    }

                    if ((dimension.IsGroupable && !binding.IsGroupable) || (dimension.IsFilterable && !binding.IsFilterable))
                        violations.Add(Violation($"{subject}, whose binding does not allow the grouping and filtering the dimension does."));
                    if (dimension.IsFilterable && binding.BridgeId is { } bridge
                        && scope.BridgeCardinalities.GetValueOrDefault(bridge) is { } cardinality && cardinality != Cardinalities.ManyToOne)
                        violations.Add(Violation($"{subject}, whose bridge can send a row to several members, so it cannot be filtered on."));
                }
            }
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
