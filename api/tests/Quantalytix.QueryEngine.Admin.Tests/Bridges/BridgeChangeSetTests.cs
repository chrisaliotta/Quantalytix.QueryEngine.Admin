using System.Globalization;
using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms the analytical bridge change set (OI-01, OI-26, B5, B8, B9, B11): the live version's settings change in place, mappings
/// that no longer comply are flagged in the preview and refuse the commit until fixed; input outside B5 and B8 is refused before it
/// applies; and a report bridge is never written here.
/// </summary>
public sealed class BridgeChangeSetTests
{
    /// <summary>The analytical bridge's live version, as operations name it.</summary>
    private static readonly string Live = MappingsFixture.LiveBridgeVersionId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// OI-26, B11: switching to partition with a second target at 0.5 leaves CA's weights at 1.5, which the preview flags on CA and the
    /// commit refuses; with both weights at 0.5 the commit stores the settings on the same live version.
    /// </summary>
    /// <returns>A task that completes when the change sets are checked.</returns>
    [Fact]
    public async Task SettingsEditedInPlace_RefuseTheCommitUntilTheMappingsComply()
    {
        var fixture = new MappingsFixture();
        var changes = fixture.BridgeChanges();
        var existing = fixture.BridgeMappings.All.Single(mapping => mapping.BridgeVersionId == MappingsFixture.LiveBridgeVersionId).BridgeMappingId;
        var partition = new BridgeVersionUpdateOperation
        {
            VersionId = Live,
            Patch = new BridgeVersionPatch { Cardinality = Cardinalities.ManyToMany, AllocationMode = AllocationModes.Partition }
        };
        var east = Save("tmp:east", "CA", "EAST", 0.5m);

        var preview = await changes.PreviewAsync(MappingsFixture.BridgeId, Request(partition, east), TestContext.Current.CancellationToken);
        var refused = await changes.CommitAsync(MappingsFixture.BridgeId, Request(partition, east), TestContext.Current.CancellationToken);
        var committed = await changes.CommitAsync(
            MappingsFixture.BridgeId,
            Request(partition, east, Save(existing.ToString(CultureInfo.InvariantCulture), "CA", "WEST", 0.5m)),
            TestContext.Current.CancellationToken);

        var finding = Assert.Single(preview.Value!.Violations);
        Assert.Equal(("allocation_partition", "CA"), (finding.Code, finding.Target!.ItemKey));
        Assert.Equal(RefusalKind.RulesViolated, refused.Refusal!.Kind);
        Assert.True(committed.Succeeded);
        var version = fixture.BridgeVersions.All.Single(row => row.BridgeVersionId == MappingsFixture.LiveBridgeVersionId);
        Assert.Equal((Cardinalities.ManyToMany, AllocationModes.Partition), (version.Cardinality, version.AllocationMode));
        Assert.Equal([1m], committed.Value.State.Content.Allocation.Sources.Select(source => source.WeightSum));
    }

    /// <summary>B5: a cardinality and allocation mode that do not go together are invalid input on the allocation mode.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task SettingsOutsideB5_AreInvalid()
    {
        var update = new BridgeVersionUpdateOperation
        {
            VersionId = Live,
            Patch = new BridgeVersionPatch { Cardinality = Cardinalities.ManyToOne, AllocationMode = AllocationModes.Partition }
        };

        var preview = await new MappingsFixture().BridgeChanges().PreviewAsync(MappingsFixture.BridgeId, Request(update), TestContext.Current.CancellationToken);

        Assert.Equal(RefusalKind.Invalid, preview.Refusal!.Kind);
        Assert.Contains("operations[0].patch.allocationMode", preview.Refusal.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>B8, OI-28: an analytical bridge maps each member alone, so a descendants selection is invalid input.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task DescendantsSelection_IsInvalidOnAnAnalyticalBridge()
    {
        var save = new BridgeMappingSaveOperation
        {
            VersionId = Live,
            Input = new BridgeMappingInput { Id = "tmp:ny", SourceMemberKey = "US", TargetMemberKey = "EAST", Factor = 1m, AllocationWeight = 1m, SourceSelection = SourceSelections.Descendants }
        };

        var preview = await new MappingsFixture().BridgeChanges().PreviewAsync(MappingsFixture.BridgeId, Request(save), TestContext.Current.CancellationToken);

        Assert.Contains("operations[0].input.sourceSelection", preview.Refusal!.Errors.Keys, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>B9: a report bridge's change set is refused as owned by a report, whatever it holds.</summary>
    /// <returns>A task that completes when the change set is checked.</returns>
    [Fact]
    public async Task ReportBridge_IsOwnedByItsReport()
    {
        var update = new BridgeUpdateOperation { Patch = new BridgePatch { IsEnabled = false } };

        var preview = await new MappingsFixture().BridgeChanges().PreviewAsync(MappingsFixture.ReportBridgeId, Request(update), TestContext.Current.CancellationToken);

        Assert.Equal((RefusalKind.Conflict, BridgeRefusals.OwnedByReportTitle), (preview.Refusal!.Kind, preview.Refusal.Title));
    }

    /// <summary>A save of one mapping into the live version.</summary>
    private static BridgeMappingSaveOperation Save(string id, string source, string target, decimal weight)
    {
        return new BridgeMappingSaveOperation
        {
            VersionId = Live,
            Input = new BridgeMappingInput { Id = id, SourceMemberKey = source, TargetMemberKey = target, Factor = 1m, AllocationWeight = weight }
        };
    }

    /// <summary>A change set of operations.</summary>
    private static ChangeSetRequest<BridgeOperation> Request(params BridgeOperation[] operations)
    {
        return new ChangeSetRequest<BridgeOperation> { Operations = operations };
    }
}
