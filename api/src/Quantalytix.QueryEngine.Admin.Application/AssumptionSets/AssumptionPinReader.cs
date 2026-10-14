using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// Reads an assumption set's pins with the keys of their dimensions for its publication rules. The pins validator
/// guarantees that every pinned dimension exists, and dimensions an assumption set references cannot be deleted.
/// </summary>
public sealed class AssumptionPinReader(ITableGateway<AssumptionSetDimensionRecord> pins, ITableGateway<DimensionRecord> dimensions)
{
    /// <summary>Lists an assumption set's pins with the keys of their dimensions, ordered by dimension key.</summary>
    /// <param name="assumptionSetId">The assumption set.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The pinned dimensions.</returns>
    public async Task<IReadOnlyList<PinnedDimension>> ListAsync(long assumptionSetId, CancellationToken cancellationToken)
    {
        var rows = await pins.ListAsync(new { AssumptionSetId = assumptionSetId }, cancellationToken);
        var dimensionRows = await dimensions.ListAsync(new { DimensionId = rows.Select(row => row.DimensionId).ToArray() }, cancellationToken);
        var keys = dimensionRows.ToDictionary(dimension => dimension.DimensionId, dimension => dimension.DimensionKey);
        return
        [
            .. rows
                .Select(row => new PinnedDimension { DimensionKey = keys[row.DimensionId], Pin = row })
                .OrderBy(pinned => pinned.DimensionKey, StringComparer.Ordinal)
        ];
    }
}
