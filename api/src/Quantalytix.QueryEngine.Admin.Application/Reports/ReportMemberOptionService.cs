using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>Reads and replaces the member options of report versions.</summary>
public sealed class ReportMemberOptionService(ITableGateway<ReportMemberOptionRecord> options)
{
    /// <summary>Lists a version's member options.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The options by member key.</returns>
    public async Task<IReadOnlyList<ReportMemberOptionRecord>> ListAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await options.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.MemberKey, StringComparer.Ordinal)];
    }

    /// <summary>Replaces a version's member options with the given list.</summary>
    /// <param name="versionId">The draft or published report version.</param>
    /// <param name="input">The complete, validated option list.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>The stored options by member key.</returns>
    public async Task<IReadOnlyList<ReportMemberOptionRecord>> ReplaceAsync(long versionId, ReportMemberOptionsInput input, CancellationToken cancellationToken)
    {
        ReportMemberOptionRecord[] rows =
        [
            .. input.Options.OrderBy(option => option.MemberKey, StringComparer.Ordinal).Select(option => new ReportMemberOptionRecord
            {
                ReportVersionId = versionId,
                MemberKey = option.MemberKey,
                DisplayName = option.DisplayName,
                IsVisible = option.IsVisible,
                ShowsValues = option.ShowsValues
            })
        ];
        await options.DeleteAsync(new { ReportVersionId = versionId }, cancellationToken);
        await options.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}
