using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that cloning a dataset version copies its settings and every child row into a new draft whose internal
/// references point at the copies: grain fields, binding source fields and source bindings, time binding interval ends,
/// the default time binding, measure components, dependencies and time policies. References to other aggregates keep
/// their IDs. A clone that still pointed at source rows would let edits to the draft change a published version, and one
/// that remapped outside IDs would bind to dimension content that does not exist.
/// </summary>
public sealed class DatasetVersionClonerTests
{
    /// <summary>The published version being cloned, version 3 of dataset 10.</summary>
    private const long SourceVersionId = 100;

    /// <summary>The first ID the clone hands out; every seeded ID is smaller.</summary>
    private const long FirstNewId = 1_000_000;

    /// <summary>The mapping set the mapped binding maps through.</summary>
    private const long MappingSetId = 7002;

    /// <summary>The bridge the bridge binding remaps through.</summary>
    private const long BridgeId = 7005;

    /// <summary>The role the mapped binding binds.</summary>
    private const long BranchRoleId = 7003;

    /// <summary>The role the bridge binding binds.</summary>
    private const long RegionRoleId = 7004;

    /// <summary>The member the mapped binding routes unmapped rows to.</summary>
    private const string UnknownMemberKey = "UNKNOWN";

    /// <summary>The dataset version table, holding the source version.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _versions = new(DatasetRows.Version(SourceVersionId, defaultTimeBindingId: 21) with
    {
        VersionNumber = 3,
        Status = PublicationStatuses.Published,
        Properties = """{"owner":"finance"}""",
        PublishedAt = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc)
    });

    /// <summary>The source version's fields.</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(
        DatasetRows.ColumnField(1, SourceVersionId, "loanId", "loan_id"),
        DatasetRows.ColumnField(2, SourceVersionId, "asOfDate", "as_of_date", DataTypes.Date),
        DatasetRows.ColumnField(3, SourceVersionId, "endingBalance", "ending_balance", DataTypes.Decimal) with { NumericPrecision = 20, NumericScale = 2 },
        DatasetRows.ColumnField(4, SourceVersionId, "periodEnd", "period_end", DataTypes.Date),
        DatasetRows.ColumnField(5, SourceVersionId, "interestRate", "interest_rate", DataTypes.Decimal) with { NumericPrecision = 9, NumericScale = 6 });

    /// <summary>The source version's grains.</summary>
    private readonly InMemoryTableGateway<DatasetGrainRecord> _grains = new(
        DatasetRows.Grain(11, SourceVersionId, "rowIdentity", GrainKinds.Row),
        DatasetRows.Grain(12, SourceVersionId, "temporalSeries", GrainKinds.Series));

    /// <summary>The grains' fields: rowIdentity is (loanId, asOfDate), temporalSeries is (loanId).</summary>
    private readonly InMemoryTableGateway<DatasetGrainFieldRecord> _grainFields = new(
        DatasetRows.GrainField(11, 1, 1),
        DatasetRows.GrainField(11, 2, 2),
        DatasetRows.GrainField(12, 1, 1));

    /// <summary>A time binding, a mapped binding with outside references, and a bridge binding over the mapped one.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(21, SourceVersionId, null, BindingKinds.Time, 2),
        DatasetRows.Binding(22, SourceVersionId, BranchRoleId, BindingKinds.Mapped, 1) with
        {
            MappingSetId = MappingSetId,
            UnmappedBehavior = UnmappedBehaviors.Unknown,
            UnknownMemberKey = UnknownMemberKey
        },
        DatasetRows.Binding(23, SourceVersionId, RegionRoleId, BindingKinds.Bridge, null) with { SourceDimensionBindingId = 22, BridgeId = BridgeId });

    /// <summary>The time extension of the time binding, whose intervals end at periodEnd.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(DatasetRows.TimeBinding(21, intervalEndFieldId: 4));

    /// <summary>An aggregate, a weighted average and a formula over both.</summary>
    private readonly InMemoryTableGateway<MeasureRecord> _measures = new(
        DatasetRows.Measure(31, SourceVersionId, "endingBalance", MeasureKinds.Aggregate) with { AggregationFunction = AggregationFunctions.Sum },
        DatasetRows.Measure(32, SourceVersionId, "averageRate", MeasureKinds.WeightedAverage),
        DatasetRows.Measure(33, SourceVersionId, "rateRatio", MeasureKinds.Formula) with { ExpressionText = "[m.averageRate] / [m.endingBalance]" });

    /// <summary>The measures' components.</summary>
    private readonly InMemoryTableGateway<MeasureComponentRecord> _components = new(
        new MeasureComponentRecord { MeasureId = 31, ComponentRole = ComponentRoles.Value, FieldId = 3 },
        new MeasureComponentRecord { MeasureId = 32, ComponentRole = ComponentRoles.Value, FieldId = 5 },
        new MeasureComponentRecord { MeasureId = 32, ComponentRole = ComponentRoles.Weight, FieldId = 3 });

    /// <summary>The formula's dependencies.</summary>
    private readonly InMemoryTableGateway<MeasureDependencyRecord> _dependencies = new(
        new MeasureDependencyRecord { MeasureId = 33, DependencyMeasureId = 31 },
        new MeasureDependencyRecord { MeasureId = 33, DependencyMeasureId = 32 });

    /// <summary>The aggregate's time policy, on the time binding and the temporalSeries grain.</summary>
    private readonly InMemoryTableGateway<MeasureTimePolicyRecord> _timePolicies = new(DatasetRows.TimePolicy(31, requiredTimeBindingId: 21, selectionDatasetGrainId: 12));

    /// <summary>The clone is a new draft with the given number and no publication time; the source version and its rows are untouched.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_IsANewDraftAndLeavesTheSourceUntouched()
    {
        var clone = await CloneAsync();

        Assert.True(clone.DatasetVersionId >= FirstNewId);
        Assert.Equal(10, clone.DatasetId);
        Assert.Equal(4, clone.VersionNumber);
        Assert.Equal(PublicationStatuses.Draft, clone.Status);
        Assert.Null(clone.PublishedAt);
        Assert.Equal("loan_daily", clone.SourceObject);
        Assert.Equal("""{"owner":"finance"}""", clone.Properties);
        Assert.Equal(clone, _versions.All.Single(row => row.DatasetVersionId == clone.DatasetVersionId));
        Assert.Equal(PublicationStatuses.Published, _versions.All.Single(row => row.DatasetVersionId == SourceVersionId).Status);
        Assert.Equal(5, _fields.All.Count(row => row.DatasetVersionId == SourceVersionId));
        Assert.Equal(5, _fields.All.Count(row => row.DatasetVersionId == clone.DatasetVersionId));
        Assert.Equal(6, _grainFields.All.Count);
        Assert.Equal(2, _timeBindings.All.Count);
        Assert.Equal(6, _components.All.Count);
        Assert.Equal(4, _dependencies.All.Count);
        Assert.Equal(2, _timePolicies.All.Count);
    }

    /// <summary>Every reference between rows of the version, and the default time binding, points at the copied rows.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_RemapsEveryInternalReference()
    {
        var clone = await CloneAsync();
        var fieldKeys = _fields.All.Where(row => row.DatasetVersionId == clone.DatasetVersionId).ToDictionary(row => row.FieldId, row => row.FieldKey);
        var grainKeys = _grains.All.Where(row => row.DatasetVersionId == clone.DatasetVersionId).ToDictionary(row => row.DatasetGrainId, row => row.GrainKey);
        var bindings = _bindings.All.Where(row => row.DatasetVersionId == clone.DatasetVersionId).ToDictionary(row => row.BindingKind);
        var bindingKinds = bindings.Values.ToDictionary(row => row.DimensionBindingId, row => row.BindingKind);
        var measureKeys = _measures.All.Where(row => row.DatasetVersionId == clone.DatasetVersionId).ToDictionary(row => row.MeasureId, row => row.MeasureKey);

        Assert.All(fieldKeys.Keys.Concat(grainKeys.Keys).Concat(bindingKinds.Keys).Concat(measureKeys.Keys), id => Assert.True(id >= FirstNewId));
        Assert.Equal(BindingKinds.Time, bindingKinds[clone.DefaultTimeBindingId!.Value]);
        Assert.Equal(
            [("rowIdentity", 1, "loanId"), ("rowIdentity", 2, "asOfDate"), ("temporalSeries", 1, "loanId")],
            _grainFields.All
                .Where(row => grainKeys.ContainsKey(row.DatasetGrainId))
                .Select(row => (grainKeys[row.DatasetGrainId], row.Ordinal, fieldKeys[row.FieldId]))
                .Order());
        Assert.Equal("asOfDate", fieldKeys[bindings[BindingKinds.Time].SourceFieldId!.Value]);
        Assert.Equal("loanId", fieldKeys[bindings[BindingKinds.Mapped].SourceFieldId!.Value]);
        Assert.Equal(BindingKinds.Mapped, bindingKinds[bindings[BindingKinds.Bridge].SourceDimensionBindingId!.Value]);
        var timeBinding = Assert.Single(_timeBindings.All, row => bindingKinds.ContainsKey(row.DimensionBindingId));
        Assert.Equal(BindingKinds.Time, bindingKinds[timeBinding.DimensionBindingId]);
        Assert.Equal("periodEnd", fieldKeys[timeBinding.IntervalEndFieldId!.Value]);
        Assert.Equal(
            [("averageRate", ComponentRoles.Value, "interestRate"), ("averageRate", ComponentRoles.Weight, "endingBalance"), ("endingBalance", ComponentRoles.Value, "endingBalance")],
            _components.All
                .Where(row => measureKeys.ContainsKey(row.MeasureId))
                .Select(row => (measureKeys[row.MeasureId], row.ComponentRole, fieldKeys[row.FieldId]))
                .Order());
        Assert.Equal(
            [("rateRatio", "averageRate"), ("rateRatio", "endingBalance")],
            _dependencies.All
                .Where(row => measureKeys.ContainsKey(row.MeasureId))
                .Select(row => (measureKeys[row.MeasureId], measureKeys[row.DependencyMeasureId]))
                .Order());
        var policy = Assert.Single(_timePolicies.All, row => measureKeys.ContainsKey(row.MeasureId));
        Assert.Equal("endingBalance", measureKeys[policy.MeasureId]);
        Assert.Equal(BindingKinds.Time, bindingKinds[policy.RequiredTimeBindingId!.Value]);
        Assert.Equal("temporalSeries", grainKeys[policy.SelectionDatasetGrainId!.Value]);
    }

    /// <summary>References to the roles, the dimension, the mapping set and the bridge keep their IDs, and member keys stay as they are.</summary>
    /// <returns>A task that completes when the clone is checked.</returns>
    [Fact]
    public async Task Clone_KeepsReferencesToOtherAggregates()
    {
        var clone = await CloneAsync();
        var bindings = _bindings.All.Where(row => row.DatasetVersionId == clone.DatasetVersionId).ToDictionary(row => row.BindingKind);

        Assert.All(bindings.Values, binding => Assert.Equal(7000, binding.DimensionId));
        Assert.Null(bindings[BindingKinds.Time].DimensionRoleId);
        Assert.Equal(BranchRoleId, bindings[BindingKinds.Mapped].DimensionRoleId);
        Assert.Equal(MappingSetId, bindings[BindingKinds.Mapped].MappingSetId);
        Assert.Equal(UnknownMemberKey, bindings[BindingKinds.Mapped].UnknownMemberKey);
        Assert.Equal(RegionRoleId, bindings[BindingKinds.Bridge].DimensionRoleId);
        Assert.Equal(BridgeId, bindings[BindingKinds.Bridge].BridgeId);
    }

    /// <summary>Clones the source version into version 4 of dataset 10 with IDs counting up from <see cref="FirstNewId"/>.</summary>
    private Task<DatasetVersionRecord> CloneAsync()
    {
        var content = new DatasetVersionContentStore(
            _fields, _grains, _grainFields, _bindings, _timeBindings, _measures, _components, _dependencies, _timePolicies);
        var cloner = new DatasetVersionCloner(_versions, content, new SequentialIdGenerator(FirstNewId), TimeProvider.System);
        return cloner.CloneAsync(SourceVersionId, 10, 4, TestContext.Current.CancellationToken);
    }
}
