using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Overview;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Overview;

/// <summary>
/// Confirms the overview's numbers: identity rows are counted whatever their status, drafts are the rows in
/// <c>draft</c> status of each versioned kind, and active measures are only the measures of the dataset versions the
/// active release pins; without an active release there are none. The overview is where an administrator sees what
/// queries currently use and what work is still in progress.
/// </summary>
public sealed class OverviewTests
{
    /// <summary>The active release.</summary>
    private const long ActiveReleaseId = 500;

    /// <summary>When every seeded row was created.</summary>
    private static readonly DateTime Created = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>A published, a draft and a retired release.</summary>
    private readonly InMemoryTableGateway<SemanticReleaseRecord> _releases = new(
        Release(ActiveReleaseId, PublicationStatuses.Published),
        Release(501, PublicationStatuses.Draft),
        Release(502, PublicationStatuses.Retired));

    /// <summary>The active release pins loans v1 and deposits v1; the draft release pins loans v2.</summary>
    private readonly InMemoryTableGateway<ReleaseDatasetRecord> _manifest = new(
        new ReleaseDatasetRecord { ReleaseId = ActiveReleaseId, DatasetId = 1, DatasetVersionId = 11 },
        new ReleaseDatasetRecord { ReleaseId = ActiveReleaseId, DatasetId = 2, DatasetVersionId = 21 },
        new ReleaseDatasetRecord { ReleaseId = 501, DatasetId = 1, DatasetVersionId = 12 });

    /// <summary>Two datasets.</summary>
    private readonly InMemoryTableGateway<DatasetRecord> _datasets = new(
        new DatasetRecord { DatasetId = 1, DatasetKey = "loans", DisplayName = "Loans", IsEnabled = true },
        new DatasetRecord { DatasetId = 2, DatasetKey = "deposits", DisplayName = "Deposits", IsEnabled = true });

