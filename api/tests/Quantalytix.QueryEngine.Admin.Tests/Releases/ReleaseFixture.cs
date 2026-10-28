using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.AssumptionSets;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Tenancy;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using static Quantalytix.QueryEngine.Admin.Tests.Releases.ReleaseRows;
using static Quantalytix.QueryEngine.Admin.Tests.Reports.ReportGraphFixture;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// A tenant for the release and assumption set tests, over the report rules' tenant (<see cref="ReportGraphFixture"/>): release 900
/// (<c>prod/2026-q3</c>) is published, active and pins GL v11, which the enabled report <c>balance_sheet</c> reads; release 902
/// (<c>prod/2026-q2</c>) is published and pins GL v11 too; release 901 (<c>prod/2026-q4</c>) is an empty draft. Loans has published
/// v1 (21) and draft v2 (22). GL v11 binds the account dimension 800 and the region dimension 600, each with published v1, and the
/// calendar dimension 700 through a time binding. The active set pins both, the report's dimension 500 and the bridge 95 into it;
/// the historical set <c>fy2026_q2</c>, captured in June over release 900, pins only the account dimension.
/// </summary>
internal sealed class ReleaseFixture
{
    /// <summary>The draft release.</summary>
    public const long DraftReleaseId = 901;

    /// <summary>A published release that is not active.</summary>
    public const long PreviousReleaseId = 902;

    /// <summary>Loans' published v1.</summary>
    public const long LoansPublishedVersionId = LoansVersionId;

    /// <summary>Loans' draft v2.</summary>
    public const long LoansDraftVersionId = 22;

    /// <summary>The region dimension GL's region binding and the report's calculation dimension read.</summary>
    public const long RegionDimensionId = 600;

    /// <summary>The active set.</summary>
    public const long ActiveSetId = 1;

    /// <summary>The historical set.</summary>
    public const long HistoricalSetId = 2;

    /// <summary>The tenant's semantic revision before each test.</summary>
    public const long StartingRevision = 1;

    /// <summary>When the historical set was captured.</summary>
    public static readonly DateTime CapturedAt = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The clock's time throughout each test.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>When the seeded living versions were published.</summary>
    private static readonly DateTime PublishedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The report rules' tables, which also hold the datasets, the manifest, the bindings and the database info.</summary>
    public ReportGraphFixture Graph { get; } = new();

    /// <summary>The releases.</summary>
    public InMemoryTableGateway<SemanticReleaseRecord> Releases { get; } = new(
        Release(SemanticReleaseId, "prod/2026-q3", PublicationStatuses.Published),
        Release(DraftReleaseId, "prod/2026-q4", PublicationStatuses.Draft),
        Release(PreviousReleaseId, "prod/2026-q2", PublicationStatuses.Published));

    /// <summary>GL v11, loans v21 and the loans draft v22.</summary>
    public InMemoryTableGateway<DatasetVersionRecord> DatasetVersions { get; } = new(
        DatasetVersion(GlVersionId, GlDatasetId, 1, PublicationStatuses.Published),
        DatasetVersion(LoansPublishedVersionId, LoansDatasetId, 1, PublicationStatuses.Published),
        DatasetVersion(LoansDraftVersionId, LoansDatasetId, 2, PublicationStatuses.Draft));

    /// <summary>No activation logged yet.</summary>
    public InMemoryTableGateway<ReleaseActivationRecord> Activations { get; } = new();

    /// <summary>The report's dimension 500, the region and account dimensions and the calendar dimension.</summary>
    public InMemoryTableGateway<DimensionRecord> Dimensions { get; } = new(
        Dimension(ReportRows.ReportDimensionId, "balance_sheet") with { DimensionPurpose = DimensionPurposes.Report },
        Dimension(RegionDimensionId, "region"),
        Dimension(700, "calendar"),
        Dimension(AccountDimensionId, "account"));

    /// <summary>No mapping sets.</summary>
    public InMemoryTableGateway<MappingSetRecord> MappingSets { get; } = new();

