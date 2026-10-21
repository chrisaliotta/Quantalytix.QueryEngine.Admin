using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;

namespace Quantalytix.QueryEngine.Admin.Application.ReportEditor;

/// <summary>
/// Lists the items of a bridge source that map to no row. A dataset row whose item maps to no row classifies into no member of the
/// report dimension, so the report leaves it out without a trace; this list is where it shows.
/// </summary>
public sealed class SourceCoverageReader(
    ITableGateway<ReportSourceRecord> sources,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<BridgeMappingRecord> bridgeMappings,
    OwnedVersions owned,
    PublishedDimensionReader publishedDimensions)
{
    /// <summary>The assignable members of a bridge source's item dimension that its bridge maps to no row.</summary>
    /// <param name="sourceId">A bridge source.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The unmapped item keys, in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> UnmappedAsync(long sourceId, CancellationToken cancellationToken)
    {
        var source = await sources.GetAsync(new { ReportSourceId = sourceId }, cancellationToken);
        var role = await roles.GetAsync(new { RoleId = source.RoleId!.Value }, cancellationToken);
        var bridgeVersion = (await owned.BridgesAsync([source], cancellationToken))[sourceId];
        var entries = await bridgeMappings.ListAsync(new { bridgeVersion.BridgeVersionId }, cancellationToken);
        return Unmapped((await publishedDimensions.ReadAsync(role.DimensionId, cancellationToken))!, entries.Select(entry => entry.SourceMemberKey));
    }

    /// <summary>The assignable items that no entry maps.</summary>
    /// <param name="items">The published version of the item dimension.</param>
    /// <param name="mapped">The item keys the bridge's entries map.</param>
    /// <returns>The unmapped item keys, in ordinal order.</returns>
    public static IReadOnlyList<string> Unmapped(DimensionKeys items, IEnumerable<string> mapped)
    {
        var mappedKeys = mapped.ToHashSet(StringComparer.Ordinal);
        return [.. items.MemberKeys.Where(key => !items.UnassignableMemberKeys.Contains(key) && !mappedKeys.Contains(key)).Order(StringComparer.Ordinal)];
    }
}
