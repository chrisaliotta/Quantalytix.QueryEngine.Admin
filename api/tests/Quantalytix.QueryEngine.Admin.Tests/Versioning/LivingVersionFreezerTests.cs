using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Versioning;

/// <summary>
/// Confirms the freeze of a living kind (blueprint 12 "Freeze", OI-14): the live version n must pass every rule of its kind;
/// then n+1 is copied from it and published, n is retired, and the semantic revision advances once, because queries now read n+1.
/// A rule violation refuses the freeze with the violations (422 over HTTP) and changes nothing; a version that is not live is
/// refused as a conflict (409). Dimension versions stand in for every living kind; the copier is the module's hook.
/// </summary>
public sealed class LivingVersionFreezerTests
{
    /// <summary>The live version n.</summary>
    private const long LiveVersionId = 101;

    /// <summary>A draft version.</summary>
    private const long DraftVersionId = 102;

    /// <summary>The id the copier gives n+1.</summary>
    private const long CopyVersionId = 103;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The fixed clock.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero));

    /// <summary>The rule and handler calls, in order.</summary>
    private readonly List<string> _log = [];

    /// <summary>The dimension version table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        Version(LiveVersionId, 1, PublicationStatuses.Published),
        Version(DraftVersionId, 2, PublicationStatuses.Draft));

    /// <summary>The tenant's database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 18,
        SemanticRevision = StartingRevision,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>
    /// A passing freeze publishes the copy as live, retires n with its retirement time and note (SG-04, SG-05), and advances the
    /// revision.
    /// </summary>
    /// <returns>A task that completes when the freeze is checked.</returns>
    [Fact]
    public async Task Freeze_PublishesTheCopyAndRetiresTheLiveVersion()
    {
        var outcome = await Freezer().FreezeAsync(VersionKinds.DimensionVersion, LiveVersionId, "FY26 close", TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded);
        Assert.Equal(CopyVersionId, outcome.Value.LiveVersionId);
        Assert.Equal(LiveVersionId, outcome.Value.Report.VersionId);
        Assert.Empty(outcome.Value.Report.Violations);
        var frozen = _versions.All.Single(row => row.DimensionVersionId == LiveVersionId);
        Assert.Equal(PublicationStatuses.Retired, frozen.Status);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, frozen.RetiredAt);
        Assert.Equal("FY26 close", frozen.FreezeNote);
        Assert.Equal(PublicationStatuses.Published, Status(CopyVersionId));
        Assert.Equal(StartingRevision + 1, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>A rule violation on n refuses the freeze with the violations; nothing is copied, retired or advanced.</summary>
    /// <returns>A task that completes when the freeze is checked.</returns>
    [Fact]
    public async Task RuleViolation_RefusesAndChangesNothing()
    {
        var violation = new RuleViolation { Code = "member_key_unique", Message = "Key 'CA' is used twice." };

        var outcome = await Freezer(violation).FreezeAsync(VersionKinds.DimensionVersion, LiveVersionId, null, TestContext.Current.CancellationToken);

        Assert.Equal(RefusalKind.RulesViolated, outcome.Refusal!.Kind);
        Assert.Equal([violation], outcome.Refusal.Violations);
        Assert.Equal(PublicationStatuses.Published, Status(LiveVersionId));
        Assert.Equal(2, _versions.All.Count);
        Assert.Equal(StartingRevision, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>Only the live version can be frozen: a draft is refused as a conflict before any rule runs.</summary>
    /// <returns>A task that completes when the freeze is checked.</returns>
    [Fact]
    public async Task VersionThatIsNotLive_RefusesWithConflict()
    {
        var outcome = await Freezer().FreezeAsync(VersionKinds.DimensionVersion, DraftVersionId, null, TestContext.Current.CancellationToken);

        Assert.Equal(RefusalKind.Conflict, outcome.Refusal!.Kind);
        Assert.Equal(LivingVersionFreezer.NotLiveTitle, outcome.Refusal.Title);
        Assert.Empty(_log);
    }

    /// <summary>The freezer over the workflow, with a dimension rule that reports the given violations and the predecessor retirer.</summary>
    private LivingVersionFreezer Freezer(params IReadOnlyList<RuleViolation> violations)
    {
        var stores = new VersionStoreRegistry([new DimensionVersionStore(_versions)]);
        var workflow = new VersionWorkflow(
            stores,
            [new RecordingPublicationRule(VersionKinds.DimensionVersion, _log, violations)],
            [],
            [new PredecessorRetirer(VersionKinds.DimensionVersion, stores, _time)],
            new PublicationSet(),
            new SemanticRevision(_databaseInfo, _time),
            _time);
        return new LivingVersionFreezer(stores, workflow, [new CopyingCopier(_versions)], _time);
    }

    /// <summary>A version's status.</summary>
    private string Status(long versionId)
    {
        return _versions.All.Single(row => row.DimensionVersionId == versionId).Status;
    }

    /// <summary>A version of dimension 100.</summary>
    private static DimensionVersionRecord Version(long id, int number, string status)
    {
        return new DimensionVersionRecord { CreatedAt = DateTime.UnixEpoch, DimensionVersionId = id, DimensionId = 100, VersionNumber = number, Status = status };
    }

    /// <summary>A copier that stores n+1 as a draft of the same dimension, as a module's copier does with its content.</summary>
    /// <param name="versions">The dimension version table.</param>
    private sealed class CopyingCopier(InMemoryTableGateway<DimensionVersionRecord> versions) : ILivingVersionCopier
    {
        /// <summary><c>dimension_version</c>.</summary>
        public string Kind => VersionKinds.DimensionVersion;

        /// <summary>Stores the draft copy.</summary>
        /// <param name="sourceVersionId">The live version.</param>
        /// <param name="cancellationToken">Cancels the write.</param>
        /// <returns>The copy's id.</returns>
        public async Task<long> CopyAsync(long sourceVersionId, CancellationToken cancellationToken)
        {
            await versions.InsertAsync(Version(CopyVersionId, 3, PublicationStatuses.Draft), cancellationToken);
            return CopyVersionId;
        }
    }
}