    /// <summary>Loans v1 published and v2 draft; deposits v1 published and v2 draft.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _datasetVersions = new(
        DatasetVersion(11, 1, PublicationStatuses.Published),
        DatasetVersion(12, 1, PublicationStatuses.Draft),
        DatasetVersion(21, 2, PublicationStatuses.Published),
        DatasetVersion(22, 2, PublicationStatuses.Draft));

    /// <summary>Two measures in loans v1, one in deposits v1 and three in the draft loans v2.</summary>
    private readonly InMemoryTableGateway<MeasureRecord> _measures = new(
        Measure(1, 11), Measure(2, 11), Measure(3, 21), Measure(4, 12), Measure(5, 12), Measure(6, 12));

    /// <summary>Three dimensions.</summary>
    private readonly InMemoryTableGateway<DimensionRecord> _dimensions = new(Dimension(100), Dimension(200), Dimension(300));

    /// <summary>One published and two draft dimension versions.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionVersion(101, PublicationStatuses.Published),
        DimensionVersion(102, PublicationStatuses.Draft),
        DimensionVersion(201, PublicationStatuses.Draft));

    /// <summary>One mapping set.</summary>
    private readonly InMemoryTableGateway<MappingSetRecord> _mappingSets = new(new MappingSetRecord
    {
        MappingSetId = 61,
        MappingSetKey = "core_branch",
        SourceNamespace = "core",
        DimensionId = 100,
        DisplayName = "Core branch codes"
    });

    /// <summary>One draft mapping version.</summary>
    private readonly InMemoryTableGateway<MappingVersionRecord> _mappingVersions = new(new MappingVersionRecord
    {
        MappingVersionId = 71,
        MappingSetId = 61,
        VersionNumber = 1,
        DimensionVersionId = 101,
        Status = PublicationStatuses.Draft
    });

    /// <summary>One bridge.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(new BridgeRecord
    {
        BridgeId = 81,
        BridgeKey = "branch_to_region",
        DisplayName = "Branch to region",
        SourceDimensionId = 100,
        TargetDimensionId = 200,
        IsEnabled = true
    });

    /// <summary>One published bridge version.</summary>
    private readonly InMemoryTableGateway<BridgeVersionRecord> _bridgeVersions = new(new BridgeVersionRecord
    {
        BridgeVersionId = 82,
        BridgeId = 81,
        VersionNumber = 1,
        SourceDimensionVersionId = 101,
        TargetDimensionVersionId = 201,
        Cardinality = Cardinalities.ManyToOne,
        AllocationMode = AllocationModes.None,
        Status = PublicationStatuses.Published
    });

    /// <summary>A draft and a published assumption set.</summary>
    private readonly InMemoryTableGateway<AssumptionSetRecord> _assumptionSets = new(
        AssumptionSet(301, AssumptionSetStatuses.Draft),
        AssumptionSet(302, AssumptionSetStatuses.Published));

    /// <summary>One calculation model.</summary>
    private readonly InMemoryTableGateway<CalculationModelRecord> _models = new(new CalculationModelRecord
    {
        ModelId = 401,
        ModelKey = "yield",
        DisplayName = "Yield",
        IsEnabled = true
    });

    /// <summary>The model's published v1 and draft v2.</summary>
    private readonly InMemoryTableGateway<CalculationModelVersionRecord> _modelVersions = new(
        ModelVersion(411, PublicationStatuses.Published),
        ModelVersion(412, PublicationStatuses.Draft));

    /// <summary>Two reports.</summary>
    private readonly InMemoryTableGateway<ReportRecord> _reports = new(Report(601), Report(602));

    /// <summary>One draft report version.</summary>
    private readonly InMemoryTableGateway<ReportVersionRecord> _reportVersions = new(new ReportVersionRecord
    {
        ReportVersionId = 611,
        ReportId = 601,
        VersionNumber = 1,
        ReportDimensionId = 300,
        ModelVersionId = 411,
        Status = PublicationStatuses.Draft
    });

    /// <summary>The overview reports the tenant's identity and active release, counts every identity row, and counts drafts per kind.</summary>
    /// <returns>A task that completes when the overview is checked.</returns>
    [Fact]
    public async Task CountsRowsAndDrafts()
    {
        var overview = await Service(DatabaseInfo(ActiveReleaseId)).GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("sandbox", overview.TenantKey);
        Assert.Equal(4, overview.MetadataSchemaVersion);
        Assert.Equal(12, overview.SemanticRevision);
        Assert.Equal(ActiveReleaseId, overview.ActiveRelease?.ReleaseId);
        Assert.Equal(
            new OverviewCounts
            {
                Datasets = 2,
                Dimensions = 3,
                MappingSets = 1,
                Bridges = 1,
                Releases = 3,
                AssumptionSets = 2,
                CalculationModels = 1,
                Reports = 2,
                ActiveMeasures = 3
            },
            overview.Counts);
        Assert.Equal(
            new OverviewDrafts
            {
                DatasetVersions = 2,
                DimensionVersions = 2,
                MappingVersions = 1,
                BridgeVersions = 0,
                Releases = 1,
                AssumptionSets = 1,
                ModelVersions = 1,
                ReportVersions = 1
            },
            overview.Drafts);
    }

    /// <summary>Without an active release there is no active release record and no active measure.</summary>
    /// <returns>A task that completes when the overview is checked.</returns>
    [Fact]
    public async Task NoActiveRelease_HasNoActiveMeasures()
    {
        var overview = await Service(DatabaseInfo(null)).GetAsync(TestContext.Current.CancellationToken);

        Assert.Null(overview.ActiveRelease);
        Assert.Equal(0, overview.Counts.ActiveMeasures);
    }

    /// <summary>The service over this test's tables and the given database info row.</summary>
    private OverviewService Service(DatabaseInfoRecord databaseInfo)
    {
        return new OverviewService(
            new InMemoryTableGateway<DatabaseInfoRecord>(databaseInfo),
            _releases,
            _manifest,
            _datasets,
            _datasetVersions,
            _measures,
            _dimensions,
            _dimensionVersions,
            _mappingSets,
            _mappingVersions,
            _bridges,
            _bridgeVersions,
            _assumptionSets,
            _models,
            _modelVersions,
            _reports,
            _reportVersions);
    }

    /// <summary>The sandbox tenant's database info, pointing at the given active release.</summary>
    private static DatabaseInfoRecord DatabaseInfo(long? activeReleaseId)
    {
        return new DatabaseInfoRecord
        {
            SingletonId = 1,
            TenantKey = "sandbox",
            MetadataSchemaVersion = 4,
            SemanticRevision = 12,
            ActiveReleaseId = activeReleaseId,
            UpdatedAt = Created
        };
    }

    /// <summary>A version of the yield model.</summary>
    private static CalculationModelVersionRecord ModelVersion(long modelVersionId, string status)
    {
        return new CalculationModelVersionRecord
        {
            ModelVersionId = modelVersionId,
            ModelId = 401,
            VersionNumber = (int)(modelVersionId % 10),
            Status = status,
            CalendarKey = "gregorian",
            CurrencyCode = "USD",
            FormulaLanguageVersion = "1.0"
        };
    }

    /// <summary>A report.</summary>
    private static ReportRecord Report(long reportId)
    {
        return new ReportRecord { ReportId = reportId, ReportKey = $"report_{reportId}", DisplayName = $"Report {reportId}", ReportType = "financial", IsEnabled = true };
    }

    /// <summary>A release.</summary>
    private static SemanticReleaseRecord Release(long releaseId, string status)
    {
        return new SemanticReleaseRecord { ReleaseId = releaseId, ReleaseKey = $"release_{releaseId}", Status = status, CreatedAt = Created };
    }

    /// <summary>A dataset version over a table.</summary>
    private static DatasetVersionRecord DatasetVersion(long datasetVersionId, long datasetId, string status)
    {
        return new DatasetVersionRecord
        {
            DatasetVersionId = datasetVersionId,
            DatasetId = datasetId,
            VersionNumber = (int)(datasetVersionId % 10),
            Status = status,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = "1.0"
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

    /// <summary>A categorical dimension.</summary>
    private static DimensionRecord Dimension(long dimensionId)
    {
        return new DimensionRecord
        {
            DimensionId = dimensionId,
            DimensionKey = $"dimension_{dimensionId}",
            DisplayName = $"Dimension {dimensionId}",
            DimensionKind = DimensionKinds.Categorical,
            DefinitionOrigin = DefinitionOrigins.User,
            IsRemovable = true,
            IsEnabled = true
        };
    }

    /// <summary>A dimension version of the dimension its ID's hundreds name.</summary>
    private static DimensionVersionRecord DimensionVersion(long dimensionVersionId, string status)
    {
        return new DimensionVersionRecord
        {
            DimensionVersionId = dimensionVersionId,
            DimensionId = dimensionVersionId / 100 * 100,
            VersionNumber = (int)(dimensionVersionId % 100),
            Status = status
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
            EffectiveAt = Created,
            Status = status,
            CreatedAt = Created
        };
    }
}
