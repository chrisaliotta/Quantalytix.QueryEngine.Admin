using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms the version lifecycle. Leaving draft runs the kind's lifecycle handlers and then its publication rules, and any
/// violation keeps the draft. Publishing a living kind, such as a dimension version, retires the version it replaces, keeping
/// that version's publication time, and advances the semantic revision, because the published version is what queries read
/// unless they name a historical assumption set; a living version is never retired by hand. A strict kind, such as a dataset
/// version, keeps its earlier published versions for releases to pin, publishes without advancing the revision, because only a
/// release activation brings it to queries, and may be retired. A transition the lifecycle skips is refused, and the Validate
/// check runs the rules without changing anything. The query engine keys its metadata cache by the semantic revision.
/// </summary>
public sealed class VersionWorkflowTests
{
    /// <summary>The published v1 of dimension 100, which v3's publish replaces.</summary>
    private const long PublishedDimensionVersionId = 101;

    /// <summary>A draft dimension version.</summary>
    private const long DraftDimensionVersionId = 102;

    /// <summary>A validated dimension version.</summary>
    private const long ValidatedDimensionVersionId = 103;

    /// <summary>A published dataset version.</summary>
    private const long PublishedDatasetVersionId = 201;

    /// <summary>A draft dataset version.</summary>
    private const long DraftDatasetVersionId = 202;

    /// <summary>A validated dataset version.</summary>
    private const long ValidatedDatasetVersionId = 203;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>When the published versions were published.</summary>
    private static readonly DateTime FirstPublishedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The clock's time throughout each test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>The calls of the recording rules and handlers, in order.</summary>
    private readonly List<string> _log = [];

    /// <summary>The fixed clock the workflow and the semantic revision read.</summary>
    private readonly FakeTimeProvider _time = new(Now);

