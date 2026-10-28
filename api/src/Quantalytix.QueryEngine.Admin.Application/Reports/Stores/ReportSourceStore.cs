using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// The report source table as a report commit writes it (P7, B9): a bridge source's row comes and goes with its own report bridge,
/// written only through the mappings module's <see cref="ReportBridges"/>. Storing a new bridge source first creates its bridge
/// (live version 1 into the report dimension) and stores the row with the bridge's id; a renamed source renames its bridge; a
/// deleted source's row goes before its bridge. A write that stops between the two leaves at most a bridge no source owns, which the
/// engine never reads and the bridges module may delete.
/// </summary>
/// <param name="sources">The report source table.</param>
/// <param name="reports">Reads a new source's report.</param>
/// <param name="roles">Reads a new bridge source's role.</param>
/// <param name="bridges">Writes the report bridges.</param>
public sealed class ReportSourceStore(
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<ReportRecord> reports,
    ITableGateway<DimensionRoleRecord> roles,
    ReportBridges bridges) : ITableGateway<ReportSourceRecord>
{
    /// <summary>Lists the source rows that match a filter.</summary>
    /// <param name="filter">A source table filter.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The rows.</returns>
    public Task<IReadOnlyList<ReportSourceRecord>> ListAsync(object? filter, CancellationToken cancellationToken)
    {
        return sources.ListAsync(filter, cancellationToken);
    }

    /// <summary>Reads one source row.</summary>
    /// <param name="key">The row's key.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The row.</returns>
    public Task<ReportSourceRecord> GetAsync(object key, CancellationToken cancellationToken)
    {
        return sources.GetAsync(key, cancellationToken);
    }

    /// <summary>Stores a new source, and first its bridge when it is a bridge source.</summary>
    /// <param name="record">The new source.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the source is stored.</returns>
    public Task InsertAsync(ReportSourceRecord record, CancellationToken cancellationToken)
    {
        return InsertManyAsync([record], cancellationToken);
    }

    /// <summary>Stores new sources, and first each bridge source's bridge.</summary>
    /// <param name="records">The new sources.</param>
    /// <param name="cancellationToken">Cancels the writes.</param>
    /// <returns>A task that completes when the sources are stored.</returns>
    public async Task InsertManyAsync(IReadOnlyCollection<ReportSourceRecord> records, CancellationToken cancellationToken)
    {
        var stored = new List<ReportSourceRecord>();
        foreach (var record in records)
            stored.Add(record.DimensionRoleId is { } roleId ? record with { BridgeId = await CreateBridgeAsync(record, roleId, cancellationToken) } : record);
        await sources.InsertManyAsync(stored, cancellationToken);
    }

    /// <summary>Stores a source's new name and time field; a bridge source's bridge takes the new name.</summary>
    /// <param name="record">The source as it is to be stored.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>A task that completes when the source is stored.</returns>
    public async Task UpdateAsync(ReportSourceRecord record, CancellationToken cancellationToken)
    {
        var stored = await sources.GetAsync(new { record.ReportSourceId }, cancellationToken);
        await sources.UpdateAsync(record, cancellationToken);
        if (record.BridgeId is { } bridgeId && record.DisplayName != stored.DisplayName) await bridges.RenameAsync(bridgeId, record.DisplayName, cancellationToken);
    }

    /// <summary>Deletes the sources a filter matches, then their bridges.</summary>
    /// <param name="filter">A source table filter.</param>
    /// <param name="cancellationToken">Cancels the reads and deletes.</param>
    /// <returns>A task that completes when the sources and their bridges are gone.</returns>
    public async Task DeleteAsync(object filter, CancellationToken cancellationToken)
    {
        var removed = await sources.ListAsync(filter, cancellationToken);
        await sources.DeleteAsync(filter, cancellationToken);
        foreach (var bridgeId in removed.Select(source => source.BridgeId).OfType<long>()) await bridges.DeleteAsync(bridgeId, cancellationToken);
    }

    /// <summary>Creates a bridge source's bridge from its role's dimension into the report dimension, keyed by the report and source.</summary>
    private async Task<long> CreateBridgeAsync(ReportSourceRecord source, long roleId, CancellationToken cancellationToken)
    {
        var report = await reports.GetAsync(new { source.ReportId }, cancellationToken);
        var role = await roles.GetAsync(new { DimensionRoleId = roleId }, cancellationToken);
        return await bridges.CreateAsync(
            new ReportBridgeInput
            {
                Key = ReportSourceKeys.Bridge(report.ReportKey, source.SourceKey),
                DisplayName = source.DisplayName,
                SourceDimensionId = role.DimensionId,
                ReportDimensionId = report.ReportDimensionId
            },
            cancellationToken);
    }
}
