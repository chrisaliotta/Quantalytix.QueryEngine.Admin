using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>scenario_roles_distinct</c>: no enabled calculation dimension of a report reads a role that is one of its scenarios, because a
/// run names one member of each scenario and reads it alone, so its members are never broken out or filtered by.
/// </summary>
public sealed class ScenarioRolesDistinctRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "scenario_roles_distinct";

    /// <summary>Reports each enabled calculation dimension that reads a scenario's role.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Holds the keys of the roles the calculation dimensions read.</param>
    /// <returns>One violation per calculation dimension on a scenario's role.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        var scenarioRoles = report.Scenarios.Select(scenario => scenario.DimensionRoleId).ToHashSet();
        var reading = report.EnabledDimensions
            .Where(dimension => scenarioRoles.Contains(dimension.DimensionRoleId))
            .OrderBy(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        foreach (var dimension in reading)
        {
            yield return new RuleViolation
            {
                Code = Code,
                Message = $"Report '{report.ReportKey}' reads role '{scope.RoleKeys[dimension.DimensionRoleId]}' as a scenario and through calculation dimension " +
                    $"'{dimension.DimensionKey}'; a scenario's member is chosen per run, never broken out or filtered by.",
                Target = ReportAreas.Target(ReportAreas.Dimensions, dimension.DimensionKey)
            };
        }
    }
}
