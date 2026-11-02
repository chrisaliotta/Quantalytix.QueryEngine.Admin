using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;
using Quantalytix.QueryEngine.Admin.Tests.Support;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms what keeps the keys that objects name in a dimension (M10): objects resolve their keys in the dimension version of
/// their own snapshot, so <c>dimension_consumers_resolve</c> reports each key a version drops that an object of its snapshot names,
/// and a member that such an object names cannot be deleted. The objects are the unknown members of dataset bindings, mapping
/// entries, bridge mappings, and the groupings of enabled reports' calculation dimensions on the dimension's roles; objects of
/// other snapshots never block. A dimension whose role a calculation dimension reads cannot be deleted at all.
/// </summary>
public sealed class DimensionConsumersTests : IAsyncLifetime
{
    /// <summary>A version in the frozen snapshot that lacks BR02 and BR04.</summary>
    private const long FrozenVersionId = 103;

    /// <summary>The tables, with the live objects seeded by <see cref="InitializeAsync"/>.</summary>
    private readonly DimensionFixture _fixture = new();

    /// <summary>
    /// A live version that lacks BR02, which the live bridge maps, and BR04, which an enabled report's calculation dimension groups
    /// under, are reported for each; the keys the binding and the mapping entry name are kept, and BR09, which only the frozen
    /// snapshot's mapping version names, does not count.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionDroppingKeysObjectsName_IsReported()
    {
        var snapshot = await _fixture.Loader().LoadLiveAsync(DimensionFixture.DimensionId, TestContext.Current.CancellationToken);

        var violations = await new DimensionConsumersResolveRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal("dimension_consumers_resolve", violation.Code));
        Assert.Equal(
            ["Bridge 'zz_map_bridge_70' names member 'BR02', which the dimension's version lacks.",
             "Calculation dimension 'branch' of report 'balance_sheet' names member 'BR04', which the dimension's version lacks."],
            violations.Select(violation => violation.Message).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// A member of the live version that a live mapping entry names cannot be deleted; the same key in the frozen version can,
    /// because the frozen snapshot's mapping version does not name it.
    /// </summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task MemberAnObjectNames_IsBlockedOnlyInItsSnapshot()
    {
        var loader = _fixture.Loader();
        var live = await loader.LoadVersionAsync(DimensionFixture.LiveVersionId, TestContext.Current.CancellationToken);
        var frozen = await loader.LoadVersionAsync(FrozenVersionId, TestContext.Current.CancellationToken);

        var liveCheck = DimensionDeleteRules.Member(live, live.Members.Single(member => member.MemberKey == "BR01"));
        var frozenCheck = DimensionDeleteRules.Member(frozen, frozen.Members.Single(member => member.MemberKey == "BR01"));

        Assert.Equal("Member 'BR01' cannot be deleted: it is named by Source key '06' of mapping set 'zz_map_codes_60'.", liveCheck.Message);
        Assert.Equal([DeleteBlockerCodes.NamedByConsumers], liveCheck.Reasons.Select(reason => reason.Code));
        Assert.True(frozenCheck.Allowed);
    }

    /// <summary>
    /// The dimension's role is read by a calculation dimension, and a frozen snapshot holds the dimension, so it cannot be deleted.
    /// </summary>
    /// <returns>A task that completes when the delete check is checked.</returns>
    [Fact]
    public async Task DimensionACalculationDimensionReads_CannotBeDeleted()
    {
        var snapshot = await _fixture.Loader().LoadLiveAsync(DimensionFixture.DimensionId, TestContext.Current.CancellationToken);

        var check = DimensionDeleteRules.Dimension(snapshot);

        Assert.Equal(
            new[]
            {
                DeleteBlockerCodes.ReferencedByBridge, DeleteBlockerCodes.ReferencedByCalculationDimension, DeleteBlockerCodes.ReferencedByDatasetBinding,
                DeleteBlockerCodes.ReferencedByMappingSet, DeleteBlockerCodes.ReferencedBySnapshot
            },
            check.Reasons.Select(reason => reason.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The live objects: a binding of the role that routes unmapped rows to UNKNOWN, a mapping version mapping 06 to BR01 next to
    /// a frozen one mapping 99 to BR09, a bridge from BR02, and the enabled balance sheet, whose calculation dimension groups
    /// branches under BR04.
    /// </summary>
    /// <returns>A task that completes when the rows are seeded.</returns>
    public async ValueTask InitializeAsync()
    {
        var fixture = _fixture;
        var id = DimensionFixture.DimensionId;
        var live = DimensionFixture.LiveVersionId;
        await fixture.Versions.InsertAsync(DimensionRows.Version(FrozenVersionId, id, SnapshotRows.FrozenId), TestContext.Current.CancellationToken);
        await fixture.Members.InsertManyAsync(
            [
                DimensionRows.Member(11, live, "UNKNOWN"),
                DimensionRows.Member(12, live, "BR01"),
                DimensionRows.Member(14, live, "BR03"),
                DimensionRows.Member(21, FrozenVersionId, "UNKNOWN"),
                DimensionRows.Member(22, FrozenVersionId, "BR01"),
                DimensionRows.Member(24, FrozenVersionId, "BR03")
            ],
            TestContext.Current.CancellationToken);
        await fixture.Datasets.InsertAsync(CatalogRows.Dataset(1, "loans"), TestContext.Current.CancellationToken);
        await fixture.DatasetVersions.InsertAsync(DatasetRows.Version(11) with { DatasetId = 1 }, TestContext.Current.CancellationToken);
        await fixture.Bindings.InsertAsync(
            CatalogRows.Binding(31, 11, DimensionFixture.RoleId, id, BindingKinds.Direct) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberKey = "UNKNOWN" },
            TestContext.Current.CancellationToken);
        await fixture.MappingSets.InsertAsync(MappingRows.MappingSet(60, id), TestContext.Current.CancellationToken);
        await fixture.MappingVersions.InsertManyAsync(
            [MappingRows.MappingVersion(61, 60), MappingRows.MappingVersion(62, 60, SnapshotRows.FrozenId)],
            TestContext.Current.CancellationToken);
        await fixture.MappingEntries.InsertManyAsync([MappingRows.Entry(1, 61, "06", "BR01"), MappingRows.Entry(2, 62, "99", "BR09")], TestContext.Current.CancellationToken);
        await fixture.Bridges.InsertAsync(BridgeRows.Bridge(70, id, 600), TestContext.Current.CancellationToken);
        await fixture.BridgeVersions.InsertAsync(BridgeRows.BridgeVersion(71, 70, Cardinalities.ManyToOne, AllocationModes.None), TestContext.Current.CancellationToken);
        await fixture.BridgeMappings.InsertAsync(BridgeRows.Mapping(71, "BR02", "EAST", 1m), TestContext.Current.CancellationToken);
        await fixture.Reports.InsertAsync(ReportRows.Report(), TestContext.Current.CancellationToken);
        await fixture.ReportVersions.InsertAsync(ReportRows.Version(), TestContext.Current.CancellationToken);
        await fixture.CalculationDimensions.InsertAsync(
            ReportRows.CalculationDimension(81, "branch", DimensionFixture.RoleId) with { GroupingMode = GroupingModes.Ancestor, GroupingAncestorMemberKey = "BR04" },
            TestContext.Current.CancellationToken);
    }

    /// <summary>Nothing to release.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
