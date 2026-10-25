using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// Reads what the entry member rules check: a mapping version's entries and the keys of the published version of its set's
/// dimension, which the active assumption set pins.
/// </summary>
public sealed class MappingEntrySetReader(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<DimensionMappingRecord> entries,
    PublishedDimensionReader dimensions)
{
    /// <summary>Reads a mapping version's entries with the keys of its dimension's published version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The version's entry set.</returns>
    public async Task<MappingEntrySet> ReadAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var set = await sets.GetAsync(new { version.MappingSetId }, cancellationToken);
        var rows = await entries.ListAsync(new { MappingVersionId = versionId }, cancellationToken);
        return new MappingEntrySet
        {
            Entries = [.. rows.OrderBy(row => row.SourceKeyValue, StringComparer.Ordinal)],
            Dimension = await dimensions.ReadAsync(set.DimensionId, cancellationToken)
        };
    }
}
