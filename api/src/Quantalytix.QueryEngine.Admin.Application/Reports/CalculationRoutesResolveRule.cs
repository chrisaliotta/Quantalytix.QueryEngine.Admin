using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>calculation_routes_resolve</c>: every route of a report over a dataset the release pins reads an enabled field there. A field
/// route's field allows the grouping and filtering its scalar calculation dimension does; a bridge route's field has a binding that
/// groups members of the bridge's source dimension, and a filterable calculation dimension never routes through a bridge that can
/// send a row to several members. Report versions and semantic releases both run the rule, each in the scope
/// <see cref="ReportGraphReader"/> gives its kind.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports and the release's dataset versions.</param>
public sealed class CalculationRoutesResolveRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "calculation_routes_resolve";

    /// <summary>The kind the rule checks: report versions or semantic releases.</summary>
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

                    if (route.RouteKind == RouteKinds.Field && ((dimension.IsGroupable && !field.IsGroupable) || (dimension.IsFilterable && !field.IsFilterable)))
                        violations.Add(Violation($"{subject}, which does not allow the grouping and filtering the dimension does."));
                    if (route.BridgeId is not { } bridgeId) continue;
                    if (!ClassificationFields.BridgesFrom(dataset, route.FieldKey, scope.Bridges[bridgeId]))
                        violations.Add(Violation($"{subject}, whose binding groups no members of the source dimension of bridge '{scope.Bridges[bridgeId].BridgeKey}'."));
                    if (dimension.IsFilterable && scope.BridgeCardinalities.GetValueOrDefault(bridgeId) is { } cardinality && cardinality != Cardinalities.ManyToOne)
                        violations.Add(Violation($"{subject} through a bridge that can send a row to several members, so it cannot be filtered on."));
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
