using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Releases;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Releases;

/// <summary>
/// Confirms that retiring a release runs its retirement rules: the active release is refused with
/// <c>release_not_active</c> and stays published, while any other published release retires. The query engine reads the
/// active release among published rows only, so retiring it would leave every query of the tenant without a release.
/// </summary>
public sealed class ReleaseRetirementTests
{
    /// <summary>The release that is active.</summary>
    private const long ActiveSemanticReleaseId = 500;

    /// <summary>A published release that is not active.</summary>
    private const long InactiveReleaseId = 501;

    /// <summary>The clock's time throughout each test.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 17, 30, 0, TimeSpan.Zero);

    /// <summary>When the releases were created and published.</summary>
    private static readonly DateTime Earlier = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>The fixed clock the workflow reads.</summary>
    private readonly FakeTimeProvider _time = new(Now);

    /// <summary>The release table.</summary>
    private readonly InMemoryTableGateway<SemanticReleaseRecord> _releases = new(
        Release(ActiveSemanticReleaseId, "development/baseline"),
        Release(InactiveReleaseId, "zz_rel_previous"));

    /// <summary>The tenant's singleton database info row, pointing at the active release.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 4,
        SemanticRevision = 7,
        ActiveSemanticReleaseId = ActiveSemanticReleaseId,
        UpdatedAt = Earlier
    });

    /// <summary>Retiring the active release is rejected, and the release stays published.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task ActiveRelease_CannotBeRetired()
    {
        var result = await Workflow().TransitionAsync(
            VersionKinds.SemanticRelease, ActiveSemanticReleaseId, PublicationStatuses.Retired, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Rejected, result.Outcome);
        Assert.Equal("release_not_active", Assert.Single(result.Violations).Code);
        Assert.Equal(PublicationStatuses.Published, _releases.All.Single(row => row.SemanticReleaseId == ActiveSemanticReleaseId).Status);
    }

    /// <summary>A published release that queries no longer run on retires.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task InactiveRelease_Retires()
    {
        var result = await Workflow().TransitionAsync(
            VersionKinds.SemanticRelease, InactiveReleaseId, PublicationStatuses.Retired, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal(PublicationStatuses.Retired, _releases.All.Single(row => row.SemanticReleaseId == InactiveReleaseId).Status);
    }

    /// <summary>The workflow over this test's tables and clock, with the releases' retirement rule.</summary>
    private VersionWorkflow Workflow()
    {
        return new VersionWorkflow(
            new VersionStoreRegistry([new SemanticReleaseStore(_releases)]),
            [],
            [new ReleaseNotActiveRule(_databaseInfo, _releases)],
            [],
            new PublicationSet(),
            new SemanticRevision(_databaseInfo, _time),
            _time);
    }

    /// <summary>A published release.</summary>
    private static SemanticReleaseRecord Release(long semanticReleaseId, string releaseKey)
    {
        return new SemanticReleaseRecord
        {
            SemanticReleaseId = semanticReleaseId,
            ReleaseKey = releaseKey,
            Status = PublicationStatuses.Published,
            CreatedAt = Earlier,
            PublishedAt = Earlier
        };
    }
}
