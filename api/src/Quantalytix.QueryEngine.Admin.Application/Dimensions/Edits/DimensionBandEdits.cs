using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// The band operation of the dimension change set (M5): a range version's bands are replaced as a whole set, each assigning a
/// member of the version, with bounds of the version's range data type. Order, overlap and open ends are publication findings.
/// </summary>
internal static class DimensionBandEdits
{
    /// <summary><c>saveBands</c>; the bands are renumbered 1..n in their order.</summary>
    /// <param name="snapshot">The aggregate.</param>
    /// <param name="operation">The operation.</param>
    /// <param name="context">Resolves ids and takes refusals.</param>
    /// <returns>The aggregate with the new band set.</returns>
    public static DimensionSnapshot Save(DimensionSnapshot snapshot, DimensionSaveBandsOperation operation, ChangeContext context)
    {
        if (snapshot.Dimension.DimensionKind != DimensionKinds.Range)
        {
            context.Conflict("Not a range dimension", "Only range dimensions have bands.");
            return snapshot;
        }

        if (snapshot.Version.RangeDataType is not { } rangeDataType)
        {
            context.Conflict("No range data type", "Set the version's range data type before adding bands.");
            return snapshot;
        }

        var bands = new List<RangeBandRecord>();
        for (var index = 0; index < operation.Bands.Count; index++)
        {
            var band = operation.Bands[index];
            var memberId = context.Ids.Resolve(band.MemberId);
            if (snapshot.Members.All(member => member.DimensionMemberId != memberId))
                context.Reject($"bands[{index}].memberId", "Choose a member of this version.");
            if (DimensionBandBounds.Problem(band.Lower, rangeDataType) is { } lower) context.Reject($"bands[{index}].lower", lower);
            if (DimensionBandBounds.Problem(band.Upper, rangeDataType) is { } upper) context.Reject($"bands[{index}].upper", upper);
            var row = new RangeBandRecord
            {
                DimensionVersionId = snapshot.VersionId,
                Ordinal = index + 1,
                DimensionMemberId = memberId,
                LowerInclusive = band.LowerInclusive,
                UpperInclusive = band.UpperInclusive
            };
            bands.Add(context.Refused ? row : DimensionBandBounds.With(row, band.Lower, band.Upper, rangeDataType));
        }

        return context.Refused ? snapshot : snapshot with { Bands = bands };
    }
}
