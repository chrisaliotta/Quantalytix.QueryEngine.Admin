using Microsoft.Extensions.Time.Testing;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Bridges.BridgeRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms that publishing a bridge version through the workflow points its bridge's <c>current_bridge_version_id</c> at
/// it, replacing the previous pointer, while validating moves nothing and other bridges keep theirs. The pointer is how
/// authors find a bridge's current version (schema section 10), so it must follow each publish.
/// </summary>
public sealed class CurrentBridgeVersionUpdaterTests
{
    /// <summary>The fixed clock of the workflow.</summary>
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 17, 30, 0, TimeSpan.Zero));

    /// <summary>The bridges: bridge 1 points at its published v1; bridge 2 has no current version.</summary>
    private readonly InMemoryTableGateway<BridgeRecord> _bridges = new(Bridge(1, 1, 2, currentVersionId: 49), Bridge(2, 1, 3));

    /// <summary>The bridge versions: bridge 1's published v1 and validated v2, and bridge 2's draft v1.</summary>
    private readonly InMemoryTableGateway<BridgeVersionRecord> _versions = new(
        BridgeVersion(49, 1, 1, 10, 20, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published),
        BridgeVersion(50, 1, 2, 11, 20, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Validated),
        BridgeVersion(60, 2, 1, 10, 30, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Draft));

    /// <summary>The tenant's database info row, whose semantic revision publishing advances.</summary>
    private readonly InMemoryTableGateway<DatabaseInfoRecord> _databaseInfo = new(new DatabaseInfoRecord
    {
        SingletonId = 1,
        TenantKey = "sandbox",
        MetadataSchemaVersion = 4,
        SemanticRevision = 7,
        UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>Publishing bridge 1's v2 moves its pointer from v1 to v2; bridge 2 keeps no pointer, and the revision advances once.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Publishing_PointsTheBridgeAtTheVersion()
    {
        var result = await Workflow().TransitionAsync(VersionKinds.BridgeVersion, 50, PublicationStatuses.Published, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Equal([(1L, (long?)50), (2L, null)], _bridges.All.Select(bridge => (bridge.BridgeId, bridge.CurrentBridgeVersionId)));
        Assert.Equal(8, Assert.Single(_databaseInfo.All).SemanticRevision);
    }

    /// <summary>Validating bridge 2's draft leaves its pointer empty: only a published version becomes current.</summary>
    /// <returns>A task that completes when the transition is checked.</returns>
    [Fact]
    public async Task Validating_LeavesThePointer()
    {
        var result = await Workflow().TransitionAsync(VersionKinds.BridgeVersion, 60, PublicationStatuses.Validated, TestContext.Current.CancellationToken);

        Assert.Equal(TransitionOutcome.Applied, result.Outcome);
        Assert.Null(_bridges.All.Single(bridge => bridge.BridgeId == 2).CurrentBridgeVersionId);
    }

    /// <summary>A workflow over the bridge versions with no rules and the updater as the only handler.</summary>
    private VersionWorkflow Workflow()
    {
        return new VersionWorkflow(
            new VersionStoreRegistry([new BridgeVersionStore(_versions)]),
            [],
            [new CurrentBridgeVersionUpdater(_versions, _bridges)],
            new SemanticRevision(_databaseInfo, _time),
            _time);
    }
}
