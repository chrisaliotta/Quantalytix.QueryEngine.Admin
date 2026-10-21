using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Hard-deletes dimension versions nothing references with everything they own, children before parents: range bands, closure
/// rows, members, levels, then the versions. Those are drafts, and the published version of a report dimension going with its
/// report, after the report's content, bridges and pins.
/// </summary>
public sealed class DimensionVersionRemover(
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure,
    ITableGateway<RangeBandRecord> bands)
{
    /// <summary>Deletes the versions and their content, one statement per table.</summary>
    /// <param name="versionIds">The dimension versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every row is deleted.</returns>
    public async Task RemoveAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await bands.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await closure.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await members.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await levels.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await versions.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
    }
}
