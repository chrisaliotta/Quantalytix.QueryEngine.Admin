using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms why a report's drafts move as one set. Each publication rule checks a draft's keys against the published versions of
/// the objects it names: <c>dimension_consumers_resolve</c> refuses a report dimension draft that drops a row a published bridge
/// still maps to, and <c>mapping_members_in_endpoint_versions</c> refuses a bridge draft that maps to a row the published report
/// dimension lacks. A draft that replaces row OLD with row NEW, beside a bridge draft that remaps account 1001 from OLD to NEW,
/// therefore validates in neither order alone. Validated together, each draft counts as its identity's published version, so the
/// set passes, and publishing it retires both predecessors and advances the semantic revision once.
/// </summary>
public sealed class PublishTogetherTests
{
    /// <summary>The report dimension, whose members are a report's rows.</summary>
    private const long ReportDimensionId = 100;

    /// <summary>The published version of the report dimension: TOTAL and OLD.</summary>
    private const long PublishedStructureId = 101;

    /// <summary>The draft of the report dimension: TOTAL and NEW.</summary>
    private const long DraftStructureId = 102;

    /// <summary>The account dimension the bridge maps from.</summary>
    private const long AccountDimensionId = 200;

    /// <summary>The published version of the account dimension: account 1001.</summary>
    private const long AccountVersionId = 201;

    /// <summary>The bridge from accounts into the report dimension.</summary>
    private const long BridgeId = 300;

    /// <summary>The bridge's published version, which maps 1001 to OLD.</summary>
    private const long PublishedBridgeVersionId = 301;

    /// <summary>The bridge's draft, which maps 1001 to NEW.</summary>
    private const long DraftBridgeVersionId = 302;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 4;