    /// <summary>No mapping versions.</summary>
    public InMemoryTableGateway<MappingVersionRecord> MappingVersions { get; } = new();

    /// <summary>The active and the historical set.</summary>
    public InMemoryTableGateway<AssumptionSetRecord> Sets { get; } = new(Set(ActiveSetId, "active", null), Set(HistoricalSetId, "fy2026_q2", CapturedAt));

    /// <summary>The active set pins region, account and the report's dimension; the historical set pins account.</summary>
    public InMemoryTableGateway<AssumptionSetDimensionRecord> DimensionPins { get; } = new(
        DimensionPin(ActiveSetId, RegionDimensionId, 601),
        DimensionPin(ActiveSetId, AccountDimensionId, 801),
        DimensionPin(ActiveSetId, ReportRows.ReportDimensionId, 501),
        DimensionPin(HistoricalSetId, AccountDimensionId, 801));

    /// <summary>No mapping pins.</summary>
    public InMemoryTableGateway<AssumptionSetMappingRecord> MappingPins { get; } = new();

    /// <summary>The active set pins the bridge into the report's dimension.</summary>
    public InMemoryTableGateway<AssumptionSetBridgeRecord> BridgePins { get; } = new(
        new AssumptionSetBridgeRecord { AssumptionSetId = ActiveSetId, BridgeId = AccountBridgeId, BridgeVersionId = AccountBridgeVersionId });

    /// <summary>The fixed clock.</summary>
    public FakeTimeProvider Time { get; } = new(Now);

    /// <summary>Hands out ids from 5000.</summary>
    public SequentialIdGenerator Ids { get; } = new(5000);

    /// <summary>The fixture with release 902's manifest, release 900 activated in the database info, and published versions of the dimensions.</summary>
    /// <returns>The fixture.</returns>
    public static async Task<ReleaseFixture> CreateAsync()
    {
        var fixture = new ReleaseFixture();
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var graph = fixture.Graph;
        await graph.Manifest.InsertAsync(new ReleaseDatasetRecord { SemanticReleaseId = PreviousReleaseId, DatasetId = GlDatasetId, DatasetVersionId = GlVersionId }, cancellationToken);
        await graph.DimensionVersions.UpdateAsync(graph.DimensionVersions.All.Single() with { PublishedAt = PublishedAt }, cancellationToken);
        await graph.DimensionVersions.InsertManyAsync(
            [
                DimensionRows.Version(601, RegionDimensionId, 1, PublicationStatuses.Published) with { PublishedAt = PublishedAt },
                DimensionRows.Version(801, AccountDimensionId, 1, PublicationStatuses.Published) with { PublishedAt = PublishedAt }
            ],
            cancellationToken);
        await graph.BridgeVersions.UpdateAsync(graph.BridgeVersions.All.Single() with { PublishedAt = PublishedAt }, cancellationToken);
        return fixture;
    }

    /// <summary>The tenant's semantic revision now.</summary>
    public long CurrentRevision => Graph.DatabaseInfo.All.Single().SemanticRevision;

    /// <summary>The tenant info reader.</summary>
    /// <returns>The reader.</returns>
    public TenantInfoReader Tenant()
    {
        return new TenantInfoReader(Graph.DatabaseInfo);
    }

    /// <summary>The release snapshot loader.</summary>
    /// <returns>The loader.</returns>
    public ReleaseSnapshotLoader Loader()
    {
        return new ReleaseSnapshotLoader(Releases, Graph.Manifest, Graph.Datasets, DatasetVersions, Activations, Tenant());
    }

    /// <summary>The manifest rules in the contract's order, and the report rules on releases.</summary>
    /// <returns>The rule runner.</returns>
    public ReleaseRules Rules()
    {
        var reader = new ReleaseManifestReader(Graph.Bindings, new BindingNames(Graph.Roles, Graph.Fields));
        IReleaseRule[] rules =
        [
            new ManifestNotEmptyRule(),
            new PinnedVersionBelongsToDatasetRule(),
            new PinnedDatasetVersionPublishedRule(),
            new BoundDimensionsResolvableRule(reader, Graph.DimensionVersions, Dimensions),
            new MappedBindingsResolvableRule(reader, MappingVersions, MappingSets),
            new BridgeBindingsResolvableRule(reader, Graph.BridgeVersions, Graph.Bridges)
        ];
        return new ReleaseRules(rules, ReportChecks(), Time);
    }

