using Quantalytix.QueryEngine.Admin.Application.Bridges;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bridges;

/// <summary>
/// Confirms <see cref="ReportBridges"/>, the reports module's only way to write report bridges (B9): a source's bridge is created live,
/// <c>many_to_many</c> / <c>replicate</c>, optionally from a copy of a sibling's rules; a row's rules are replaced without touching the
/// other rows'; rules into removed rows go; and the bridge is deleted with its pins, version and rules.
/// </summary>
public sealed class ReportBridgesTests
{
    /// <summary>A new source's bridge is live, many_to_many / replicate, pinned, and starts with a copy of the sibling's rules.</summary>
    /// <returns>A task that completes when the bridge is checked.</returns>
    [Fact]
    public async Task Create_IsLiveAndCopiesTheSiblingsRules()
    {
        var fixture = new MappingsFixture();
        var input = new ReportBridgeInput
        {
            Key = "balance_sheet.gl2",
            DisplayName = "GL 2",
            SourceDimensionId = MappingsFixture.BranchDimensionId,
            ReportDimensionId = ReportRows.ReportDimensionId,
            CopyFromBridgeId = MappingsFixture.ReportBridgeId
        };

        var bridgeId = await fixture.ReportBridges().CreateAsync(input, TestContext.Current.CancellationToken);

        var version = fixture.BridgeVersions.All.Single(row => row.BridgeId == bridgeId);
        Assert.Equal((PublicationStatuses.Published, Cardinalities.ManyToMany, AllocationModes.Replicate), (version.Status, version.Cardinality, version.AllocationMode));
        Assert.Equal([("US", "LOANS", SourceSelections.Descendants)], fixture.BridgeMappings.All.Where(m => m.BridgeVersionId == version.BridgeVersionId).Select(m => (m.SourceMemberKey, m.TargetMemberKey, m.SourceSelection)));
        Assert.Contains(fixture.BridgePins.All, pin => pin.BridgeId == bridgeId && pin.BridgeVersionId == version.BridgeVersionId);
    }

    /// <summary>Replacing one row's rules leaves the rules of the source's other rows as they are.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task ReplaceRow_KeepsTheOtherRowsRules()
    {
        var fixture = new MappingsFixture();
        ReportBridgeRule[] rules = [new() { ItemKey = "CA", Selection = SourceSelections.Member, Factor = -1m }];

        await fixture.ReportBridges().ReplaceRowAsync(MappingsFixture.ReportBridgeId, "DEPOSITS", rules, TestContext.Current.CancellationToken);

        Assert.Equal(
            [("CA", "DEPOSITS", -1m, 1m), ("US", "LOANS", 1m, 1m)],
            fixture.BridgeMappings.All
                .Where(mapping => mapping.BridgeVersionId == MappingsFixture.ReportBridgeVersionId)
                .OrderBy(mapping => mapping.SourceMemberKey, StringComparer.Ordinal)
                .Select(mapping => (mapping.SourceMemberKey, mapping.TargetMemberKey, mapping.Factor, mapping.AllocationWeight)));
    }

    /// <summary>Rules into rows the report removed go; the bridge itself stays.</summary>
    /// <returns>A task that completes when the rules are checked.</returns>
    [Fact]
    public async Task RemoveRows_DeletesTheirRules()
    {
        var fixture = new MappingsFixture();

        await fixture.ReportBridges().RemoveRowsAsync(MappingsFixture.ReportBridgeId, ["LOANS"], TestContext.Current.CancellationToken);

        Assert.DoesNotContain(fixture.BridgeMappings.All, mapping => mapping.BridgeVersionId == MappingsFixture.ReportBridgeVersionId);
        Assert.Contains(fixture.Bridges.All, bridge => bridge.BridgeId == MappingsFixture.ReportBridgeId);
    }

    /// <summary>Deleting a source's bridge removes its pins, its version with the rules, and the bridge.</summary>
    /// <returns>A task that completes when the bridge is checked.</returns>
    [Fact]
    public async Task Delete_RemovesPinsVersionAndRules()
    {
        var fixture = new MappingsFixture();

        await fixture.ReportBridges().DeleteAsync(MappingsFixture.ReportBridgeId, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(fixture.Bridges.All, bridge => bridge.BridgeId == MappingsFixture.ReportBridgeId);
        Assert.DoesNotContain(fixture.BridgeVersions.All, version => version.BridgeId == MappingsFixture.ReportBridgeId);
        Assert.DoesNotContain(fixture.BridgeMappings.All, mapping => mapping.BridgeVersionId == MappingsFixture.ReportBridgeVersionId);
        Assert.DoesNotContain(fixture.BridgePins.All, pin => pin.BridgeId == MappingsFixture.ReportBridgeId);
    }
}
