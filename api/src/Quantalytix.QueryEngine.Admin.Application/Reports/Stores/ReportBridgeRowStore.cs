using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The bridge sources' rules as rows of a table keyed by source and data row (OI-49), stored as the mappings of each source's report
/// bridge and written only through the mappings module's <see cref="ReportBridges"/> (B9). Storing a row replaces that row's
/// mappings, as an insert into a primary-key table does; deleting a row removes them. Lists take a source table filter; a key or a
/// delete filter is a <see cref="ReportBridgeRow"/>, whose rules are ignored.
/// </summary>
/// <param name="sources">Reads each source's bridge.</param>
/// <param name="owned">Reads the bridges' live versions.</param>
/// <param name="mappings">Reads the bridges' mappings.</param>
/// <param name="bridges">Writes the mappings.</param>
public sealed class ReportBridgeRowStore(
    ITableGateway<ReportSourceRecord> sources,
    OwnedVersions owned,
    ITableGateway<BridgeMappingRecord> mappings,
    ReportBridges bridges) : ITableGateway<ReportBridgeRow>
{
    /// <summary>Lists the rule rows of the bridge sources a source table filter matches.</summary>
    /// <param name="filter">A source table filter.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One row per source and data row with rules.</returns>
    public async Task<IReadOnlyList<ReportBridgeRow>> ListAsync(object? filter, CancellationToken cancellationToken)
    {
        var sourceOf = (await owned.BridgesAsync(await sources.ListAsync(filter, cancellationToken), cancellationToken))
            .ToDictionary(pair => pair.Value.BridgeVersionId, pair => pair.Key);
        var entries = await mappings.ListAsync(new { BridgeVersionId = sourceOf.Keys.ToArray() }, cancellationToken);
        return
        [
            .. entries.GroupBy(entry => (Source: sourceOf[entry.BridgeVersionId], Row: entry.TargetMemberKey)).Select(row => new ReportBridgeRow
            {
                ReportSourceId = row.Key.Source,
                RowKey = row.Key.Row,
                Rules = [.. row.Select(entry => new ReportBridgeRule { ItemKey = entry.SourceMemberKey, Selection = entry.SourceSelection, Factor = entry.Factor })]
            })
        ];
    }

    /// <summary>Reads the rules of one source into one row.</summary>
    /// <param name="key">A <see cref="ReportBridgeRow"/> naming the source and row.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The row.</returns>
    public async Task<ReportBridgeRow> GetAsync(object key, CancellationToken cancellationToken)
    {
        var row = (ReportBridgeRow)key;
        return (await ListAsync(new { row.ReportSourceId }, cancellationToken)).Single(stored => stored.RowKey == row.RowKey);
    }

    /// <summary>Replaces the rules of one source into one row.</summary>
    /// <param name="record">The row's complete rules.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the rules are stored.</returns>
    public Task InsertAsync(ReportBridgeRow record, CancellationToken cancellationToken)
    {
        return InsertManyAsync([record], cancellationToken);
    }

    /// <summary>Replaces the rules of each row.</summary>
    /// <param name="records">The rows' complete rules.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the rules are stored.</returns>
    public async Task InsertManyAsync(IReadOnlyCollection<ReportBridgeRow> records, CancellationToken cancellationToken)
    {
        foreach (var record in records) await bridges.ReplaceRowAsync(await BridgeOfAsync(record, cancellationToken), record.RowKey, record.Rules, cancellationToken);
    }

    /// <summary>Replaces the rules of one row.</summary>
    /// <param name="record">The row's complete rules.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the rules are stored.</returns>
    public Task UpdateAsync(ReportBridgeRow record, CancellationToken cancellationToken)
    {
        return InsertManyAsync([record], cancellationToken);
    }

    /// <summary>Removes the rules of a source into a row the report no longer has.</summary>
    /// <param name="filter">A <see cref="ReportBridgeRow"/> naming the source and row.</param>
    /// <param name="cancellationToken">Cancels the reads and the delete.</param>
    /// <returns>A task that completes when the rules are deleted.</returns>
    public async Task DeleteAsync(object filter, CancellationToken cancellationToken)
    {
        var row = (ReportBridgeRow)filter;
        await bridges.RemoveRowsAsync(await BridgeOfAsync(row, cancellationToken), [row.RowKey], cancellationToken);
    }

    /// <summary>The bridge a row's source owns.</summary>
    private async Task<long> BridgeOfAsync(ReportBridgeRow row, CancellationToken cancellationToken)
    {
        return (await sources.GetAsync(new { row.ReportSourceId }, cancellationToken)).BridgeId!.Value;
    }
}
