using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the variable scopes of report versions.</summary>
public sealed class ReportVariableScopeService(ITableGateway<ReportVariableScopeRecord> scopes)
{
    /// <summary>Lists a version's scopes.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The scopes by variable key.</returns>
    public async Task<IReadOnlyList<ReportVariableScopeRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await scopes.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VariableKey, StringComparer.Ordinal)];
    }

    /// <summary>Replaces a version's scopes with the given list.</summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete, validated scope list.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored scopes by variable key.</returns>
    public async Task<IReadOnlyList<ReportVariableScopeRecord>> ReplaceAsync(long versionId, ReportVariableScopesInput input, CancellationToken cancellationToken)
    {
        ReportVariableScopeRecord[] rows =
        [
            .. input.Scopes.OrderBy(scope => scope.VariableKey, StringComparer.Ordinal).Select(scope => new ReportVariableScopeRecord
            {
                ReportVersionId = versionId,
                VariableKey = scope.VariableKey,
                ScopeMemberKey = scope.ScopeMemberKey,
                SelectionScope = scope.SelectionScope
            })
        ];
        await scopes.DeleteAsync(new { ReportVersionId = versionId }, cancellationToken);
        await scopes.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}
