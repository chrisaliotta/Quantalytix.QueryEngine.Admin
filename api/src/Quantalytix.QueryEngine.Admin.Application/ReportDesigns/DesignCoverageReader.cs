using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.ReportDesigns;

/// <summary>
/// Lists the items of a design source that map to no row. A source row whose item maps to no row classifies into no member of
/// the report dimension, so the report leaves it out without a trace; this list is where it shows.
/// </summary>
public sealed class DesignCoverageReader(
    ReportDesignVersions designs,
    PublishedDimensionReader publishedDimensions,
    ITableGateway<BridgeMappingRecord> bridgeMappings)
{
    /// <summary>The assignable members of the source's bound dimension that the paired version of its bridge maps to no row.</summary>
    /// <param name="reportVersionId">A draft, validated or published report version.</param>
    /// <param name="bridgeId">The source's bridge.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The unmapped member keys, in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> UnmappedAsync(long reportVersionId, long bridgeId, CancellationToken cancellationToken)
    {
        var scope = await designs.ReadAsync(reportVersionId, cancellationToken);
        var items = (await publishedDimensions.ReadAsync(scope.Bridges[bridgeId].SourceDimensionId, cancellationToken))!;
        var mapped = (await bridgeMappings.ListAsync(new { scope.BridgeVersions[bridgeId].BridgeVersionId }, cancellationToken))
            .Select(entry => entry.SourceMemberKey)
            .ToHashSet(StringComparer.Ordinal);
        return [.. items.MemberKeys.Where(key => !items.UnassignableMemberKeys.Contains(key) && !mapped.Contains(key)).Order(StringComparer.Ordinal)];
    }
}
