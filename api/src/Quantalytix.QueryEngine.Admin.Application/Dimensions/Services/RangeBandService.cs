using Quantalytix.QueryEngine.Admin.Application.Metadata;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>Reads and replaces the range bands of a range dimension version: the typed ranges that assign source values to members.</summary>
public sealed class RangeBandService(ITableGateway<RangeBandRecord> bands)
{
    /// <summary>Lists a version's bands.</summary>
    /// <param name="reference">A version of a range dimension.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The bands by ordinal.</returns>
    public async Task<IReadOnlyList<RangeBandRecord>> ListAsync(RangeVersionReference reference, CancellationToken cancellationToken)
    {
        var rows = await bands.ListAsync(new { reference.DimensionVersionId }, cancellationToken);
        return [.. rows.OrderBy(row => row.Ordinal)];
    }

    /// <summary>Replaces every band of a draft version with the given list, numbering the ordinals by position from 1.</summary>
    /// <param name="reference">A draft version of a range dimension.</param>
    /// <param name="input">The complete band list, in order.</param>
    /// <param name="cancellationToken">Cancels the delete and the insert.</param>
    /// <returns>The stored bands by ordinal.</returns>
    public async Task<IReadOnlyList<RangeBandRecord>> ReplaceAsync(RangeVersionReference reference, RangeBandsInput input, CancellationToken cancellationToken)
    {
        List<RangeBandRecord> rows =
        [
            .. input.Bands.Select((band, index) => new RangeBandRecord
            {
                DimensionVersionId = reference.DimensionVersionId,
                Ordinal = index + 1,
                DimensionMemberId = band.DimensionMemberId,
                LowerDecimal = band.LowerDecimal,
                UpperDecimal = band.UpperDecimal,
                LowerDate = band.LowerDate,
                UpperDate = band.UpperDate,
                LowerDatetime = band.LowerDatetime,
                UpperDatetime = band.UpperDatetime,
                LowerInclusive = band.LowerInclusive,
                UpperInclusive = band.UpperInclusive
            })
        ];
        await bands.DeleteAsync(new { reference.DimensionVersionId }, cancellationToken);
        await bands.InsertManyAsync(rows, cancellationToken);
        return rows;
    }
}
