using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Reports;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Mappings;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Reports;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Dimensions;

/// <summary>
/// Confirms what keeps the keys that live objects name in a dimension: the published version of the dimension is what they
/// resolve their keys in, so <c>dimension_consumers_resolve</c> reports each key a new version would drop, and a member of the
/// published version that a live object names cannot be deleted. Live objects are the unknown members of bindings of published
/// dataset versions, the entries of published mapping versions, the mappings of published bridge versions, and the member
/// options, overlay row parents, variable scopes and the groupings of calculation dimensions on the dimension's roles of
/// published report versions; drafts resolve nothing, so they never block. A dimension a report names, or whose role a
/// calculation dimension reads, cannot be deleted at all.
/// </summary>
public sealed class DimensionConsumersTests
{
    /// <summary>The branch dimension.</summary>
    private const long BranchId = 500;

    /// <summary>The branch dimension's branch role.</summary>
    private const long BranchRoleId = 91;

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
    /// A draft that drops BR02, which a published bridge maps, and BR04, which an overlay row sits under and a calculation dimension
    /// groups under, is reported for each; the keys the binding, the mapping entry, the member option and the scope name are kept,
    /// the overlay row under another overlay row names no member, and BR09, which only a draft mapping version names, does not count.
    /// </summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task VersionDroppingKeysLiveObjectsName_IsReported()
    {
        var rule = new DimensionConsumersResolveRule(_versions, Consumers(), new PublishedDimensionReader(_versions, _levels, _members, new PublicationSet()));

        var violations = await rule.EvaluateAsync(DraftVersionId, TestContext.Current.CancellationToken);

        Assert.All(violations, violation => Assert.Equal(DimensionConsumersResolveRule.Code, violation.Rule));
        Assert.Equal(
            ["Bridge 'zz_map_bridge_70' names member 'BR02', which v2 lacks.",
             "Row 'notes' of report 'balance_sheet' names member 'BR04', which v2 lacks.",
             "Calculation dimension 'branch' of report 'balance_sheet' names member 'BR04', which v2 lacks."],
            violations.Select(violation => violation.Message));
    }

