using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.AssumptionSets;

/// <summary>
/// Confirms the assumption sets that let requests read the default versions or backtest against history. The one active set
/// holds the default versions: publishing a living version repoints it, while historical sets keep the versions they
/// captured, and it cannot be deleted. Capturing copies the active set's pins into a new historical set that reads the active
/// release, so a backtest runs the pins over the datasets they were validated with, and capturing or deleting a set advances the
/// semantic revision, because the engine caches which set keys requests can name by revision.
/// </summary>
public sealed class AssumptionSetTests
{
    /// <summary>The active set.</summary>
    private const long ActiveSetId = 1;

    /// <summary>A historical set captured before v2 of the branch dimension was published.</summary>
    private const long HistoricalSetId = 2;

    /// <summary>The branch dimension.</summary>
    private const long BranchId = 100;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The release queries use while each test runs.</summary>
    private const long ActiveReleaseId = 900;

    /// <summary>The clock's time throughout each test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>The active set and the historical set.</summary>
    private readonly InMemoryTableGateway<AssumptionSetRecord> _sets = new(
        Set(ActiveSetId, "active", AssumptionSetStatuses.Active),
        Set(HistoricalSetId, "fy2026_q2", AssumptionSetStatuses.Historical) with { CapturedAt = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc) });

    /// <summary>Both sets pin v1 of the branch dimension, which v2 is about to replace.</summary>
    private readonly InMemoryTableGateway<AssumptionSetDimensionRecord> _dimensionPins = new(
        new AssumptionSetDimensionRecord { AssumptionSetId = ActiveSetId, DimensionId = BranchId, DimensionVersionId = 101 },
        new AssumptionSetDimensionRecord { AssumptionSetId = HistoricalSetId, DimensionId = BranchId, DimensionVersionId = 101 });

    /// <summary>The active set pins v1 of the margin model.</summary>
    private readonly InMemoryTableGateway<AssumptionSetModelRecord> _modelPins = new(
        new AssumptionSetModelRecord { AssumptionSetId = ActiveSetId, ModelId = 40, ModelVersionId = 41 });

    /// <summary>No bridge pins at first.</summary>
    private readonly InMemoryTableGateway<AssumptionSetBridgeRecord> _bridgePins = new();

    /// <summary>The analytical branch dimension and report 60's report dimension 500.</summary>
    private readonly InMemoryTableGateway<DimensionRecord> _dimensions = new(
        ReleaseRows.Dimension(BranchId, "branch"),
        ReleaseRows.Dimension(500, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report });

    /// <summary>Bridge 95 of a source of report 60, from branches into its report dimension.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(BridgeRows.Bridge(95, BranchId, 500));

    /// <summary>The branch dimension's published v1 and v2.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionRows.Version(101, BranchId, 1, PublicationStatuses.Published),
        DimensionRows.Version(102, BranchId, 2, PublicationStatuses.Published));

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 11,
        SemanticRevision = StartingRevision,
        ActiveReleaseId = ActiveReleaseId,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The pins over this test's tables.</summary>
    private readonly AssumptionPins _pins;

    /// <summary>Builds the pins over this test's tables.</summary>
    public AssumptionSetTests()
    {
        _pins = new AssumptionPins(_sets, _dimensionPins, new InMemoryTableGateway<AssumptionSetMappingRecord>(), _bridgePins, _modelPins);
    }

    /// <summary>
    /// A capture is a historical set stamped with the capture time that pins exactly what the active set pins and reads the active
    /// release.
    /// </summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task Capture_CopiesTheActivePinsAndReleaseAndAdvancesTheRevision()
    {
        var input = new AssumptionCaptureInput { AssumptionSetKey = "fy2026_q3", DisplayName = "FY2026 Q3 close" };

        var set = await Service().CaptureAsync(input, TestContext.Current.CancellationToken);

        Assert.Equal((AssumptionSetStatuses.Historical, Now.UtcDateTime, ActiveReleaseId), (set.Status, set.CapturedAt, set.ReleaseId));
        Assert.Equal(
            await _pins.ListAsync(ActiveSetId, TestContext.Current.CancellationToken),
            await _pins.ListAsync(set.AssumptionSetId, TestContext.Current.CancellationToken));
        Assert.Equal(StartingRevision + 1, Revision());
    }

    /// <summary>
    /// A capture leaves out the pins of a report's dimension and of its sources' bridges, because reports always run on the active
    /// set: a historical set holding them would keep versions alive that their report edits in place.
    /// </summary>
    /// <returns>A task that completes when the capture is checked.</returns>
    [Fact]
    public async Task Capture_SkipsReportOwnedPins()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _dimensionPins.InsertAsync(new AssumptionSetDimensionRecord { AssumptionSetId = ActiveSetId, DimensionId = 500, DimensionVersionId = 501 }, cancellationToken);
        await _bridgePins.InsertAsync(new AssumptionSetBridgeRecord { AssumptionSetId = ActiveSetId, BridgeId = 95, BridgeVersionId = 96 }, cancellationToken);

        var set = await Service().CaptureAsync(new AssumptionCaptureInput { AssumptionSetKey = "fy2026_q3", DisplayName = "FY2026 Q3 close" }, cancellationToken);

        Assert.Equal(
            [new PinnedVersion(VersionKinds.DimensionVersion, BranchId, 101), new PinnedVersion(VersionKinds.CalculationModelVersion, 40, 41)],
            await _pins.ListAsync(set.AssumptionSetId, cancellationToken));
    }

    /// <summary>Publishing v2 of the branch dimension repoints the active set and leaves the historical set on v1.</summary>
    /// <returns>A task that completes when the pins are checked.</returns>
    [Fact]
    public async Task Publishing_RepointsOnlyTheActiveSet()
    {
        var pinner = new ActiveSetPinner(VersionKinds.DimensionVersion, new VersionStoreRegistry([new DimensionVersionStore(_dimensionVersions)]), _pins);

        await pinner.OnPublishedAsync(102, TestContext.Current.CancellationToken);

        Assert.Equal(102, PinOf(ActiveSetId));
        Assert.Equal(101, PinOf(HistoricalSetId));
    }

    /// <summary>The active set cannot be deleted: the refusal names it, and its pins and the revision stay.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task ActiveSet_CannotBeDeleted()
    {
        var conflict = await Service().DeleteAsync(ActiveSetId, TestContext.Current.CancellationToken);

        Assert.Equal("Assumption set 'active' is the active set, which holds the default versions; it cannot be deleted.", conflict);
        Assert.Equal(101, PinOf(ActiveSetId));
        Assert.Equal(StartingRevision, Revision());
    }

    /// <summary>Deleting a historical set removes it with its pins and advances the revision.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task HistoricalSet_IsDeletedWithItsPins()
    {
        var conflict = await Service().DeleteAsync(HistoricalSetId, TestContext.Current.CancellationToken);

        Assert.Null(conflict);
        Assert.DoesNotContain(_sets.All, set => set.AssumptionSetId == HistoricalSetId);
        Assert.DoesNotContain(_dimensionPins.All, pin => pin.AssumptionSetId == HistoricalSetId);
        Assert.Equal(StartingRevision + 1, Revision());
    }

    /// <summary>The service over this test's tables and clock.</summary>
    private AssumptionSetService Service()
    {
        var time = new FakeTimeProvider(Now);
        var bridgeVersions = new InMemoryTableGateway<BridgeVersionRecord>();
        var reader = new AssumptionPinReader(
            _pins,
            _dimensions,
            _dimensionVersions,
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<MappingVersionRecord>(),
            _bridges,
            bridgeVersions,
            new InMemoryTableGateway<CalculationModelRecord>(),
            new InMemoryTableGateway<CalculationModelVersionRecord>());
        var ownership = new ReportOwnership(_dimensions, _dimensionVersions, _bridges, bridgeVersions, new InMemoryTableGateway<ReportRecord>());
        return new AssumptionSetService(
            _sets, _pins, reader, ownership, _databaseInfo, new SemanticRevision(_databaseInfo, time), new SequentialIdGenerator(1000), time);
    }

    /// <summary>The branch dimension version a set pins.</summary>
    private long PinOf(long assumptionSetId)
    {
        return _dimensionPins.All.Single(pin => pin.AssumptionSetId == assumptionSetId && pin.DimensionId == BranchId).DimensionVersionId;
    }

    /// <summary>The tenant's current semantic revision.</summary>
    private long Revision()
    {
        return Assert.Single(_databaseInfo.All).SemanticRevision;
    }

    /// <summary>An assumption set with a status.</summary>
    private static AssumptionSetRecord Set(long assumptionSetId, string key, string status)
    {
        return new AssumptionSetRecord
        {
            AssumptionSetId = assumptionSetId,
            AssumptionSetKey = key,
            DisplayName = key,
            Status = status,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }
}