    /// <summary>The report checks with the report rules a release can break.</summary>
    /// <returns>The checks.</returns>
    public ReleaseReportChecks ReportChecks()
    {
        return new ReleaseReportChecks(Graph.Reader(), [new ReportSourcesResolveRule(), new ReportFactorsApplyRule()]);
    }

    /// <summary>The release reads.</summary>
    /// <returns>The queries.</returns>
    public ReleaseQueries Queries()
    {
        return new ReleaseQueries(Releases, Graph.Manifest, Activations, Tenant(), Loader(), ReportReader(), Rules());
    }

    /// <summary>The release commands.</summary>
    /// <returns>The commands.</returns>
    public ReleaseCommands Commands()
    {
        return new ReleaseCommands(
            Releases,
            Graph.Manifest,
            Activations,
            Graph.DatabaseInfo,
            Loader(),
            Rules(),
            ReportChecks(),
            new ReleaseNotActiveRule(Tenant(), Releases),
            Tenant(),
            Revision(),
            Ids,
            Time);
    }

    /// <summary>The manifest change set.</summary>
    /// <returns>The change set service.</returns>
    public ReleaseChanges Changes()
    {
        var pipeline = new ChangeSetPipeline<ReleaseSnapshot, ReleaseOperation, ReleaseDetail>(
            new ReleaseAggregate(Loader(), Graph.Manifest),
            [new ReleaseGateRule(Rules())],
            [new ReleaseOperationValidator()],
            Ids,
            Revision());
        return new ReleaseChanges(pipeline, Releases, Loader(), Rules(), Queries());
    }

    /// <summary>What reports read.</summary>
    /// <returns>The reader.</returns>
    public ReleaseReportReader ReportReader()
    {
        return new ReleaseReportReader(
            Graph.Reports, Graph.Sources, Graph.SourceVariables, Graph.Dimensions, Graph.Roles, Graph.BridgeVersions, Graph.BridgeMappings, Graph.Bindings);
    }

    /// <summary>The assumption set pins.</summary>
    /// <returns>The pins.</returns>
    public AssumptionPins Pins()
    {
        return new AssumptionPins(Sets, DimensionPins, MappingPins, BridgePins);
    }

    /// <summary>The assumption set reads.</summary>
    /// <returns>The queries.</returns>
    public AssumptionSetQueries SetQueries()
    {
        return new AssumptionSetQueries(Sets, Releases, Graph.Manifest, Graph.Datasets, Pins(), Identities(), ReportReader(), Queries(), Tenant());
    }

    /// <summary>The assumption set commands.</summary>
    /// <returns>The commands.</returns>
    public AssumptionSetCommands SetCommands()
    {
        return new AssumptionSetCommands(Sets, Pins(), Identities(), Tenant(), Revision(), Ids, Time);
    }

    /// <summary>The identities sets pin.</summary>
    /// <returns>The reader.</returns>
    public PinIdentityReader Identities()
    {
        return new PinIdentityReader(Dimensions, Graph.DimensionVersions, MappingSets, MappingVersions, Graph.Bridges, Graph.BridgeVersions, Graph.Reports);
    }

    /// <summary>The semantic revision over the database info.</summary>
    private SemanticRevision Revision()
    {
        return new SemanticRevision(Graph.DatabaseInfo, Time);
    }

    /// <summary>A dimension pin.</summary>
    private static AssumptionSetDimensionRecord DimensionPin(long setId, long dimensionId, long versionId)
    {
        return new AssumptionSetDimensionRecord { AssumptionSetId = setId, DimensionId = dimensionId, DimensionVersionId = versionId };
    }
}
