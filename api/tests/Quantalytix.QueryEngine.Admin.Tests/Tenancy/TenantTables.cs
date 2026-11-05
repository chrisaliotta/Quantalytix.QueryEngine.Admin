using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.Snapshots;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// One small tenant for the tenant module's read tests: the live snapshot holds loans v11, deposits v21 and the disabled fees v31;
/// frozen snapshot <c>fy2025_close</c> holds loans v12 and branch v101. Branch (100) is live in v102, region (200) in v201, and
/// report dimension 300 belongs to report 601. Bridge 81 joins branch to region; bridge 82 leads into the report dimension, so
/// report 601 owns it. Mapping set 62 exists only in the frozen snapshot, so the catalog does not list it.
/// </summary>
internal sealed class TenantTables
{
    /// <summary>When every seeded row was created.</summary>
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The singleton database info row.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 21,
        SemanticRevision = 12,
        UpdatedAt = Created
    });

    /// <summary>The live snapshot and one frozen snapshot.</summary>
    public InMemoryTableGateway<SnapshotRecord> Snapshots { get; } = new(SnapshotRows.Live(), SnapshotRows.Frozen());

    /// <summary>Loans, deposits and fees.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(
        new DatasetRecord { DatasetId = 1, DatasetKey = "loans", DisplayName = "Loans" },
        new DatasetRecord { DatasetId = 2, DatasetKey = "deposits", DisplayName = "Deposits" },
        new DatasetRecord { DatasetId = 3, DatasetKey = "fees", DisplayName = "Fees" });

    /// <summary>Live loans v11, deposits v21 and disabled fees v31; loans v12 in the frozen snapshot.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(
        DatasetVersion(11, 1, SnapshotRows.LiveId),
        DatasetVersion(12, 1, SnapshotRows.FrozenId),
        DatasetVersion(21, 2, SnapshotRows.LiveId),
        DatasetVersion(31, 3, SnapshotRows.LiveId) with { IsEnabled = false });

    /// <summary>Two measures in loans v11, one in deposits v21 and three in the frozen loans v12.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(Measure(1, 11), Measure(2, 11), Measure(3, 21), Measure(4, 12), Measure(5, 12), Measure(6, 12));

    /// <summary>Branch and region, analytical, and report 601's report dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(100, "branch", DimensionPurposes.Analytical),
        Dimension(200, "region", DimensionPurposes.Analytical),
        Dimension(300, "income_statement_rows", DimensionPurposes.Report));

    /// <summary>Branch v101 frozen and v102 live; region v201 live; the report dimension v301 live.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        DimensionVersion(101, SnapshotRows.FrozenId),
        DimensionVersion(102, SnapshotRows.LiveId),
        DimensionVersion(201, SnapshotRows.LiveId),
        DimensionVersion(301, SnapshotRows.LiveId));

    /// <summary>Branch's only level, in its live version.</summary>
    public InMemoryTableGateway<DimensionLevelRecord> Levels { get; } = new(new DimensionLevelRecord
    {
        DimensionLevelId = 103,
        DimensionVersionId = 102,
        LevelKey = "branch",
        DisplayName = "Branch",
        Ordinal = 1,
        IsLeafLevel = true
    });

    /// <summary>
    /// Branch's live members: roots <c>HQ</c> (sort 0) and <c>EAST</c> (sort 1); under <c>EAST</c>, <c>E2</c> (sort 1, level branch)
    /// and the unassignable <c>E1</c> (sort 2, level branch). The frozen version holds one member of its own.
    /// </summary>
    public InMemoryTableGateway<DimensionMemberRecord> Members { get; } = new(
        Member(1, 102, "E1", parentId: 3, levelId: 103, sortOrder: 2, isAssignable: false),
        Member(2, 102, "E2", parentId: 3, levelId: 103, sortOrder: 1, isAssignable: true),
        Member(3, 102, "EAST", parentId: null, levelId: null, sortOrder: 1, isAssignable: true),
        Member(4, 102, "HQ", parentId: null, levelId: null, sortOrder: 0, isAssignable: true),
        Member(5, 101, "OLD", parentId: null, levelId: null, sortOrder: 0, isAssignable: true));

    /// <summary>Branch's role.</summary>
    public InMemoryTableGateway<DimensionRoleRecord> Roles { get; } = new(new DimensionRoleRecord
    {
        DimensionRoleId = 110,
        DimensionId = 100,
        DimensionRoleKey = "booking_branch",
        DisplayName = "Booking branch"
    });

    /// <summary>Two mapping sets into branch.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new(
        new MappingSetRecord { MappingSetId = 61, MappingSetKey = "core_branch", SourceNamespace = "core", DimensionId = 100, DisplayName = "Core branch codes" },
        new MappingSetRecord { MappingSetId = 62, MappingSetKey = "old_branch", SourceNamespace = "old", DimensionId = 100, DisplayName = "Old branch codes" });

    /// <summary>Mapping set 61 live; mapping set 62 only in the frozen snapshot.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new(
        new MappingVersionRecord { MappingVersionId = 63, MappingSetId = 61, SnapshotId = SnapshotRows.LiveId, CreatedAt = Created },
        new MappingVersionRecord { MappingVersionId = 64, MappingSetId = 62, SnapshotId = SnapshotRows.FrozenId, CreatedAt = Created });

    /// <summary>An analytical bridge from branch to region and a report bridge from region into the report dimension.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(
        Bridge(81, "branch_to_region", 100, 200),
        Bridge(82, "region_to_rows", 200, 300));

    /// <summary>Both bridges' live versions.</summary>
    public InMemoryTableGateway<BridgeVersionRecord> BridgeVersions { get; } = new(BridgeVersion(83, 81), BridgeVersion(84, 82));

    /// <summary>One calculation model.</summary>
    public InMemoryTableGateway<CalculationModelRecord> Models { get; } = new(new CalculationModelRecord { CalculationModelId = 401, ModelKey = "yield" });

    /// <summary>The model's live version.</summary>
    public InMemoryTableGateway<CalculationModelVersionRecord> ModelVersions { get; } = new(new CalculationModelVersionRecord
    {
        CalculationModelVersionId = 402,
        CalculationModelId = 401,
        SnapshotId = SnapshotRows.LiveId,
        DisplayName = "Yield",
        CalendarKey = "gregorian",
        CurrencyCode = "USD",
        FormulaLanguageVersion = "1.0",
        IsEnabled = true,
        AttributionMethod = "shapley",
        CreatedAt = Created
    });

    /// <summary>Report 601, holding its rows in the report dimension.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(new ReportRecord { ReportId = 601, ReportKey = "income_statement", ReportDimensionId = 300 });

    /// <summary>The report's live version over the model.</summary>
    public InMemoryTableGateway<ReportVersionRecord> ReportVersions { get; } = new(new ReportVersionRecord
    {
        ReportVersionId = 602,
        ReportId = 601,
        SnapshotId = SnapshotRows.LiveId,
        DisplayName = "Income statement",
        ReportType = "financial",
        CalculationModelId = 401,
        SubtotalPosition = SubtotalPositions.Above,
        IsEnabled = true,
        CreatedAt = Created
    });

    /// <summary>The catalog reader over these tables.</summary>
    /// <returns>The reader.</returns>
    public TenantCatalogReader CatalogReader()
    {
        return new TenantCatalogReader(
            new TenantInfoReader(DatabaseInfo), Live(), Snapshots, Datasets, DatasetVersions, Dimensions, DimensionVersions, Roles, MappingSets, MappingVersions,
            Bridges, BridgeVersions, Models, ModelVersions, Reports, ReportVersions);
    }

    /// <summary>The overview service over these tables.</summary>
    /// <returns>The service.</returns>
    public OverviewService Overview()
    {
        return new OverviewService(CatalogReader(), Measures);
    }

    /// <summary>The published-members reader over these tables.</summary>
    /// <returns>The reader.</returns>
    public PublishedMembersReader PublishedMembers()
    {
        return new PublishedMembersReader(Live(), DimensionVersions, Levels, Members);
    }

    /// <summary>The live snapshot over the snapshot table.</summary>
    private LiveSnapshot Live()
    {
        return new LiveSnapshot(Snapshots);
    }

    /// <summary>An enabled dataset version over a table.</summary>
    private static DatasetVersionRecord DatasetVersion(long datasetVersionId, long datasetId, long snapshotId)
    {
        return new DatasetVersionRecord
        {
            CreatedAt = Created,
            DatasetVersionId = datasetVersionId,
            DatasetId = datasetId,
            SnapshotId = snapshotId,
            IsEnabled = true,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = ExpressionLanguageVersions.Version10
        };
    }

    /// <summary>A sum measure of a dataset version.</summary>
    private static MeasureRecord Measure(long measureId, long datasetVersionId)
    {
        return new MeasureRecord
        {
            MeasureId = measureId,
            DatasetVersionId = datasetVersionId,
            MeasureKey = $"measure_{measureId}",
            DisplayName = $"Measure {measureId}",
            MeasureKind = MeasureKinds.Aggregate,
            AggregationFunction = AggregationFunctions.Sum,
            ValueNullBehavior = ValueNullBehaviors.Exclude,
            NegativeWeightBehavior = NegativeWeightBehaviors.Allow,
            EmptySetBehavior = EmptySetBehaviors.Null,
            BridgeFactorBehavior = BridgeFactorBehaviors.Value,
            BridgeAllocationBehavior = BridgeAllocationBehaviors.Apply,
            TotalBehavior = TotalBehaviors.Recompute,
            OutputDataType = DataTypes.Decimal,
            AccumulatorPrecision = 38,
            AccumulatorScale = 6,
            IsEnabled = true
        };
    }

    /// <summary>A hierarchical user dimension.</summary>
    private static DimensionRecord Dimension(long dimensionId, string key, string purpose)
    {
        return new DimensionRecord
        {
            DimensionId = dimensionId,
            DimensionKey = key,
            DisplayName = key,
            DimensionKind = DimensionKinds.Hierarchical,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = true,
            IsEnabled = true,
            DimensionPurpose = purpose
        };
    }

    /// <summary>A version of the dimension its id's hundreds name.</summary>
    private static DimensionVersionRecord DimensionVersion(long dimensionVersionId, long snapshotId)
    {
        return new DimensionVersionRecord
        {
            CreatedAt = Created,
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionVersionId / 100 * 100,
            SnapshotId = snapshotId
        };
    }

    /// <summary>A member of a dimension version.</summary>
    private static DimensionMemberRecord Member(long memberId, long versionId, string key, long? parentId, long? levelId, int sortOrder, bool isAssignable)
    {
        return new DimensionMemberRecord
        {
            DimensionMemberId = memberId,
            DimensionVersionId = versionId,
            MemberKey = key,
            DisplayName = key,
            ParentDimensionMemberId = parentId,
            DimensionLevelId = levelId,
            IsAssignable = isAssignable,
            SortOrder = sortOrder
        };
    }

    /// <summary>A bridge.</summary>
    private static BridgeRecord Bridge(long bridgeId, string key, long sourceDimensionId, long targetDimensionId)
    {
        return new BridgeRecord
        {
            BridgeId = bridgeId,
            BridgeKey = key,
            DisplayName = key,
            SourceDimensionId = sourceDimensionId,
            TargetDimensionId = targetDimensionId
        };
    }

    /// <summary>An enabled, many-to-one live version of a bridge.</summary>
    private static BridgeVersionRecord BridgeVersion(long bridgeVersionId, long bridgeId)
    {
        return new BridgeVersionRecord
        {
            BridgeVersionId = bridgeVersionId,
            BridgeId = bridgeId,
            SnapshotId = SnapshotRows.LiveId,
            Cardinality = Cardinalities.ManyToOne,
            AllocationMode = AllocationModes.None,
            IsEnabled = true,
            CreatedAt = Created
        };
    }
}
