using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Dimensions.DimensionRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms that publishing a dimension version through the workflow closes the open-ended published predecessor at the new
/// version's start and leaves every other version alone: a closed or later-starting published version, a draft, and other
/// dimensions' versions. Without the correction two published windows would overlap from the new start onward, and the
/// engine could not pick one version per request time.
/// </summary>
public sealed class PredecessorValidityCloserTests
{
    /// <summary>The dimension being republished.</summary>
    private const long DimensionId = 1;

    /// <summary>The validated version that gets published.</summary>
    private const long NewVersionId = 13;

    /// <summary>The start of the newly published version.</summary>
    private static readonly DateTime NewStart = Utc(2026, 3, 1);

    /// <summary>The fixed clock of the workflow.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 17, 30, 0, TimeSpan.Zero));

    /// <summary>The version table.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        Version(10, DimensionId, 1, PublicationStatuses.Published, Utc(2024, 1, 1), Utc(2025, 1, 1)),
        Version(11, DimensionId, 2, PublicationStatuses.Published, Utc(2025, 1, 1), null),
        Version(12, DimensionId, 3, PublicationStatuses.Draft, Utc(2025, 6, 1), null),
        Version(NewVersionId, DimensionId, 4, PublicationStatuses.Validated, NewStart, null),
        Version(20, 2, 1, PublicationStatuses.Published, Utc(2025, 1, 1), null));

    /// <summary>The tenant's database info row, whose semantic revision publishing advances.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 4,
        SemanticRevision = 7,
        UpdatedAt = Utc(2026, 1, 1)
    });

    /// <summary>Publishing v4 from 2026-03-01 ends the open-ended published v2 there; v1, the draft v3 and dimension 2 keep their windows.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Publishing_ClosesOnlyTheOpenEndedPredecessor()
    {
        var before = _versions.All.ToDictionary(version => version.DimensionVersionId);
        var workflow = new VersionWorkflow(
            new VersionStoreRegistry([new DimensionVersionStore(_versions)]),
            [],
            [new PredecessorValidityCloser(_versions)],
            new SemanticRevision(_databaseInfo, _time),
            _time);

        var result = await workflow.TransitionAsync(
            VersionKinds.DimensionVersion, NewVersionId, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        var after = _versions.All.ToDictionary(version => version.DimensionVersionId);
        Assert.Equal(before[11] with { ValidTo = NewStart }, after[11]);
        Assert.Equal(before[10], after[10]);
        Assert.Equal(before[12], after[12]);
        Assert.Equal(before[20], after[20]);
        Assert.Equal(before[NewVersionId] with { Status = PublicationStatuses.Published, PublishedAt = _time.GetUtcNow().UtcDateTime }, after[NewVersionId]);
        Assert.Equal(8, Assert.Single(_databaseInfo.All).SemanticRevision);
    }
}
