using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms that <c>unknown_members_in_version</c> resolves a binding's unknown member key in the published version of its
/// dimension, which the active assumption set pins, and ignores retired versions and drafts. A binding with the key whose
/// dimension has no published version is reported too. The engine resolves the key in that version, so a key it lacks routes
/// unmapped rows nowhere.
/// </summary>
public sealed class UnknownMembersInVersionRuleTests
{
    /// <summary>The dataset version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>The bound dimension.</summary>
    private const long DimensionId = 7000;

    /// <summary>v1, retired.</summary>
    private const long V1 = 701;

    /// <summary>v2, published.</summary>
    private const long V2 = 702;

    /// <summary>v3, a draft.</summary>
    private const long V3 = 703;

    /// <summary>The members: UNKNOWN in v2, LEGACY in v1 and PENDING in v3 only.</summary>
    private readonly InMemoryTableGateway<DimensionMemberRecord> _members = new(
        DimensionRows.Member(7013, V1, "LEGACY"),
        DimensionRows.Member(7022, V2, "UNKNOWN"),
        DimensionRows.Member(7033, V3, "PENDING"));

    /// <summary>The bound role, whose key rule messages name the binding by.</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(DimensionRows.Role(81, "region", DimensionId));

    /// <summary>An unknown member the published version has passes.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeyInThePublishedVersion_Passes()
    {
        Assert.Empty(await EvaluateAsync(Binding("UNKNOWN"), Published()));
    }

    /// <summary>An unknown member that only a retired version or a draft has is reported against the published version.</summary>
    /// <param name="memberKey">The member the binding routes unmapped rows to.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData("LEGACY")]
    [InlineData("PENDING")]
    public async Task KeyOutsideThePublishedVersion_IsReported(string memberKey)
    {
        var violation = Assert.Single(await EvaluateAsync(Binding(memberKey), Published()));

        Assert.Equal(UnknownMembersInVersionRule.Code, violation.Rule);
        Assert.Equal(
            $"Binding 'region' routes unmapped rows to member '{memberKey}', which is not a member of v2, the published version of its dimension.",
            violation.Message);
    }

    /// <summary>A binding with an unknown member whose dimension has no published version is reported once.</summary>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Fact]
    public async Task KeyWithoutAPublishedVersion_IsReported()
    {
        var unpublished = new InMemoryTableGateway<DimensionVersionRecord>(DimensionRows.Version(V3, DimensionId, 3, PublicationStatuses.Draft));

        var violation = Assert.Single(await EvaluateAsync(Binding("UNKNOWN"), unpublished));

        Assert.Equal("Binding 'region' routes unmapped rows to a member of its dimension, which has no published version.", violation.Message);
    }

    /// <summary>The dimension's versions: v1 retired, v2 published and v3 a draft.</summary>
    private static InMemoryTableGateway<DimensionVersionRecord> Published()
    {
        return new InMemoryTableGateway<DimensionVersionRecord>(
            DimensionRows.Version(V1, DimensionId, 1, PublicationStatuses.Retired),
            DimensionRows.Version(V2, DimensionId, 2, PublicationStatuses.Published),
            DimensionRows.Version(V3, DimensionId, 3, PublicationStatuses.Draft));
    }

    /// <summary>Evaluates the rule on a version holding one binding, over the given dimension versions.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(DimensionBindingRecord binding, InMemoryTableGateway<DimensionVersionRecord> versions)
    {
        var rule = new UnknownMembersInVersionRule(
            new InMemoryTableGateway<DimensionBindingRecord>(binding),
            new PublishedDimensionReader(versions, new InMemoryTableGateway<DimensionLevelRecord>(), _members, new PublicationSet()),
            new BindingNames(_roles, new InMemoryTableGateway<FieldRecord>()));
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }

    /// <summary>A mapped binding of the region role that routes unmapped rows to a member.</summary>
    private static DimensionBindingRecord Binding(string unknownMemberKey)
    {
        return DatasetRows.Binding(21, VersionId, 81, BindingKinds.Mapped, 1) with
        {
            MappingSetId = 30,
            UnmappedBehavior = UnmappedBehaviors.Unknown,
            UnknownMemberKey = unknownMemberKey
        };
    }
}
