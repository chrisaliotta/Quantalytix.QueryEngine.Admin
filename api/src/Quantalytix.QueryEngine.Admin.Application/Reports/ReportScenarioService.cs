using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the scenarios of reports.</summary>
public sealed class ReportScenarioService(
    ITableGateway<ReportScenarioRecord> scenarios,
    ITableGateway<DimensionRoleRecord> roles,
    ReportChanges changes)
{
    /// <summary>Lists the roles that are a report's scenarios.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The roles by key, ignoring case.</returns>
    public async Task<IReadOnlyList<DimensionRoleRecord>> ListAsync(long reportId, CancellationToken cancellationToken)
    {
        var rows = await scenarios.ListAsync(new { ReportId = reportId }, cancellationToken);
        var named = await roles.ListAsync(new { DimensionRoleId = rows.Select(row => row.DimensionRoleId).ToArray() }, cancellationToken);
        return [.. named.OrderBy(role => role.DimensionRoleKey, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Replaces a report's scenarios with the given list.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete, validated scenario list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the list, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, ReportScenariosInput input, CancellationToken cancellationToken)
    {
        ReportScenarioRecord[] rows = [.. input.Scenarios.Select(scenario => new ReportScenarioRecord { ReportId = reportId, DimensionRoleId = scenario.DimensionRoleId })];
        return await changes.ApplyAsync(reportId, report => report with { Scenarios = rows }, async () =>
        {
            await scenarios.DeleteAsync(new { ReportId = reportId }, cancellationToken);
            await scenarios.InsertManyAsync(rows, cancellationToken);
        }, cancellationToken);
    }
}
