using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Lookups;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Lookups;

/// <summary>
/// Confirms the role pickers and the pickers a bridge source is authored with: <c>dimension-roles</c> lists every role
/// labeled with its template, or the roles of one dimension; <c>bound-roles</c> lists the roles a dataset version binds into
/// members with an enabled binding, each pointing at its template; and <c>bridges</c> narrowed to that template lists the
/// bridges from it. Together they offer only bridges a role's binding can feed.
/// </summary>
public sealed class BridgeSourceLookupTests
{
    /// <summary>
    /// GL version 11 binds glAccount into accounts by mapping, region through a bridge, product with a disabled binding, and its
    /// as-of date as a time binding.
    /// </summary>
    private readonly LookupService _lookups = Lookups(
        new InMemoryTableGateway<DimensionBindingRecord>(
            ReleaseRows.Binding(31, 11, 85, 800, BindingKinds.Mapped) with { SourceFieldId = 1 },
            ReleaseRows.Binding(32, 11, 86, 600, BindingKinds.Bridge) with { SourceBindingId = 31, BridgeId = 97 },
            ReleaseRows.Binding(33, 11, 87, 700, BindingKinds.Direct) with { SourceFieldId = 3, IsEnabled = false },
            ReleaseRows.Binding(34, 11, null, 900, BindingKinds.Time) with { SourceFieldId = 4 }),
        new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(95, 800, 500), BridgeRows.Bridge(96, 600, 500)));

    /// <summary>Only glAccount's binding is enabled and classifies members, so it is the one bound role, pointing at accounts.</summary>
    /// <returns>A task that completes when the lookup is checked.</returns>
    [Fact]
    public async Task BoundRoles_AreTheRolesWhoseEnabledBindingClassifiesMembers()
    {
        var items = await _lookups.ListAsync(new LookupQuery { Kind = LookupKinds.BoundRoles, ParentId = 11 }, TestContext.Current.CancellationToken);

        Assert.Equal([("glAccount", (long?)800, BindingKinds.Mapped)], items.Select(item => (item.Key, item.ParentId, item.Classifier)));
    }

    /// <summary>Every role is labeled with its template and classified by the template's kind; a parent narrows them to one dimension.</summary>
    /// <returns>A task that completes when the lookup is checked.</returns>
    [Fact]
    public async Task DimensionRoles_AreLabeledWithTheirTemplate()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        var all = await _lookups.ListAsync(new LookupQuery { Kind = LookupKinds.DimensionRoles }, cancellationToken);
        var accounts = await _lookups.ListAsync(new LookupQuery { Kind = LookupKinds.DimensionRoles, ParentId = 800 }, cancellationToken);

        Assert.Equal(["glAccount", "product", "region"], all.Select(item => item.Key));
        Assert.Equal([("GL account · Accounts", (long?)800, DimensionKinds.Categorical)], accounts.Select(item => (item.Label, item.ParentId, item.Classifier)));
    }

    /// <summary>Narrowed to accounts, the bridges are the one from accounts, pointing at its target; unnarrowed, both.</summary>
    /// <returns>A task that completes when the lookup is checked.</returns>
    [Fact]
    public async Task Bridges_NarrowToTheirSourceDimension()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(
            [(95L, (long?)500)],
            (await _lookups.ListAsync(new LookupQuery { Kind = LookupKinds.Bridges, ParentId = 800 }, cancellationToken)).Select(item => (item.Id, item.ParentId)));
        Assert.Equal(2, (await _lookups.ListAsync(new LookupQuery { Kind = LookupKinds.Bridges }, cancellationToken)).Count);
    }

    /// <summary>
    /// The lookup service over the given bindings and bridges, the accounts, regions and products dimensions with one role each,
    /// and every other table empty.
    /// </summary>
    private static LookupService Lookups(InMemoryTableGateway<DimensionBindingRecord> bindings, InMemoryTableGateway<BridgeRecord> bridges)
    {
        var fields = new InMemoryTableGateway<FieldRecord>();
        var roles = new InMemoryTableGateway<DimensionRoleRecord>(
            DimensionRows.Role(85, "glAccount", 800) with { DisplayName = "GL account" },
            DimensionRows.Role(86, "region", 600),
            DimensionRows.Role(87, "product", 700));
        return new LookupService(
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            fields,
            new InMemoryTableGateway<DatasetGrainRecord>(),
            bindings,
            new InMemoryTableGateway<TimeBindingRecord>(),
            new InMemoryTableGateway<MeasureRecord>(),
            new InMemoryTableGateway<DimensionRecord>(
                ReleaseRows.Dimension(800, "accounts") with { DisplayName = "Accounts" },
                ReleaseRows.Dimension(600, "regions"),
                ReleaseRows.Dimension(700, "products")),
            roles,
            new InMemoryTableGateway<DimensionVersionRecord>(),
            new InMemoryTableGateway<DimensionLevelRecord>(),
            new InMemoryTableGateway<DimensionMemberRecord>(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<MappingVersionRecord>(),
            bridges,
            new InMemoryTableGateway<BridgeVersionRecord>(),
            new InMemoryTableGateway<SemanticReleaseRecord>(),
            new InMemoryTableGateway<AssumptionSetRecord>(),
            new BindingNames(roles, fields),
            new CalculationLookups(
                new InMemoryTableGateway<CalculationModelRecord>(),
                new InMemoryTableGateway<CalculationModelVersionRecord>(),
                new InMemoryTableGateway<VariableRecord>(),
                new InMemoryTableGateway<ReportSourceRecord>(),
                new InMemoryTableGateway<SourceProfileRecord>(),
                new InMemoryTableGateway<CalculatedMeasureRecord>(),
                new InMemoryTableGateway<CalculationDimensionRecord>(),
                new InMemoryTableGateway<ReportRecord>(),
                new InMemoryTableGateway<ReportColumnRecord>()));
    }
}