    /// <summary>A member of the published version that a live mapping entry and a report's scope name cannot be deleted; the same key in the draft can.</summary>
    /// <returns>A task that completes when the delete checks are checked.</returns>
    [Fact]
    public async Task MemberALiveObjectNames_IsBlockedOnlyInThePublishedVersion()
    {
        var checks = Checks();
        var cancellationToken = TestContext.Current.CancellationToken;

        var published = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 12), cancellationToken);
        var draft = await checks.MemberAsync(_members.All.Single(member => member.MemberId == 22), cancellationToken);

        Assert.Equal(
            "Member 'BR01' cannot be deleted: named by Scope of variable 'balance' of report 'balance_sheet', Source key '06' of mapping set 'zz_map_codes_60'.",
            published);
        Assert.Null(draft);
    }

    /// <summary>The branch dimension is the balance sheet's report dimension and its role is read by a calculation dimension, so it cannot be deleted.</summary>
    /// <returns>A task that completes when the delete check is checked.</returns>
    [Fact]
    public async Task DimensionAReportOrCalculationDimensionNames_CannotBeDeleted()
    {
        var detail = await Checks().DimensionAsync(ReleaseRows.Dimension(BranchId, "branch"), [], TestContext.Current.CancellationToken);

        Assert.Equal(
            "Dimension 'branch' cannot be deleted: the report dimension of reports 'balance_sheet'; read by calculation dimensions 'branch' of 'balance_sheet' v1.",
            detail);
    }

    /// <summary>The delete checks over the branch dimension's versions and the live objects.</summary>
    private DimensionDeleteChecks Checks()
    {
        return new DimensionDeleteChecks(
            _versions,
            _members,
            new InMemoryTableGateway<RangeBandRecord>(),
            Roles(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<BridgeRecord>(),
            new InMemoryTableGateway<DimensionBindingRecord>(),
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            new InMemoryTableGateway<ReportRecord>(ReportRows.Report()),
            CalculationDimensions(),
            new InMemoryTableGateway<VariableSourceRecord>(),
            new ReportVersionNames(new InMemoryTableGateway<ReportRecord>(ReportRows.Report()), ReportVersions()),
            Consumers());
    }

    /// <summary>The branch dimension's branch role.</summary>
    private static InMemoryTableGateway<DimensionRoleRecord> Roles()
    {
        return new InMemoryTableGateway<DimensionRoleRecord>(DimensionRows.Role(BranchRoleId, "branch", BranchId));
    }

    /// <summary>The published v1 of the balance sheet, whose report dimension is the branch dimension.</summary>
    private static InMemoryTableGateway<ReportVersionRecord> ReportVersions()
    {
        return new InMemoryTableGateway<ReportVersionRecord>(ReportRows.ReportVersion(ReportRows.ReportVersionId, PublicationStatuses.Published));
    }

    /// <summary>The balance sheet's calculation dimension on the branch role, which groups branches under BR04.</summary>
    private static InMemoryTableGateway<CalculationDimensionRecord> CalculationDimensions()
    {
        return new InMemoryTableGateway<CalculationDimensionRecord>(ReportRows.CalculationDimension(81, "branch", BranchRoleId) with
        {
            GroupingMode = GroupingModes.Ancestor,
            GroupingAncestorMemberKey = "BR04"
        });
    }

    /// <summary>
    /// The live objects: a binding of the branch role in a published dataset version that routes unmapped rows to UNKNOWN, a
    /// published mapping version mapping 06 to BR01 next to a draft mapping 99 to BR09, a published bridge from BR02, and the
    /// published balance sheet over the branch dimension, which makes BR03 a header, scopes a variable to BR01, places the notes
    /// row under BR04 and the detail row under notes, and groups its calculation dimension under BR04.
    /// </summary>
    private static DimensionConsumers Consumers()
    {
        var roles = Roles();
        return new DimensionConsumers(
            new InMemoryTableGateway<DatasetRecord>(ReleaseRows.Dataset(1, "loans")),
            new InMemoryTableGateway<DatasetVersionRecord>(ReleaseRows.DatasetVersion(11, 1, 1, PublicationStatuses.Published)),
            new InMemoryTableGateway<DimensionBindingRecord>(ReleaseRows.Binding(31, 11, BranchRoleId, BranchId, BindingKinds.Direct) with
            {
                UnmappedBehavior = UnmappedBehaviors.Unknown,
                UnknownMemberKey = "UNKNOWN"
            }),
            roles,
            new InMemoryTableGateway<MappingSetRecord>(MappingRows.MappingSet(60, BranchId)),
            new InMemoryTableGateway<MappingVersionRecord>(
                MappingRows.MappingVersion(61, 60, 1, PublicationStatuses.Published),
                MappingRows.MappingVersion(62, 60, 2, PublicationStatuses.Draft)),
            new InMemoryTableGateway<DimensionMappingRecord>(MappingRows.Entry(1, 61, "06", "BR01"), MappingRows.Entry(2, 62, "99", "BR09")),
            new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(70, BranchId, 600)),
            new InMemoryTableGateway<BridgeVersionRecord>(BridgeRows.BridgeVersion(71, 70, 1, Cardinalities.ManyToOne, AllocationModes.None, PublicationStatuses.Published)),
            new InMemoryTableGateway<BridgeMappingRecord>(BridgeRows.Mapping(71, "BR02", "EAST", 1m)),
            new InMemoryTableGateway<ReportRecord>(ReportRows.Report()),
            ReportVersions(),
            new InMemoryTableGateway<ReportMemberOptionRecord>(ReportRows.MemberOption("BR03")),
            new InMemoryTableGateway<ReportLineRecord>(
                ReportRows.Line(1, "notes", ReportLineKinds.Section, "BR04", 1),
                ReportRows.Line(2, "detail", ReportLineKinds.Section, "notes", 1)),
            new InMemoryTableGateway<ReportVariableScopeRecord>(ReportRows.Scope("balance", "BR01")),
            CalculationDimensions(),
            new BindingNames(roles, new InMemoryTableGateway<FieldRecord>()),
            new PublicationSet());
    }
}
