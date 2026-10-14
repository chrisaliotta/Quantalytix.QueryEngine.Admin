using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Infrastructure.StarRocks.Identifiers;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.AssumptionSets;

/// <summary>
/// Confirms how capturing resolves versions: each requested dimension is pinned to its published version whose
/// half-open window <c>[valid_from, valid_to)</c> contains the set's <c>effective_at</c>, where a null <c>valid_to</c> is
/// open-ended; drafts are never captured; a requested dimension with no effective version is left unpinned; and pins of
/// other dimensions are kept. An assumption set exists to reproduce exactly the dimension content that was effective at
/// one moment, so the window semantics must match the engine's version resolution.
/// </summary>
public sealed class AssumptionCaptureTests
{
    /// <summary>The draft assumption set being captured.</summary>
    private const long AssumptionSetId = 300;

    /// <summary>Branch: v1 ends exactly at the effective time, v2 starts there, and a draft v3 starts there too.</summary>
    private const long BranchId = 100;

    /// <summary>Region: one published version without an end.</summary>
    private const long RegionId = 200;

    /// <summary>Product: one published version that starts after the effective time.</summary>
    private const long ProductId = 400;

    /// <summary>Segment: pinned before the capture and not requested.</summary>
    private const long SegmentId = 500;

    /// <summary>Desk: one published version that ends exactly at the effective time.</summary>
    private const long DeskId = 600;

    /// <summary>The set's effective time.</summary>
    private static readonly DateTime EffectiveAt = Utc(2026, 7, 1);

    /// <summary>The assumption set table.</summary>
    private readonly InMemoryTableGateway<AssumptionSetRecord> _sets = new(new AssumptionSetRecord
    {
        AssumptionSetId = AssumptionSetId,
        AssumptionSetKey = "FY2026_Q2_CLOSE",
        DisplayName = "FY2026 Q2 close",
        EffectiveAt = EffectiveAt,
        Status = AssumptionSetStatuses.Draft,
        CreatedAt = Utc(2026, 7, 2)
    });

    /// <summary>Branch is pinned to its old v1 and Segment to its v1 before the capture.</summary>
    private readonly InMemoryTableGateway<AssumptionSetDimensionRecord> _pins = new(Pin(BranchId, 101), Pin(SegmentId, 501));

    /// <summary>The versions of Branch, Region, Product, Segment and Desk.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        Version(101, BranchId, 1, PublicationStatuses.Published, Utc(2026, 1, 1), EffectiveAt),
        Version(102, BranchId, 2, PublicationStatuses.Published, EffectiveAt, Utc(2027, 1, 1)),
        Version(103, BranchId, 3, PublicationStatuses.Draft, EffectiveAt, null),
        Version(201, RegionId, 1, PublicationStatuses.Published, Utc(2020, 1, 1), null),
        Version(401, ProductId, 1, PublicationStatuses.Published, Utc(2027, 1, 1), null),
        Version(501, SegmentId, 1, PublicationStatuses.Published, Utc(2020, 1, 1), null),
        Version(601, DeskId, 1, PublicationStatuses.Published, Utc(2026, 1, 1), EffectiveAt));

    /// <summary>
    /// Branch is re-pinned to v2, which starts at the effective time, and the draft v3 is ignored. <c>valid_from</c> is
    /// inclusive and <c>valid_to</c> exclusive, so Desk, whose only version ends at the effective time, is not pinned.
    /// </summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task PinsThePublishedVersionEffectiveAtEffectiveAt()
    {
        var pins = await CaptureAsync(BranchId, DeskId);

        Assert.Equal(102, pins.Single(pin => pin.DimensionId == BranchId).DimensionVersionId);
        Assert.DoesNotContain(pins, pin => pin.DimensionId == DeskId);
    }

    /// <summary>A published version with a null <c>valid_to</c> is effective from its start onwards.</summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task OpenEndedVersion_IsCaptured()
    {
        var pins = await CaptureAsync(RegionId);

        Assert.Equal(201, pins.Single(pin => pin.DimensionId == RegionId).DimensionVersionId);
    }

    /// <summary>
    /// Only the requested dimensions change: Product, whose only version starts later, ends up unpinned, and Segment,
    /// which was not requested, keeps its pin.
    /// </summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task OnlyRequestedDimensionsChange()
    {
        var pins = await CaptureAsync(BranchId, RegionId, ProductId);

        Assert.Equal([(BranchId, 102L), (RegionId, 201L), (SegmentId, 501L)], pins.Select(pin => (pin.DimensionId, pin.DimensionVersionId)));
        Assert.Equal(pins, _pins.All.OrderBy(pin => pin.DimensionId));
    }

    /// <summary>Captures the given dimensions into the assumption set.</summary>
    private async Task<IReadOnlyList<AssumptionSetDimensionRecord>> CaptureAsync(params long[] dimensionIds)
    {
        var service = new AssumptionSetService(_sets, _pins, _dimensionVersions, new IdGenerator(), new FakeTimeProvider());
        return await service.CaptureAsync(AssumptionSetId, new AssumptionCaptureInput { DimensionIds = dimensionIds }, TestContext.Current.CancellationToken);
    }

    /// <summary>A pin of the assumption set.</summary>
    private static AssumptionSetDimensionRecord Pin(long dimensionId, long dimensionVersionId)
    {
        return new AssumptionSetDimensionRecord { AssumptionSetId = AssumptionSetId, DimensionId = dimensionId, DimensionVersionId = dimensionVersionId };
    }

    /// <summary>A dimension version valid over <c>[validFrom, validTo)</c>.</summary>
    private static DimensionVersionRecord Version(long dimensionVersionId, long dimensionId, int versionNumber, string status, DateTime validFrom, DateTime? validTo)
    {
        return new DimensionVersionRecord
        {
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionId,
            VersionNumber = versionNumber,
            Status = status,
            ValidFrom = validFrom,
            ValidTo = validTo
        };
    }

    /// <summary>Midnight UTC of a date.</summary>
    private static DateTime Utc(int year, int month, int day)
    {
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}
