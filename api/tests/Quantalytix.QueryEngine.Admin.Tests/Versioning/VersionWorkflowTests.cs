using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms the version lifecycle: leaving draft runs the kind's lifecycle handlers and then its publication rules, and any
/// violation keeps the draft; publishing stamps the publication time and advances the semantic revision only for kinds whose
/// published content reaches queries without a release activation; a transition the lifecycle skips is refused; assumption
/// sets publish straight from draft. Every feature's versions move through this workflow, and the query engine keys its
/// metadata cache by the semantic revision.
/// </summary>
public sealed class VersionWorkflowTests
{
    /// <summary>A draft dimension version.</summary>
    private const long DraftDimensionVersionId = 101;

    /// <summary>A validated dimension version.</summary>
    private const long ValidatedDimensionVersionId = 102;

    /// <summary>A draft dataset version.</summary>
    private const long DraftDatasetVersionId = 201;

    /// <summary>A validated dataset version.</summary>
    private const long ValidatedDatasetVersionId = 202;

    /// <summary>A draft assumption set.</summary>
    private const long DraftAssumptionSetId = 301;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The clock's time throughout each test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>The calls of the recording rules and handlers, in order.</summary>
    private readonly List<string> _log = [];

    /// <summary>The fixed clock the workflow and the semantic revision read.</summary>
    private readonly FakeTimeProvider _time = new(Now);

    /// <summary>The dimension version table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _dimensionVersions = new(
        DimensionVersion(DraftDimensionVersionId, 2, PublicationStatuses.Draft),
        DimensionVersion(ValidatedDimensionVersionId, 3, PublicationStatuses.Validated));

    /// <summary>The dataset version table.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _datasetVersions = new(
        DatasetVersion(DraftDatasetVersionId, 2, PublicationStatuses.Draft),
        DatasetVersion(ValidatedDatasetVersionId, 3, PublicationStatuses.Validated));

    /// <summary>The assumption set table.</summary>
    private readonly InMemoryTableGateway<AssumptionSetRecord> _assumptionSets = new(new AssumptionSetRecord
    {
        AssumptionSetId = DraftAssumptionSetId,
        AssumptionSetKey = "FY2026_Q3_CLOSE",
        DisplayName = "FY2026 Q3 close",
        EffectiveAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
        Status = AssumptionSetStatuses.Draft,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 4,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

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
    /// Publishing a dimension version stamps the publication time, runs the publish cascade and advances the semantic revision,
    /// because queries resolve published dimension versions directly.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task PublishingDimensionVersion_StampsTimeAndAdvancesRevision()
    {
        var workflow = Workflow([], [Handler(VersionKinds.DimensionVersion)]);

        var result = await workflow.TransitionAsync(
            VersionKinds.DimensionVersion, ValidatedDimensionVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal(Now.UtcDateTime, result.State.PublishedAt);
        var row = DimensionVersionRow(ValidatedDimensionVersionId);
        Assert.Equal(PublicationStatuses.Published, row.Status);
        Assert.Equal(Now.UtcDateTime, row.PublishedAt);
        Assert.Equal([$"dimension_version published {ValidatedDimensionVersionId}"], _log);
        var databaseInfo = Assert.Single(_databaseInfo.All);
        Assert.Equal(StartingRevision + 1, databaseInfo.SemanticRevision);
        Assert.Equal(Now.UtcDateTime, databaseInfo.UpdatedAt);
    }

    /// <summary>
    /// Publishing a dataset version stamps the publication time but keeps the semantic revision, because dataset versions reach
    /// queries only through a release activation, which advances it.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task PublishingDatasetVersion_KeepsTheRevision()
    {
        var result = await Workflow([], []).TransitionAsync(
            VersionKinds.DatasetVersion, ValidatedDatasetVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal(Now.UtcDateTime, DatasetVersionRow(ValidatedDatasetVersionId).PublishedAt);
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

    /// <summary>
    /// An assumption set, which has no validated state, publishes straight from draft: its rules run, the publication time is
    /// stamped and the semantic revision advances.
    /// </summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task AssumptionSet_PublishesFromDraft()
    {
        var workflow = Workflow([Rule(VersionKinds.AssumptionSet)], []);

        var result = await workflow.TransitionAsync(
            VersionKinds.AssumptionSet, DraftAssumptionSetId, AssumptionSetStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal([$"assumption_set rule {DraftAssumptionSetId}"], _log);
        var row = Assert.Single(_assumptionSets.All);
        Assert.Equal(AssumptionSetStatuses.Published, row.Status);
        Assert.Equal(Now.UtcDateTime, row.PublishedAt);
        Assert.Equal(StartingRevision + 1, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>The workflow over this test's tables and clock, with the given rules and handlers.</summary>
    private VersionWorkflow Workflow(IEnumerable<IPublicationRule> rules, IEnumerable<IVersionLifecycleHandler> handlers)
    {
        var stores = new VersionStoreRegistry(
        [
            new DimensionVersionStore(_dimensionVersions),
            new DatasetVersionStore(_datasetVersions),
            new AssumptionSetStore(_assumptionSets)
        ]);
        return new VersionWorkflow(stores, rules, handlers, new SemanticRevision(_databaseInfo, _time), _time);
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

    /// <summary>A version of dimension 100, valid from 2026.</summary>
    private static DimensionVersionRecord DimensionVersion(long dimensionVersionId, int versionNumber, string status)
    {
        return new DimensionVersionRecord
        {
            DimensionVersionId = dimensionVersionId,
            DimensionId = 100,
            VersionNumber = versionNumber,
            Status = status,
            ValidFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    /// <summary>A version of dataset 200 over a table.</summary>
    private static DatasetVersionRecord DatasetVersion(long datasetVersionId, int versionNumber, string status)
    {
        return new DatasetVersionRecord
        {
            DatasetVersionId = datasetVersionId,
            DatasetId = 200,
            VersionNumber = versionNumber,
            Status = status,
            SourceObjectKind = SourceObjectKinds.Table,
            SourceObject = "loan_daily",
            SupportsAggregate = true,
            SupportsDetail = true,
            ExpressionLanguageVersion = "1.0"
        };
    }
}
