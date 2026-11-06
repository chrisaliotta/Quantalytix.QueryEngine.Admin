using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Hard-deletes dimension versions with everything they own, children before parents: range bands, closure rows, members, levels,
/// then the versions; and dimensions with their roles once no snapshot holds a version of them.
/// </summary>
public sealed class DimensionVersionRemover(
    ITableGateway<DimensionRecord> dimensions,
    ITableGateway<DimensionRoleRecord> roles,
    ITableGateway<DimensionVersionRecord> versions,
    ITableGateway<DimensionLevelRecord> levels,
    ITableGateway<DimensionMemberRecord> members,
    ITableGateway<DimensionClosureRecord> closure,
    ITableGateway<RangeBandRecord> bands) : IVersionRemover
{
    /// <summary><c>dimension</c>.</summary>
    public string Kind => SnapshotKinds.Dimension;

    /// <summary>Deletes the versions and their content, one statement per table.</summary>
    /// <param name="versionIds">The dimension versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every row is deleted.</returns>
    public async Task RemoveVersionsAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await RemoveContentAsync(versionIds, cancellationToken);
        await versions.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes the versions' content and keeps the version rows, which a copy then fills again.</summary>
    /// <param name="versionIds">The dimension versions.</param>
    /// <param name="cancellationToken">Cancels the remaining statements.</param>
    /// <returns>A task that completes when every content row is deleted.</returns>
    public async Task RemoveContentAsync(IReadOnlyCollection<long> versionIds, CancellationToken cancellationToken)
    {
        await bands.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await closure.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await members.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
        await levels.DeleteAsync(new { DimensionVersionId = versionIds }, cancellationToken);
    }

    /// <summary>Deletes the dimensions' roles, then the dimensions.</summary>
    /// <param name="identityIds">The dimensions.</param>
    /// <param name="cancellationToken">Cancels the deletes.</param>
    /// <returns>A task that completes when the rows are gone.</returns>
    public async Task RemoveIdentitiesAsync(IReadOnlyCollection<long> identityIds, CancellationToken cancellationToken)
    {
        await roles.DeleteAsync(new { DimensionId = identityIds }, cancellationToken);
        await dimensions.DeleteAsync(new { DimensionId = identityIds }, cancellationToken);
    }
}
