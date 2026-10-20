using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the variable targets of report versions.</summary>
public sealed class ReportVariableTargetService(ITableGateway<ReportVariableTargetRecord> targets)
{
    /// <summary>Lists a version's targets.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The targets by variable key.</returns>
    public async Task<IReadOnlyList<ReportVariableTargetRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await targets.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.VariableKey, StringComparer.Ordinal)];
    }

    /// <summary>Replaces a version's targets with the given list.</summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete, validated target list.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored targets by variable key.</returns>
    public async Task<IReadOnlyList<ReportVariableTargetRecord>> ReplaceAsync(long versionId, ReportVariableTargetsInput input, CancellationToken cancellationToken)
    {
        ReportVariableTargetRecord[] rows =
        [
            .. input.Targets.OrderBy(target => target.VariableKey, StringComparer.Ordinal).Select(target => new ReportVariableTargetRecord
            {
                ReportVersionId = versionId,
                VariableKey = target.VariableKey,
                TargetMemberKey = target.TargetMemberKey,
                SelectionScope = target.SelectionScope
            })
        ];
        await targets.DeleteAsync(new { ReportVersionId = versionId }, cancellationToken);
        await targets.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}