    /// <summary>The dimension version table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionVersion(PublishedDimensionVersionId, 1, PublicationStatuses.Published) with { PublishedAt = FirstPublishedAt },
        DimensionVersion(DraftDimensionVersionId, 2, PublicationStatuses.Draft),
        DimensionVersion(ValidatedDimensionVersionId, 3, PublicationStatuses.Validated));

    /// <summary>The dataset version table.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _datasetVersions = new(
        ReleaseRows.DatasetVersion(PublishedDatasetVersionId, 200, 1, PublicationStatuses.Published) with { PublishedAt = FirstPublishedAt },
        ReleaseRows.DatasetVersion(DraftDatasetVersionId, 200, 2, PublicationStatuses.Draft),
        ReleaseRows.DatasetVersion(ValidatedDatasetVersionId, 200, 3, PublicationStatuses.Validated));

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 11,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The version stores over this test's tables.</summary>
    private readonly VersionStoreRegistry _stores;

    /// <summary>Builds the version stores over this test's tables.</summary>
    public VersionWorkflowTests()
    {
        _stores = new VersionStoreRegistry([new DimensionVersionStore(_dimensionVersions), new DatasetVersionStore(_datasetVersions)]);
    }

    /// <summary>Validation derives data first, then checks it: the kind's handlers run before its rules, and other kinds' do not run.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Validating_RunsTheKindsHandlersThenItsRules()
    {
        var workflow = Workflow(
            [Rule(VersionKinds.DimensionVersion), Rule(VersionKinds.DatasetVersion)],
            [Handler(VersionKinds.DimensionVersion), Handler(VersionKinds.DatasetVersion)]);

        var result = await workflow.TransitionAsync(
            VersionKinds.DimensionVersion, DraftDimensionVersionId, PublicationStatuses.Validated, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal([$"dimension_version validating {DraftDimensionVersionId}", $"dimension_version rule {DraftDimensionVersionId}"], _log);
        Assert.Equal(PublicationStatuses.Validated, DimensionVersionRow(DraftDimensionVersionId).Status);
    }

    /// <summary>A rule violation rejects the transition with the violations and leaves the version a draft.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Violation_KeepsTheDraft()
    {
        var violation = new RuleViolation { Rule = "closure_mismatch", Message = "The closure table does not match the member tree." };
        var workflow = Workflow([Rule(VersionKinds.DimensionVersion, violation)], []);

        var result = await workflow.TransitionAsync(
            VersionKinds.DimensionVersion, DraftDimensionVersionId, PublicationStatuses.Validated, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Rejected, result.Outcome);
        Assert.Equal([violation], result.Violations);
        Assert.Equal(PublicationStatuses.Draft, result.State.Status);
        Assert.Equal(PublicationStatuses.Draft, DimensionVersionRow(DraftDimensionVersionId).Status);
    }

    /// <summary>
    /// Publishing v3 of a dimension stamps its publication time, runs the publish handlers, retires the published v1 with its
    /// original publication time, leaves the draft v2 alone and advances the semantic revision.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task PublishingLivingVersion_RetiresItsPredecessorAndAdvancesTheRevision()
    {
        var workflow = Workflow([], [new PredecessorRetirer(VersionKinds.DimensionVersion, _stores), Handler(VersionKinds.DimensionVersion)]);

        var result = await workflow.TransitionAsync(
            VersionKinds.DimensionVersion, ValidatedDimensionVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal((PublicationStatuses.Published, Now.UtcDateTime), Published(ValidatedDimensionVersionId));
        Assert.Equal((PublicationStatuses.Retired, FirstPublishedAt), Published(PublishedDimensionVersionId));
        Assert.Equal(PublicationStatuses.Draft, DimensionVersionRow(DraftDimensionVersionId).Status);
        Assert.Equal([$"dimension_version published {ValidatedDimensionVersionId}"], _log);
        var databaseInfo = Assert.Single(_databaseInfo.All);
        Assert.Equal(StartingRevision + 1, databaseInfo.SemanticRevision);
        Assert.Equal(Now.UtcDateTime, databaseInfo.UpdatedAt);
    }

    /// <summary>
    /// Publishing a dataset version stamps the publication time, keeps the earlier published version for releases that pin it,
    /// and keeps the semantic revision, because dataset versions reach queries only through a release activation.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task PublishingStrictVersion_KeepsItsPredecessorAndTheRevision()
    {
        var result = await Workflow([], []).TransitionAsync(
            VersionKinds.DatasetVersion, ValidatedDatasetVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal(Now.UtcDateTime, DatasetVersionRow(ValidatedDatasetVersionId).PublishedAt);
        Assert.Equal(PublicationStatuses.Published, DatasetVersionRow(PublishedDatasetVersionId).Status);
        Assert.Equal(StartingRevision, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>A dataset version cannot skip validation: draft to published is not allowed, and nothing runs or changes.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task DatasetVersionDraftToPublished_IsNotAllowed()
    {
        var workflow = Workflow([Rule(VersionKinds.DatasetVersion)], [Handler(VersionKinds.DatasetVersion)]);

        var result = await workflow.TransitionAsync(
            VersionKinds.DatasetVersion, DraftDatasetVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.NotAllowed, result.Outcome);
        Assert.Equal(PublicationStatuses.Draft, result.State.Status);
        Assert.Equal(PublicationStatuses.Draft, DatasetVersionRow(DraftDatasetVersionId).Status);
        Assert.Empty(_log);
    }

    /// <summary>A published dimension version cannot be retired by hand, while a published dataset version can.</summary>
    /// <returns>A task that completes when the transitions are checked.</returns>
    [Fact]
    public async Task OnlyStrictVersions_AreRetiredByHand()
    {
        var workflow = Workflow([], []);
        var cancellationToken = TestContext.Current.CancellationToken;

        var living = await workflow.TransitionAsync(VersionKinds.DimensionVersion, PublishedDimensionVersionId, PublicationStatuses.Retired, cancellationToken);
        var strict = await workflow.TransitionAsync(VersionKinds.DatasetVersion, PublishedDatasetVersionId, PublicationStatuses.Retired, cancellationToken);

        Assert.Equal(TransitionOutcome.NotAllowed, living.Outcome);
        Assert.Equal(PublicationStatuses.Published, DimensionVersionRow(PublishedDimensionVersionId).Status);
        Assert.Equal(TransitionOutcome.Applied, strict.Outcome);
        Assert.Equal(PublicationStatuses.Retired, DatasetVersionRow(PublishedDatasetVersionId).Status);
    }

    /// <summary>The Validate check of a published version reports the rules' violations and runs no handler and changes nothing.</summary>
    /// <returns>A task that completes when the check is checked.</returns>
    [Fact]
    public async Task Check_ReportsViolationsWithoutChangingTheVersion()
    {
        var violation = new RuleViolation { Rule = "dimension_consumers_resolve", Message = "Binding 'Branch' of dataset 'loans' names member 'BR09', which v1 lacks." };
        var workflow = Workflow([Rule(VersionKinds.DimensionVersion, violation)], [Handler(VersionKinds.DimensionVersion)]);

        var violations = await workflow.CheckAsync(VersionKinds.DimensionVersion, PublishedDimensionVersionId, TestContext.Current.CancellationToken);

        Assert.Equal([violation], violations);
        Assert.Equal([$"dimension_version rule {PublishedDimensionVersionId}"], _log);
        Assert.Equal((PublicationStatuses.Published, FirstPublishedAt), Published(PublishedDimensionVersionId));
        Assert.Equal(StartingRevision, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>The workflow over this test's stores and clock, with the given rules and handlers.</summary>
    private VersionWorkflow Workflow(IEnumerable<IPublicationRule> rules, IEnumerable<IVersionLifecycleHandler> handlers)
    {
        return new VersionWorkflow(_stores, rules, [], handlers, new PublicationSet(), new SemanticRevision(_databaseInfo, _time), _time);
    }

    /// <summary>A rule of a kind that logs to this test's log and reports the given violations.</summary>
    private RecordingPublicationRule Rule(string kind, params IReadOnlyList<RuleViolation> violations)
    {
        return new RecordingPublicationRule(kind, _log, violations);
    }

    /// <summary>A handler of a kind that logs to this test's log.</summary>
    private RecordingLifecycleHandler Handler(string kind)
    {
        return new RecordingLifecycleHandler(kind, _log);
    }

    /// <summary>The status and publication time of a dimension version.</summary>
    private (string Status, DateTime? PublishedAt) Published(long dimensionVersionId)
    {
        var row = DimensionVersionRow(dimensionVersionId);
        return (row.Status, row.PublishedAt);
    }

    /// <summary>The current row of a dimension version.</summary>
    private DimensionVersionRecord DimensionVersionRow(long dimensionVersionId)
    {
        return _dimensionVersions.All.Single(row => row.DimensionVersionId == dimensionVersionId);
    }

    /// <summary>The current row of a dataset version.</summary>
    private DatasetVersionRecord DatasetVersionRow(long datasetVersionId)
    {
        return _datasetVersions.All.Single(row => row.DatasetVersionId == datasetVersionId);
    }

    /// <summary>A version of dimension 100.</summary>
    private static DimensionVersionRecord DimensionVersion(long dimensionVersionId, int versionNumber, string status)
    {
        return new DimensionVersionRecord { DimensionVersionId = dimensionVersionId, DimensionId = 100, VersionNumber = versionNumber, Status = status };
    }
}
