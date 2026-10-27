using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms the range band rules <c>bands_non_overlapping</c> and <c>band_boundary_family</c>: in ordinal order each band
/// must start after the previous one ends, with inclusivity deciding a shared boundary and a null bound counting as
/// unbounded, and a band may only set the bounds of its version's range data type. The engine joins a source value to every
/// band that contains it, so overlapping bands duplicate fact rows, and bounds of another family are never compared.
/// </summary>
public sealed class RangeBandRuleTests
{
    /// <summary>The range dimension.</summary>
    private const long DimensionId = 1;

    /// <summary>The version being validated.</summary>
    private const long VersionId = 10;

    /// <summary>The members the bands assign.</summary>
    private static readonly DimensionMemberRecord[] Members =
    [
        Member(1, VersionId, "LOW"),
        Member(2, VersionId, "MID"),
        Member(3, VersionId, "HIGH")
    ];

    /// <summary>Bands that meet at a boundary included by only one side, unbounded at both ends, do not overlap.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task AscendingBandsMeetingAtHalfOpenBoundaries_DoNotOverlap()
    {
        var violations = await NonOverlappingAsync(
            RangeDataTypes.Decimal,
            DecimalBand(VersionId, 1, 1, null, 10m),
            DecimalBand(VersionId, 2, 2, 10m, 20m),
            DecimalBand(VersionId, 3, 3, 20m, null, lowerInclusive: false));

        Assert.Empty(violations);
    }

    /// <summary>A boundary that both neighbours include belongs to both bands.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BoundaryIncludedByBothBands_Overlaps()
    {
        var violations = await NonOverlappingAsync(
            RangeDataTypes.Decimal,
            DecimalBand(VersionId, 1, 1, 0m, 10m, upperInclusive: true),
            DecimalBand(VersionId, 2, 2, 10m, 20m));

        var violation = Assert.Single(violations);
        Assert.Equal("bands_non_overlapping", violation.Code);
        Assert.Equal("Band 2 (member 'MID') overlaps Band 1 (member 'LOW'): each band must start after the previous one ends.", violation.Message);
    }

    /// <summary>A band without an upper bound covers everything above it, so any later band overlaps it.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task UnboundedBandBeforeAnother_Overlaps()
    {
        var violations = await NonOverlappingAsync(
            RangeDataTypes.Decimal,
            DecimalBand(VersionId, 1, 1, 0m, null),
            DecimalBand(VersionId, 2, 2, 100m, 200m));

        Assert.Equal(["bands_non_overlapping"], violations.Select(violation => violation.Code));
    }

    /// <summary>Date versions compare the date bounds.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task DateBands_CompareTheirDates()
    {
        var violations = await NonOverlappingAsync(
            RangeDataTypes.Date,
            DateBand(1, 1, Utc(2025, 1, 1), Utc(2025, 7, 1)),
            DateBand(2, 2, Utc(2025, 7, 1), Utc(2026, 1, 1)),
            DateBand(3, 3, Utc(2025, 12, 1), null));

        Assert.Equal(["Band 3 (member 'HIGH') overlaps Band 2 (member 'MID'): each band must start after the previous one ends."], violations.Select(violation => violation.Message));
    }

    /// <summary>Bands that set only bounds of the version's family pass.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BoundsOfTheVersionsFamily_Pass()
    {
        var violations = await BoundaryFamilyAsync(
            RangeDataTypes.Decimal,
            DecimalBand(VersionId, 1, 1, null, 10m),
            DecimalBand(VersionId, 2, 2, 10m, null));

        Assert.Empty(violations);
    }

    /// <summary>A decimal version's band that sets a date bound breaks the family rule.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task BoundOfAnotherFamily_Fails()
    {
        var violations = await BoundaryFamilyAsync(
            RangeDataTypes.Decimal,
            DecimalBand(VersionId, 1, 1, null, 10m),
            DecimalBand(VersionId, 2, 2, 10m, null) with { UpperDate = Utc(2026, 1, 1) });

        var violation = Assert.Single(violations);
        Assert.Equal("band_boundary_family", violation.Code);
        Assert.Equal("Band 2 (member 'MID') sets date bounds, but the version's range data type is decimal.", violation.Message);
    }

    /// <summary>Only range dimensions use bands, so a categorical version's stray bands are not checked.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task NonRangeDimension_HasNoBandsToCheck()
    {
        var snapshot = RangeVersion(DimensionKinds.Categorical, null, DecimalBand(VersionId, 1, 1, null, 10m) with { LowerDate = Utc(2026, 1, 1) });

        var violations = await new BandBoundaryFamilyRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.Empty(violations);
    }

    /// <summary>Runs <c>bands_non_overlapping</c> over a range version with the given bands.</summary>
    private static Task<IReadOnlyList<RuleViolation>> NonOverlappingAsync(string rangeDataType, params RangeBandRecord[] bands)
    {
        return new BandsNonOverlappingRule().EvaluateAsync(RangeVersion(DimensionKinds.Range, rangeDataType, bands), TestContext.Current.CancellationToken);
    }

    /// <summary>Runs <c>band_boundary_family</c> over a range version with the given bands.</summary>
    private static Task<IReadOnlyList<RuleViolation>> BoundaryFamilyAsync(string rangeDataType, params RangeBandRecord[] bands)
    {
        return new BandBoundaryFamilyRule().EvaluateAsync(RangeVersion(DimensionKinds.Range, rangeDataType, bands), TestContext.Current.CancellationToken);
    }

    /// <summary>A version of a dimension of the given kind, with the members and the given bands.</summary>
    private static DimensionSnapshot RangeVersion(string kind, string? rangeDataType, params RangeBandRecord[] bands)
    {
        return Snapshot(DimensionId, VersionId, kind, rangeDataType, Members, bands);
    }

    /// <summary>A date band; the lower bound is inclusive and the upper one exclusive.</summary>
    private static RangeBandRecord DateBand(int ordinal, long dimensionMemberId, DateTime? lower, DateTime? upper)
    {
        return DecimalBand(VersionId, ordinal, dimensionMemberId, null, null) with { LowerDate = lower, UpperDate = upper };
    }
}
