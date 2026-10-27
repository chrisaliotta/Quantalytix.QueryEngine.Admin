using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms what keeps the keys that live objects name in a dimension (M10): live objects resolve their keys in the live version,
/// so <c>dimension_consumers_resolve</c> reports each key another version would drop, and a member of the live version that a live
/// object names cannot be deleted. Live objects are the unknown members of bindings of published dataset versions, the entries of
/// published mapping versions, the mappings of published bridge versions, and the groupings of enabled reports' calculation
/// dimensions on the dimension's roles; drafts resolve nothing, so they never block. A dimension whose role a calculation
/// dimension reads cannot be deleted at all.
/// </summary>
public sealed class DimensionConsumersTests : IAsyncLifetime
{
    /// <summary>A draft version 2 that drops BR02 and BR04.</summary>
    private const long DraftVersionId = 103;

    /// <summary>The tables, with the live objects seeded by <see cref="InitializeAsync"/>.</summary>
    private readonly DimensionFixture _fixture = new();

    /// <summary>
    /// A version that drops BR02, which a published bridge maps, and BR04, which an enabled report's calculation dimension groups
    /// under, is reported for each; the keys the binding and the mapping entry name are kept, and BR09, which only a draft mapping
    /// version names, does not count.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionDroppingKeysLiveObjectsName_IsReported()
    {
        var snapshot = await _fixture.Loader().LoadVersionAsync(DraftVersionId, TestContext.Current.CancellationToken);

        var violations = await new DimensionConsumersResolveRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal("dimension_consumers_resolve", violation.Code));
        Assert.Equal(
            ["Bridge 'zz_map_bridge_70' names member 'BR02', which v2 lacks.",
             "Calculation dimension 'branch' of report 'balance_sheet' names member 'BR04', which v2 lacks."],
            violations.Select(violation => violation.Message).Order(StringComparer.Ordinal));
    }

    /// <summary>A member of the live version that a live mapping entry names cannot be deleted; the same key in the draft can.</summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task MemberALiveObjectNames_IsBlockedOnlyInTheLiveVersion()
    {
        var loader = _fixture.Loader();
        var live = await loader.LoadVersionAsync(DimensionFixture.LiveVersionId, TestContext.Current.CancellationToken);
        var draft = await loader.LoadVersionAsync(DraftVersionId, TestContext.Current.CancellationToken);

        var livePublished = DimensionDeleteRules.Member(live, live.Members.Single(member => member.MemberKey == "BR01"));
        var drafted = DimensionDeleteRules.Member(draft, draft.Members.Single(member => member.MemberKey == "BR01"));

        Assert.Equal("Member 'BR01' cannot be deleted: it is named by Source key '06' of mapping set 'zz_map_codes_60'.", livePublished.Message);
        Assert.Equal([DeleteBlockerCodes.NamedByConsumers], livePublished.Reasons.Select(reason => reason.Code));
        Assert.True(drafted.Allowed);
    }

    /// <summary>
    /// The dimension's role is read by a calculation dimension, so the dimension cannot be deleted; the active assumption set's
    /// pin of the live version does not block, because it goes with the dimension.
    /// </summary>
    /// <returns>A task that completes when the delete check is checked.</returns>
    [Fact]
    public async Task DimensionACalculationDimensionReads_CannotBeDeleted()
    {
        var snapshot = await _fixture.Loader().LoadLiveAsync(DimensionFixture.DimensionId, TestContext.Current.CancellationToken);

        var check = DimensionDeleteRules.Dimension(snapshot);

        Assert.Equal(
            new[] { DeleteBlockerCodes.ReferencedByBridge, DeleteBlockerCodes.ReferencedByCalculationDimension, DeleteBlockerCodes.ReferencedByDatasetBinding, DeleteBlockerCodes.ReferencedByMappingSet },
            check.Reasons.Select(reason => reason.Code).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The live objects: a binding of the role in a published dataset version that routes unmapped rows to UNKNOWN, a published
    /// mapping version mapping 06 to BR01 next to a draft mapping 99 to BR09, a published bridge from BR02, the enabled balance
    /// sheet, whose calculation dimension groups branches under BR04, and the active assumption set's pin of the live version.
    /// </summary>
    /// <returns>A task that completes when the rows are seeded.</returns>
    public async ValueTask InitializeAsync()
    {
        var fixture = _fixture;
        var id = DimensionFixture.DimensionId;
        var live = DimensionFixture.LiveVersionId;
        await fixture.Versions.InsertAsync(DimensionRows.Version(DraftVersionId, id, 2, PublicationStatuses.Draft), TestContext.Current.CancellationToken);
        await fixture.Members.InsertManyAsync(
            [
                DimensionRows.Member(11, live, "UNKNOWN"),
                DimensionRows.Member(12, live, "BR01"),
                DimensionRows.Member(13, live, "BR02"),
                DimensionRows.Member(14, live, "BR03"),
                DimensionRows.Member(15, live, "BR04"),
                DimensionRows.Member(21, DraftVersionId, "UNKNOWN"),
                DimensionRows.Member(22, DraftVersionId, "BR01"),
                DimensionRows.Member(24, DraftVersionId, "BR03")
            ],
            TestContext.Current.CancellationToken);
        await fixture.Datasets.InsertAsync(ReleaseRows.Dataset(1, "loans"), TestContext.Current.CancellationToken);
        await fixture.DatasetVersions.InsertAsync(ReleaseRows.DatasetVersion(11, 1, 1, PublicationStatuses.Published), TestContext.Current.CancellationToken);
        await fixture.Bindings.InsertAsync(
            ReleaseRows.Binding(31, 11, DimensionFixture.RoleId, id, BindingKinds.Direct) with { UnmappedBehavior = UnmappedBehaviors.Unknown, UnknownMemberKey = "UNKNOWN" },
            TestContext.Current.CancellationToken);
        await fixture.MappingSets.InsertAsync(MappingRows.MappingSet(60, id), TestContext.Current.CancellationToken);
        await fixture.MappingVersions.InsertManyAsync(
            [MappingRows.MappingVersion(61, 60, 1, PublicationStatuses.Published), MappingRows.MappingVersion(62, 60, 2, PublicationStatuses.Draft)],
            TestContext.Current.CancellationToken);
        await fixture.MappingEntries.InsertManyAsync([MappingRows.Entry(1, 61, "06", "BR01"), MappingRows.Entry(2, 62, "99", "BR09")], TestContext.Current.CancellationToken);
        await fixture.Bridges.InsertAsync(BridgeRows.Bridge(70, id, 600), TestContext.Current.CancellationToken);
        await fixture.BridgeVersions.InsertAsync(BridgeRows.BridgeVersion(71, 70, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published), TestContext.Current.CancellationToken);
        await fixture.BridgeMappings.InsertAsync(BridgeRows.Mapping(71, "BR02", "EAST", 1m), TestContext.Current.CancellationToken);
        await fixture.Reports.InsertAsync(ReportRows.Report(), TestContext.Current.CancellationToken);
        await fixture.CalculationDimensions.InsertAsync(
            ReportRows.CalculationDimension(81, "branch", DimensionFixture.RoleId) with { GroupingMode = GroupingModes.Ancestor, GroupingAncestorMemberKey = "BR04" },
            TestContext.Current.CancellationToken);
        await fixture.Pins.InsertAsync(new AssumptionSetDimensionRecord { AssumptionSetId = DimensionFixture.ActiveSetId, DimensionId = id, DimensionVersionId = live }, TestContext.Current.CancellationToken);
    }

    /// <summary>Nothing to release.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
