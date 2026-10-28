using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Reads the identities assumption sets pin, each with its live and frozen versions (OI-14) and the report that owns its dimension.
/// An identity is listed once it has a version; it was created with its first.
/// </summary>
public sealed class PinIdentityReader(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<MappingVersionRecord> mappingVersions,
    ITableGateway<BridgeRecord> bridges,
    ITableGateway<BridgeVersionRecord> bridgeVersions,
    ITableGateway<ReportRecord> reports)
{
    /// <summary>Reads every identity: dimensions, mapping sets, then bridges, each by key.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The index.</returns>
    public async Task<PinIdentities> ReadAsync(CancellationToken cancellationToken)
    {
        var reportOf = (await reports.ListAsync(null, cancellationToken)).ToDictionary(report => report.ReportDimensionId, report => report.ReportId);
        var dimensionVersionsOf = (await dimensionVersions.ListAsync(null, cancellationToken))
            .ToLookup(row => row.DimensionId, row => new VersionRow(row.DimensionVersionId, row.VersionNumber, row.Status, row.PublishedAt, row.CreatedAt));
        var mappingVersionsOf = (await mappingVersions.ListAsync(null, cancellationToken))
            .ToLookup(row => row.MappingSetId, row => new VersionRow(row.MappingVersionId, row.VersionNumber, row.Status, row.PublishedAt, row.CreatedAt));
        var bridgeVersionsOf = (await bridgeVersions.ListAsync(null, cancellationToken))
            .ToLookup(row => row.BridgeId, row => new VersionRow(row.BridgeVersionId, row.VersionNumber, row.Status, row.PublishedAt, row.CreatedAt));
        IEnumerable<PinIdentity?> identities =
        [
            .. (await dimensions.ListAsync(null, cancellationToken)).OrderBy(row => row.DimensionKey, StringComparer.Ordinal)
                .Select(row => Identity(PinKinds.Dimension, row.DimensionId, row.DimensionKey, row.DisplayName, row.DimensionId, dimensionVersionsOf[row.DimensionId], reportOf)),
            .. (await mappingSets.ListAsync(null, cancellationToken)).OrderBy(row => row.MappingSetKey, StringComparer.Ordinal)
                .Select(row => Identity(PinKinds.MappingSet, row.MappingSetId, row.MappingSetKey, row.DisplayName, row.DimensionId, mappingVersionsOf[row.MappingSetId], reportOf)),
            .. (await bridges.ListAsync(null, cancellationToken)).OrderBy(row => row.BridgeKey, StringComparer.Ordinal)
                .Select(row => Identity(PinKinds.Bridge, row.BridgeId, row.BridgeKey, row.DisplayName, row.TargetDimensionId, bridgeVersionsOf[row.BridgeId], reportOf))
        ];
        return new PinIdentities([.. identities.OfType<PinIdentity>()]);
    }

    /// <summary>An identity with its live and frozen versions, oldest first; null while it has no version.</summary>
    private static PinIdentity? Identity(
        string kind,
        long id,
        string key,
        string displayName,
        long dimensionId,
        IEnumerable<VersionRow> versions,
        IReadOnlyDictionary<long, long> reportOf)
    {
        VersionRow[] all = [.. versions];
        if (all.Length == 0) return null;
        return new PinIdentity
        {
            Kind = kind,
            Id = id,
            Key = key,
            DisplayName = displayName,
            DimensionId = dimensionId,
            ReportId = reportOf.TryGetValue(dimensionId, out var reportId) ? reportId : null,
            CreatedAt = all.Min(version => version.CreatedAt),
            Versions =
            [
                .. all.Where(version => version.Status is PublicationStatuses.Published or PublicationStatuses.Retired)
                    .OrderBy(version => version.Number)
                    .Select(version => new PinIdentityVersion { Id = version.Id, VersionNumber = version.Number, Status = version.Status, PublishedAt = version.PublishedAt!.Value })
            ]
        };
    }

    /// <summary>A version of any living kind.</summary>
    private sealed record VersionRow(long Id, int Number, string Status, DateTime? PublishedAt, DateTime CreatedAt);
}
