using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>component_roles_match_kind</c> (D7): a measure's component roles are the ones its kind reads.</summary>
public sealed class ComponentRolesMatchKindRule : MeasureRule
{
    /// <summary><c>component_roles_match_kind</c>.</summary>
    protected override string Code => "component_roles_match_kind";

    /// <summary>Reports every missing or unexpected role.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per problem.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var roles = snapshot.Components.ToLookup(component => component.MeasureId, component => component.ComponentRole);
        return ByKey(snapshot).SelectMany(measure => MeasureComponentRoles.Problems(measure.MeasureKind, measure.AggregationFunction, roles[measure.MeasureId])
            .Select(problem => Violation(measure, $"Measure '{measure.MeasureKey}': {problem}")));
    }
}
