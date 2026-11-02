using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Creates a dimension's live version: empty, or a copy of another version's content and range data type. A new dimension, analytical
/// or report-owned, starts with one.
/// </summary>
public sealed class LiveDimensionVersions(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionVersionCloner cloner,
    LiveSnapshot live,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Creates an empty live version.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="rangeDataType">The range data type of a range dimension's version; null otherwise.</param>
    /// <param name="cancellationToken">Cancels the read and the write.</param>
    /// <returns>The stored version.</returns>
    public async Task<DimensionVersionRecord> CreateAsync(long dimensionId, string? rangeDataType, CancellationToken cancellationToken)
    {
        var version = new DimensionVersionRecord
        {
            DimensionVersionId = ids.NextId(),
            DimensionId = dimensionId,
            SnapshotId = await live.IdAsync(cancellationToken),
            RangeDataType = rangeDataType,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(version, cancellationToken);
        return version;
    }

    /// <summary>Creates a live version of a dimension as a copy of a version of another dimension.</summary>
    /// <param name="dimensionId">The dimension the version belongs to.</param>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored version.</returns>
    public async Task<DimensionVersionRecord> CloneAsync(long dimensionId, long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var version = await CreateAsync(dimensionId, source.RangeDataType, cancellationToken);
        await cloner.CloneAsync(sourceVersionId, version.DimensionVersionId, cancellationToken);
        return version;
    }
}
