using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Quantalytix.QueryEngine.Admin.Tests.Versioning;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Changes;

/// <summary>
/// Confirms the bridge from the existing rules to snapshot rules: until a module migrates its <see cref="IPublicationRule"/>s,
/// the adapter runs every rule of the snapshot's kind, unchanged, on the stored version the snapshot came from, and reports
/// their violations as publication findings; rules of other kinds do not run.
/// </summary>
public sealed class VersionRuleAdapterTests
{
    /// <summary>The kind's rules run on the snapshot's version and their violations come back as publication findings.</summary>
    /// <returns>A task that completes when the adapter is checked.</returns>
    [Fact]
    public async Task Adapter_RunsTheKindsRulesOnTheSnapshotsVersion()
    {
        var log = new List<string>();
        var violation = new RuleViolation { Code = "hierarchy_acyclic", Message = "Members A -> B -> A form a parent cycle." };
        var versions = new InMemoryTableGateway<DimensionVersionRecord>(
            new DimensionVersionRecord { CreatedAt = DateTime.UnixEpoch, DimensionVersionId = 10, DimensionId = 1, VersionNumber = 1, Status = PublicationStatuses.Draft });
        var databaseInfo = new InMemoryTableGateway<DatabaseInfoRecord>();
        var workflow = new VersionWorkflow(
            new VersionStoreRegistry([new DimensionVersionStore(versions)]),
            [new RecordingPublicationRule(VersionKinds.DimensionVersion, log, violation), new RecordingPublicationRule(VersionKinds.DatasetVersion, log)],
            [],
            [],
            new PublicationSet(),
            new SemanticRevision(databaseInfo, new FakeTimeProvider()),
            new FakeTimeProvider());
        var adapter = new VersionRuleAdapter<VersionSnapshot>(workflow);

        var violations = await adapter.EvaluateAsync(new VersionSnapshot(VersionKinds.DimensionVersion, 10), TestContext.Current.CancellationToken);

        Assert.Equal(RuleStage.Publication, adapter.Stage);
        Assert.Equal([violation], violations);
        Assert.Equal(["dimension_version rule 10"], log);
    }

    /// <summary>A snapshot of one stored version.</summary>
    /// <param name="Kind">The version kind.</param>
    /// <param name="VersionId">The version.</param>
    private sealed record VersionSnapshot(string Kind, long VersionId) : IVersionSnapshot;
}