    /// <summary>The dimension version table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionRows.Version(PublishedStructureId, ReportDimensionId, 1, PublicationStatuses.Published),
        DimensionRows.Version(DraftStructureId, ReportDimensionId, 2, PublicationStatuses.Draft),
        DimensionRows.Version(AccountVersionId, AccountDimensionId, 1, PublicationStatuses.Published));

    /// <summary>The members of the three dimension versions.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(1, PublishedStructureId, "TOTAL"),
        DimensionRows.Member(2, PublishedStructureId, "OLD", parentId: 1),
        DimensionRows.Member(3, DraftStructureId, "TOTAL"),
        DimensionRows.Member(4, DraftStructureId, "NEW", parentId: 3),
        DimensionRows.Member(5, AccountVersionId, "1001"));

    /// <summary>The bridge version table.</summary>
    private readonly InMemoryTableGateway<BridgeVersionRecord> _bridgeVersions = new(
        BridgeRows.BridgeVersion(PublishedBridgeVersionId, BridgeId, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published),
        BridgeRows.BridgeVersion(DraftBridgeVersionId, BridgeId, 2, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Draft));

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 9,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The report dimension draft and the bridge draft, in the order a design publishes them.</summary>
    private static readonly VersionReference[] Drafts =
    [
        new() { Kind = VersionKinds.DimensionVersion, Id = DraftStructureId },
        new() { Kind = VersionKinds.BridgeVersion, Id = DraftBridgeVersionId }
    ];

    /// <summary>Each draft alone breaks a rule that reads the other's published version, so neither order validates.</summary>
    /// <returns>A task that completes when the transitions are checked.</returns>
    [Fact]
    public async Task OneAtATime_TheDraftsAreRefusedInEitherOrder()
    {
        var workflow = Workflow();
        var cancellationToken = TestContext.Current.CancellationToken;

        var structure = await workflow.TransitionAsync(VersionKinds.DimensionVersion, DraftStructureId, PublicationStatuses.Validated, cancellationToken);
        var bridge = await workflow.TransitionAsync(VersionKinds.BridgeVersion, DraftBridgeVersionId, PublicationStatuses.Validated, cancellationToken);

        Assert.Equal(TransitionOutcome.Rejected, structure.Outcome);
        Assert.Equal(DimensionConsumersResolveRule.Code, Assert.Single(structure.Violations).Rule);
        Assert.Equal(TransitionOutcome.Rejected, bridge.Outcome);
        Assert.Equal(MappingMembersInEndpointVersionsRule.Code, Assert.Single(bridge.Violations).Rule);
        Assert.Equal(PublicationStatuses.Draft, Status(DraftStructureId));
        Assert.Equal(PublicationStatuses.Draft, BridgeStatus(DraftBridgeVersionId));
    }

    /// <summary>Together the drafts validate, and publishing them retires both predecessors and advances the revision once.</summary>
    /// <returns>A task that completes when the transitions are checked.</returns>
    [Fact]
    public async Task Together_TheDraftsValidateAndPublish()
    {
        var workflow = Workflow();
        var cancellationToken = TestContext.Current.CancellationToken;

        var validated = await workflow.TransitionAsync(Drafts, PublicationStatuses.Validated, cancellationToken);
        var published = await workflow.TransitionAsync(Drafts, PublicationStatuses.Published, cancellationToken);

        Assert.Equal(TransitionOutcome.Applied, validated.Outcome);
        Assert.Equal(TransitionOutcome.Applied, published.Outcome);
        Assert.Equal(
            [PublicationStatuses.Published, PublicationStatuses.Published],
            published.States.Select(state => state.Status));
        Assert.Equal((PublicationStatuses.Retired, PublicationStatuses.Published), (Status(PublishedStructureId), Status(DraftStructureId)));
        Assert.Equal((PublicationStatuses.Retired, PublicationStatuses.Published), (BridgeStatus(PublishedBridgeVersionId), BridgeStatus(DraftBridgeVersionId)));
        Assert.Equal(StartingRevision + 1, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>
    /// The workflow over the dimension and bridge versions, with the two rules that read each other's published versions and the
    /// predecessor retirers; the rules and the workflow share one publication set, as one request's services do.
    /// </summary>
    private VersionWorkflow Workflow()
    {
        var publicationSet = new PublicationSet();
        var levels = new InMemoryTableGateway<DimensionLevelRecord>();
        var published = new PublishedDimensionReader(_dimensionVersions, levels, _members, publicationSet);
        var bridges = new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(BridgeId, AccountDimensionId, ReportDimensionId));
        var mappings = new InMemoryTableGateway<BridgeMappingRecord>(
            BridgeRows.Mapping(PublishedBridgeVersionId, "1001", "OLD", 1m),
            BridgeRows.Mapping(DraftBridgeVersionId, "1001", "NEW", 1m));
        var roles = new InMemoryTableGateway<DimensionRoleRecord>();
        var consumers = new DimensionConsumers(
            new InMemoryTableGateway<DatasetRecord>(), new InMemoryTableGateway<DatasetVersionRecord>(), new InMemoryTableGateway<DimensionBindingRecord>(), roles,
            new InMemoryTableGateway<MappingSetRecord>(), new InMemoryTableGateway<MappingVersionRecord>(), new InMemoryTableGateway<DimensionMappingRecord>(),
            bridges, _bridgeVersions, mappings,
            new InMemoryTableGateway<ReportRecord>(), new InMemoryTableGateway<ReportVersionRecord>(), new InMemoryTableGateway<ReportMemberOptionRecord>(),
            new InMemoryTableGateway<ReportLineRecord>(), new InMemoryTableGateway<ReportVariableScopeRecord>(), new InMemoryTableGateway<CalculationDimensionRecord>(),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()), publicationSet);
        var stores = new VersionStoreRegistry([new DimensionVersionStore(_dimensionVersions), new BridgeVersionStore(_bridgeVersions)]);
        IPublicationRule[] rules =
        [
            new DimensionConsumersResolveRule(_dimensionVersions, consumers, published),
            new MappingMembersInEndpointVersionsRule(new BridgeMappingSetReader(_bridgeVersions, bridges, mappings, published))
        ];
        IVersionLifecycleHandler[] handlers =
        [
            new PredecessorRetirer(VersionKinds.DimensionVersion, stores),
            new PredecessorRetirer(VersionKinds.BridgeVersion, stores)
        ];
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
        return new VersionWorkflow(stores, rules, [], handlers, publicationSet, new SemanticRevision(_databaseInfo, time), time);
    }

    /// <summary>The status of a dimension version.</summary>
    private string Status(long dimensionVersionId)
    {
        return _dimensionVersions.All.Single(row => row.DimensionVersionId == dimensionVersionId).Status;
    }

    /// <summary>The status of a bridge version.</summary>
    private string BridgeStatus(long bridgeVersionId)
    {
        return _bridgeVersions.All.Single(row => row.BridgeVersionId == bridgeVersionId).Status;
    }
}
