using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.CalculationModels;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms what keeps the keys that live objects name in a dimension: the published version of the dimension is what they
/// resolve their keys in, so <c>dimension_consumers_resolve</c> reports each key a new version would drop, and a member of the
/// published version that a live object names cannot be deleted. Live objects are the bindings of published dataset versions,
/// the entries of published mapping versions, the mappings of published bridge versions, the lines of published report
/// versions and the variable targets of their models' published versions; drafts resolve nothing, so they never block.
/// </summary>
public sealed class DimensionConsumersTests
{
    /// <summary>The branch dimension.</summary>
    private const long BranchId = 500;

    /// <summary>The published version of the branch dimension.</summary>
    private const long PublishedVersionId = 501;

    /// <summary>The draft version about to be published.</summary>
    private const long DraftVersionId = 502;

    /// <summary>The published v1 and the draft v2 of the branch dimension.</summary>
    private readonly InMemoryTableGateway<DimensionVersionRecord> _versions = new(
        DimensionRows.Version(PublishedVersionId, BranchId, 1, PublicationStatuses.Published),
        DimensionRows.Version(DraftVersionId, BranchId, 2, PublicationStatuses.Draft));

    /// <summary>The region level in both versions.</summary>
    private readonly InMemoryTableGateway<DimensionLevelRecord> _levels = new(
        DimensionRows.Level(1, PublishedVersionId, "region", 1),
        DimensionRows.Level(2, DraftVersionId, "region", 1));

    /// <summary>v1 holds every named member; the draft v2 drops BR02 and BR04.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(11, PublishedVersionId, "UNKNOWN"),
        DimensionRows.Member(12, PublishedVersionId, "BR01"),
        DimensionRows.Member(13, PublishedVersionId, "BR02"),
        DimensionRows.Member(14, PublishedVersionId, "BR03"),
        DimensionRows.Member(15, PublishedVersionId, "BR04"),
        DimensionRows.Member(21, DraftVersionId, "UNKNOWN"),
        DimensionRows.Member(22, DraftVersionId, "BR01"),
        DimensionRows.Member(24, DraftVersionId, "BR03"));

    /// <summary>
    /// A draft that drops BR02, which a published bridge maps, and BR04, which a published model's variable targets, is
    /// reported for both; the keys the binding, the mapping entry and the report line name are kept, and BR09, which only a
    /// draft mapping version names, does not count.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionDroppingKeysLiveObjectsName_IsReported()
    {
        var rule = new DimensionConsumersResolveRule(_versions, Consumers(), new PublishedDimensionReader(_versions, _levels, _members));

        var violations = await rule.EvaluateAsync(DraftVersionId, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal(DimensionConsumersResolveRule.Code, violation.Rule));
        Assert.Equal(
            ["Bridge 'zz_map_bridge_70' names member 'BR02', which v2 lacks.",
             "Variable 'balance' of model 'margin_model' names member 'BR04', which v2 lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A member of the published version that a live mapping entry names cannot be deleted; the same key in the draft can.</summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task MemberALiveObjectNames_IsBlockedOnlyInThePublishedVersion()
    {
        var checks = new DimensionDeleteChecks(
            _versions,
            _members,
            new InMemoryTableGateway<RangeBandRecord>(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            Consumers());
        var cancellationToken = TestContext.Current.CancellationToken;

        var published = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 12), cancellationToken);
        var draft = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 22), cancellationToken);

        Assert.Equal("Member 'BR01' cannot be deleted: named by Source key '06' of mapping set 'zz_map_codes_60'.", published);
        Assert.Null(draft);
    }

    /// <summary>
    /// The live objects: a binding of a published dataset version that groups at region and routes unmapped rows to UNKNOWN, a
    /// published mapping version mapping 06 to BR01 next to a draft mapping 99 to BR09, a published bridge from BR02, a
    /// published report line showing BR03, and the published model's variable targeting BR04.
    /// </summary>
    private DimensionConsumers Consumers()
    {
        return new DimensionConsumers(
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "loans")),
            new InMemoryTableGateway<DatasetVersionRecord>(ReleaseRows.DatasetVersion(11, 1, 1, PublicationStatuses.Published)),
            new InMemoryTableGateway<DimensionBindingRecord>(ReleaseRows.Binding(31, 11, "Branch", BranchId, BindingKinds.Direct) with
            {
                GroupingMode = GroupingModes.Level,
                GroupingLevelKey = "region",
                UnmappedBehavior = UnmappedBehaviors.Unknown,
                UnknownMemberKey = "UNKNOWN"
            }),
            new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(60, BranchId)),
            new InMemoryTableGateway<MappingVersionRecord>(
                MappingRows.MappingVersion(61, 60, 1, PublicationStatuses.Published),
                MappingRows.MappingVersion(62, 60, 2, PublicationStatuses.Draft)),
            new InMemoryTableGateway<DimensionMappingRecord>(MappingRows.Entry(1, 61, "06", "BR01"), MappingRows.Entry(2, 62, "99", "BR09")),
            new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(70, BranchId, 600)),
            new InMemoryTableGateway<BridgeVersionRecord>(BridgeRows.BridgeVersion(71, 70, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published)),
            new InMemoryTableGateway<BridgeMappingRecord>(BridgeRows.Mapping(71, "BR02", "EAST", 1m)),
            new InMemoryTableGateway<ReportRecord>(new ReportRecord { ReportId = 60, ReportKey = "balance_sheet", DisplayName = "Balance sheet", ReportType = "financial", IsEnabled = true }),
            new InMemoryTableGateway<ReportVersionRecord>(ReportRows.ReportVersion(ReportRows.ReportVersionId, PublicationStatuses.Published) with { ReportDimensionId = BranchId }),
            new InMemoryTableGateway<ReportLineRecord>(ReportRows.Line(1, "br03", ReportLineKinds.Member, null, 1) with { ReportMemberKey = "BR03" }),
            new InMemoryTableGateway<CalculationModelRecord>(new CalculationModelRecord { ModelId = ModelRows.ModelId, ModelKey = "margin_model", DisplayName = "Margin", IsEnabled = true }),
            new InMemoryTableGateway<CalculationModelVersionRecord>(ModelRows.ModelVersion()),
            new InMemoryTableGateway<VariableRecord>(ModelRows.Variable(51, "balance", "BR04")));
    }
}
