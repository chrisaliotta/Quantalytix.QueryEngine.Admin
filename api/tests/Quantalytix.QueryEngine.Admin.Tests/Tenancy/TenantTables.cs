using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Tenancy;

/// <summary>
/// One small tenant for the tenant module's read tests: release 500 is active and pins loans v11 and deposits v21; fees has no
/// pinned version. Branch (100) is live in v102 after retiring v101, region (200) is live in v201, and report dimension 300 belongs
/// to report 601. Bridge 81 joins branch to region; bridge 82 leads into the report dimension, so report 601 owns it.
/// </summary>
internal sealed class TenantTables
{
    /// <summary>The active release.</summary>
    public const long ActiveReleaseId = 500;

    /// <summary>When every seeded row was created.</summary>
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The singleton database info row, pointing at the active release.</summary>
    public InMemoryTableGateway<DatabaseInfoRecord> DatabaseInfo { get; } = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 18,
        SemanticRevision = 12,
        ActiveSemanticReleaseId = ActiveReleaseId,
        UpdatedAt = Created
    });

    /// <summary>A published, a draft and a retired release.</summary>
    public InMemoryTableGateway<SemanticReleaseRecord> Releases { get; } = new(
        Release(ActiveReleaseId, PublicationStatuses.Published),
        Release(501, PublicationStatuses.Draft),
        Release(502, PublicationStatuses.Retired));

    /// <summary>The active release pins loans v11 and deposits v21; the draft release pins loans v12.</summary>
    public InMemoryTableGateway<ReleaseDatasetRecord> Manifest { get; } = new(
        new ReleaseDatasetRecord { SemanticReleaseId = ActiveReleaseId, DatasetId = 1, DatasetVersionId = 11 },
        new ReleaseDatasetRecord { SemanticReleaseId = ActiveReleaseId, DatasetId = 2, DatasetVersionId = 21 },
        new ReleaseDatasetRecord { SemanticReleaseId = 501, DatasetId = 1, DatasetVersionId = 12 });

    /// <summary>Loans, deposits and fees.</summary>
    public InMemoryTableGateway<DatasetRecord> Datasets { get; } = new(
        new DatasetRecord { DatasetId = 1, DatasetKey = "loans", DisplayName = "Loans", IsEnabled = true },
        new DatasetRecord { DatasetId = 2, DatasetKey = "deposits", DisplayName = "Deposits", IsEnabled = true },
        new DatasetRecord { DatasetId = 3, DatasetKey = "fees", DisplayName = "Fees", IsEnabled = false });

    /// <summary>Loans v11 published and v12 draft; deposits v21 published and v22 draft.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(
        DatasetVersion(11, 1, PublicationStatuses.Published),
        DatasetVersion(12, 1, PublicationStatuses.Draft),
        DatasetVersion(21, 2, PublicationStatuses.Published),
        DatasetVersion(22, 2, PublicationStatuses.Draft));

    /// <summary>Two measures in loans v11, one in deposits v21 and three in the draft loans v12.</summary>
    public InMemoryTableGateway<MeasureRecord> Measures { get; } = new(Measure(1, 11), Measure(2, 11), Measure(3, 21), Measure(4, 12), Measure(5, 12), Measure(6, 12));

    /// <summary>Branch and region, analytical, and report 601's report dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(100, "branch", DimensionPurposes.Analytical),
        Dimension(200, "region", DimensionPurposes.Analytical),
        Dimension(300, "income_statement_rows", DimensionPurposes.Report));

    /// <summary>Branch v101 retired and v102 live; region v201 live; the report dimension v301 live.</summary>
    public InMemoryTableGateway<DimensionVersionRecord> DimensionVersions { get; } = new(
        DimensionVersion(101, PublicationStatuses.Retired),
        DimensionVersion(102, PublicationStatuses.Published),
        DimensionVersion(201, PublicationStatuses.Published),
        DimensionVersion(301, PublicationStatuses.Published));

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
    /// and the unassignable <c>E1</c> (sort 2, level branch). The retired version holds one member of its own.
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

    /// <summary>One mapping set into branch.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new(new MappingSetRecord
    {
        MappingSetId = 61,
        MappingSetKey = "core_branch",
        SourceNamespace = "core",
        DimensionId = 100,
        DisplayName = "Core branch codes"
    });

    /// <summary>An analytical bridge from branch to region and a report bridge from region into the report dimension.</summary>
    public InMemoryTableGateway<BridgeRecord> Bridges { get; } = new(
        Bridge(81, "branch_to_region", 100, 200),
        Bridge(82, "region_to_rows", 200, 300));

    /// <summary>The active assumption set and a historical one.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> AssumptionSets { get; } = new(
        AssumptionSet(701, AssumptionSetStatuses.Active),
        AssumptionSet(702, AssumptionSetStatuses.Historical));

    /// <summary>One calculation model.</summary>
    public InMemoryTableGateway<CalculationModelRecord> Models { get; } = new(new CalculationModelRecord
    {
        CalculationModelId = 401,
        ModelKey = "yield",
        DisplayName = "Yield",
        CalendarKey = "gregorian",
        CurrencyCode = "USD",
        FormulaLanguageVersion = "1.0",
        IsEnabled = true,
        AttributionMethod = "shapley"
    });

    /// <summary>Report 601 over the model, holding its rows in the report dimension.</summary>
    public InMemoryTableGateway<ReportRecord> Reports { get; } = new(new ReportRecord
    {
        ReportId = 601,
        ReportKey = "income_statement",
        DisplayName = "Income statement",
        ReportType = "financial",
        ReportDimensionId = 300,
        CalculationModelId = 401,
        SubtotalPosition = SubtotalPositions.Above,
        IsEnabled = true
    });

    /// <summary>The catalog reader over these tables.</summary>
    /// <returns>The reader.</returns>
    public TenantCatalogReader CatalogReader()
    {
        return new TenantCatalogReader(
            new TenantInfoReader(DatabaseInfo), Manifest, Datasets, Dimensions, DimensionVersions, Roles, MappingSets, Bridges, Models, Reports, Releases, AssumptionSets);
    }

    /// <summary>The overview service over these tables.</summary>
    /// <returns>The service.</returns>
    public OverviewService Overview()
    {
        return new OverviewService(CatalogReader(), DatasetVersions, Measures);
    }

    /// <summary>The published-members reader over these tables.</summary>
    /// <returns>The reader.</returns>
    public PublishedMembersReader PublishedMembers()
    {
        return new PublishedMembersReader(DimensionVersions, Levels, Members);
    }

    /// <summary>A release.</summary>
    private static SemanticReleaseRecord Release(long semanticReleaseId, string status)
    {
        return new SemanticReleaseRecord { SemanticReleaseId = semanticReleaseId, ReleaseKey = $"R{semanticReleaseId}", Status = status, CreatedAt = Created };
    }

    /// <summary>A dataset version over a table.</summary>
    private static DatasetVersionRecord DatasetVersion(long datasetVersionId, long datasetId, string status)
    {
        return new DatasetVersionRecord
        {
            CreatedAt = Created,
            DatasetVersionId = datasetVersionId,
            DatasetId = datasetId,
            VersionNumber = (int)(datasetVersionId % 10),
            Status = status,
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

    /// <summary>A version of the dimension its id's hundreds name, numbered by its last digits.</summary>
    private static DimensionVersionRecord DimensionVersion(long dimensionVersionId, string status)
    {
        return new DimensionVersionRecord
        {
            CreatedAt = Created,
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionVersionId / 100 * 100,
            VersionNumber = (int)(dimensionVersionId % 100),
            Status = status
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

    /// <summary>An enabled bridge.</summary>
    private static BridgeRecord Bridge(long bridgeId, string key, long sourceDimensionId, long targetDimensionId)
    {
        return new BridgeRecord
        {
            BridgeId = bridgeId,
            BridgeKey = key,
            DisplayName = key,
            SourceDimensionId = sourceDimensionId,
            TargetDimensionId = targetDimensionId,
            IsEnabled = true
        };
    }

    /// <summary>An assumption set.</summary>
    private static AssumptionSetRecord AssumptionSet(long assumptionSetId, string status)
    {
        return new AssumptionSetRecord
        {
            AssumptionSetId = assumptionSetId,
            AssumptionSetKey = $"SET_{assumptionSetId}",
            DisplayName = $"Set {assumptionSetId}",
            Status = status,
            CreatedAt = Created
        };
    }
}
