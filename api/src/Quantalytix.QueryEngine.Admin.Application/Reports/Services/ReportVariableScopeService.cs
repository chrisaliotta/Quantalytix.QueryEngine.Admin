using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the variable scopes of reports.</summary>
public sealed class ReportVariableScopeService(ITableGateway<ReportVariableScopeRecord> scopes, ReportChanges changes)
{
    /// <summary>Lists a report's scopes.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scopes by variable key.</returns>
    public async Task<IReadOnlyList<ReportVariableScopeRecord>> ListAsync(long reportId, CancellationToken cancellationToken)
    {
        var rows = await scopes.ListAsync(new { ReportId = reportId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VariableKey, StringComparer.Ordinal)];
    }

    /// <summary>Replaces a report's scopes with the given list.</summary>
    /// <param name="reportId">The report.</param>
    /// <param name="input">The complete, validated scope list.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The violations that kept an enabled report from taking the list, in which case nothing was written.</returns>
    public async Task<IReadOnlyList<RuleViolation>> ReplaceAsync(long reportId, ReportVariableScopesInput input, CancellationToken cancellationToken)
    {
        ReportVariableScopeRecord[] rows =
        [
            .. input.Scopes.OrderBy(scope => scope.VariableKey, StringComparer.Ordinal).Select(scope => new ReportVariableScopeRecord
            {
                ReportId = reportId,
                VariableKey = scope.VariableKey,
                ScopeMemberKey = scope.ScopeMemberKey,
                SelectionScope = scope.SelectionScope
            })
        ];
        return await changes.ApplyAsync(reportId, report => report with { Scopes = rows }, async () =>
        {
            await scopes.DeleteAsync(new { ReportId = reportId }, cancellationToken);
            await scopes.InsertManyAsync(rows, cancellationToken);
        }, cancellationToken);
    }
}
