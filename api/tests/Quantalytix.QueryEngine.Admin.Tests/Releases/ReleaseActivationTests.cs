using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms release activation: a published release becomes the tenant's active release, with the update time stamped and
/// the semantic revision advanced; a draft release is refused before anything is written, which the endpoint answers
/// with 409. Activation is the only way dataset versions reach queries, and the query engine keys its metadata cache by
/// the semantic revision, so the pointer must move only to a release the engine can read, and always with a new revision.
/// </summary>
public sealed class ReleaseActivationTests
{
    /// <summary>The release that is active before each test.</summary>
    private const long ActiveSemanticReleaseId = 500;

    /// <summary>A published release that is not active.</summary>
    private const long PublishedReleaseId = 501;

    /// <summary>A draft release.</summary>
    private const long DraftReleaseId = 502;

    /// <summary>The tenant's semantic revision before each test.</summary>
    private const long StartingRevision = 7;

    /// <summary>The clock's time throughout each test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>The database info's update time before each test.</summary>
    private static readonly DateTime Earlier = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The fixed clock the activator and the semantic revision read.</summary>
    private readonly FakeTimeProvider _time = new(Now);

    /// <summary>The release table.</summary>
    private readonly InMemoryTableGateway<SemanticReleaseRecord> _releases = new(
        Release(ActiveSemanticReleaseId, "development/baseline", PublicationStatuses.Published),
        Release(PublishedReleaseId, "zz_rel_next", PublicationStatuses.Published),
        Release(DraftReleaseId, "zz_rel_draft", PublicationStatuses.Draft));

    /// <summary>The tenant's singleton database info row.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 4,
        SemanticRevision = StartingRevision,
        ActiveSemanticReleaseId = ActiveSemanticReleaseId,
        UpdatedAt = Earlier
    });

    /// <summary>Activating a published release points the tenant at it, stamps the update time and advances the revision.</summary>
    /// <returns>A task that completes when the activation is checked.</returns>
    [Fact]
    public async Task PublishedRelease_BecomesActiveAndAdvancesTheRevision()
    {
        var result = await Activator().ActivateAsync(PublishedReleaseId, TestContext.Current.CancellationToken);

        Assert.True(result.Activated);
        var info = Assert.Single(_databaseInfo.All);
        Assert.Equal(PublishedReleaseId, info.ActiveSemanticReleaseId);
        Assert.Equal(StartingRevision + 1, info.SemanticRevision);
        Assert.Equal(Now.UtcDateTime, info.UpdatedAt);
        Assert.Equal(info, result.DatabaseInfo);
    }

    /// <summary>Activating a draft release is refused with its status, and the pointer, revision and update time stay as they were.</summary>
    /// <returns>A task that completes when the activation is checked.</returns>
    [Fact]
    public async Task DraftRelease_IsRefusedAndNothingChanges()
    {
        var result = await Activator().ActivateAsync(DraftReleaseId, TestContext.Current.CancellationToken);

        Assert.False(result.Activated);
        Assert.Equal(PublicationStatuses.Draft, result.Release.Status);
        var info = Assert.Single(_databaseInfo.All);
        Assert.Equal(ActiveSemanticReleaseId, info.ActiveSemanticReleaseId);
        Assert.Equal(StartingRevision, info.SemanticRevision);
        Assert.Equal(Earlier, info.UpdatedAt);
    }

    /// <summary>The activator over this test's tables and clock.</summary>
    private ReleaseActivator Activator()
    {
        return new ReleaseActivator(_releases, _databaseInfo, new SemanticRevision(_databaseInfo, _time), _time);
    }

    /// <summary>A release created at the start of 2026.</summary>
    private static SemanticReleaseRecord Release(long semanticReleaseId, string releaseKey, string status)
    {
        return new SemanticReleaseRecord { SemanticReleaseId = semanticReleaseId, ReleaseKey = releaseKey, Status = status, CreatedAt = Earlier };
    }
}
