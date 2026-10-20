using Quantalytix.QueryEngine.Admin.Application.Lookups;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Bridges;
using Quantalytix.QueryEngine.Admin.Tests.Releases;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;
using static Quantalytix.QueryEngine.Admin.Tests.Datasets.DatasetRows;

namespace Quantalytix.QueryEngine.Admin.Tests.Lookups;

/// <summary>
/// Confirms the pickers a bridge classification is authored with: <c>classified-fields</c> lists the fields of a dataset version whose
/// binding groups members of a canonical dimension, each pointing at that dimension, and <c>bridges</c> narrowed to that dimension lists
/// the bridges from it, each pointing at its target. Together they offer only bridges a field's binding can feed.
/// </summary>
public sealed class ClassificationLookupTests
{
    /// <summary>GL version 11: <c>account</c> groups accounts by member, <c>region</c> groups at a level and <c>memo</c> has no binding.</summary>
    private readonly LookupService _lookups = Lookups(
        new InMemoryTableGateway<FieldRecord>(ColumnField(1, 11, "account", "account"), ColumnField(2, 11, "region", "region"), ColumnField(3, 11, "memo", "memo")),
        new InMemoryTableGateway<DimensionBindingRecord>(
            ReleaseRows.Binding(31, 11, "Account", 800, BindingKinds.Mapped) with { SourceFieldId = 1 },
            ReleaseRows.Binding(32, 11, "Region", 600, BindingKinds.Direct) with { SourceFieldId = 2, GroupingMode = GroupingModes.Level, GroupingLevelKey = "region" }),
        new InMemoryTableGateway<BridgeRecord>(BridgeRows.Bridge(95, 800, 500), BridgeRows.Bridge(96, 600, 500)));

    /// <summary>Only <c>account</c> groups members, so it is the one classified field, pointing at accounts.</summary>
    /// <returns>A task that completes when the lookup is checked.</returns>
    [Fact]
    public async Task ClassifiedFields_AreTheFieldsWhoseBindingGroupsMembers()
    {
        var items = await _lookups.ListAsync(LookupKinds.ClassifiedFields, 11, TestContext.Current.CancellationToken);

        Assert.Equal([("account", (long?)800)], items.Select(item => (item.Key, item.ParentId)));
    }

    /// <summary>Narrowed to accounts, the bridges are the one from accounts, pointing at its target; unnarrowed, both.</summary>
    /// <returns>A task that completes when the lookup is checked.</returns>
    [Fact]
    public async Task Bridges_NarrowToTheirSourceDimension()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal([(95L, (long?)500)], (await _lookups.ListAsync(LookupKinds.Bridges, 800, cancellationToken)).Select(item => (item.Id, item.ParentId)));
        Assert.Equal(2, (await _lookups.ListAsync(LookupKinds.Bridges, null, cancellationToken)).Count);
    }

    /// <summary>The lookup service over the given fields, bindings and bridges, with every other table empty.</summary>
    private static LookupService Lookups(
        InMemoryTableGateway<FieldRecord> fields, InMemoryTableGateway<DimensionBindingRecord> bindings, InMemoryTableGateway<BridgeRecord> bridges)
    {
        return new LookupService(
            new InMemoryTableGateway<DatasetRecord>(),
            new InMemoryTableGateway<DatasetVersionRecord>(),
            fields,
            new InMemoryTableGateway<DatasetGrainRecord>(),
            bindings,
            new InMemoryTableGateway<TimeBindingRecord>(),
            new InMemoryTableGateway<MeasureRecord>(),
            new InMemoryTableGateway<DimensionRecord>(),
            new InMemoryTableGateway<DimensionVersionRecord>(),
            new InMemoryTableGateway<DimensionLevelRecord>(),
            new InMemoryTableGateway<DimensionMemberRecord>(),
            new InMemoryTableGateway<MappingSetRecord>(),
            new InMemoryTableGateway<MappingVersionRecord>(),
            bridges,
            new InMemoryTableGateway<BridgeVersionRecord>(),
            new InMemoryTableGateway<SemanticReleaseRecord>(),
            new InMemoryTableGateway<AssumptionSetRecord>(),
            new CalculationLookups(
                new InMemoryTableGateway<CalculationModelRecord>(),
                new InMemoryTableGateway<CalculationModelVersionRecord>(),
                new InMemoryTableGateway<VariableRecord>(),
                new InMemoryTableGateway<VariableSourceRecord>(),
                new InMemoryTableGateway<SourceProfileRecord>(),
                new InMemoryTableGateway<CalculatedMeasureRecord>(),
                new InMemoryTableGateway<CalculationDimensionRecord>(),
                new InMemoryTableGateway<ReportRecord>(),
                new InMemoryTableGateway<ReportVersionRecord>(),
                new InMemoryTableGateway<ReportColumnRecord>()));
    }
}
