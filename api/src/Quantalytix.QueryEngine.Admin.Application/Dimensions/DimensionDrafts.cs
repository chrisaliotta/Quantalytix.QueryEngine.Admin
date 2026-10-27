using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// Creates the draft a dimension version starts as, with the dimension's next version number: empty, or a clone of another
/// version's content and range data type. A draft lives only until the workflow publishes it: a new dimension's version 1, the
/// copy a freeze publishes, and a report dimension's version.
/// </summary>
public sealed class DimensionDrafts(
    ITableGateway<DimensionVersionRecord> versions,
    DimensionVersionCloner cloner,
    IIdGenerator ids,
    TimeProvider time)
{
    /// <summary>Creates an empty draft.</summary>
    /// <param name="dimensionId">The dimension.</param>
    /// <param name="rangeDataType">The range data type of a range dimension's version; null otherwise.</param>
    /// <param name="cancellationToken">Cancels the reads and the write.</param>
    /// <returns>The stored draft.</returns>
    public async Task<DimensionVersionRecord> CreateAsync(long dimensionId, string? rangeDataType, CancellationToken cancellationToken)
    {
        var existing = await versions.ListAsync(new { DimensionId = dimensionId }, cancellationToken);
        var draft = new DimensionVersionRecord
        {
            DimensionVersionId = ids.NextId(),
            DimensionId = dimensionId,
            VersionNumber = VersionNumbers.Next(existing.Select(row => row.VersionNumber)),
            Status = PublicationStatuses.Draft,
            RangeDataType = rangeDataType,
            CreatedAt = time.GetUtcNow().UtcDateTime
        };
        await versions.InsertAsync(draft, cancellationToken);
        return draft;
    }

    /// <summary>Creates a draft of a dimension as a copy of a version, of the same or another dimension.</summary>
    /// <param name="dimensionId">The dimension the draft belongs to.</param>
    /// <param name="sourceVersionId">The version to copy.</param>
    /// <param name="cancellationToken">Cancels the reads and writes.</param>
    /// <returns>The stored draft.</returns>
    public async Task<DimensionVersionRecord> CloneAsync(long dimensionId, long sourceVersionId, CancellationToken cancellationToken)
    {
        var source = await versions.GetAsync(new { DimensionVersionId = sourceVersionId }, cancellationToken);
        var draft = await CreateAsync(dimensionId, source.RangeDataType, cancellationToken);
        await cloner.CloneAsync(sourceVersionId, draft, cancellationToken);
        return draft;
    }
}
