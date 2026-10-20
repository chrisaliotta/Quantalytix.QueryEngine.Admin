using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Dimensions;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms the binding writes that change more than the binding row: a delete refuses a binding that is required, that a
/// bridge binding remaps, that a measure's time policy requires or that is the version's default time binding, naming every
/// reason by the binding's role or field key and deleting nothing; saving a binding that stops being a time binding deletes
/// its time extension, so no extension outlives its time binding; and a role binding takes its role's template as its
/// dimension, which the rules that compare dimensions read.
/// </summary>
public sealed class BindingServiceTests
{
    /// <summary>The draft version holding the bindings.</summary>
    private const long VersionId = 100;

    /// <summary>The time binding under test.</summary>
    private const long TimeBindingId = 21;

    /// <summary>The version's bindings: the asOf time binding, and a bridge binding of the fiscalPeriod role that remaps it.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(TimeBindingId, VersionId, null, BindingKinds.Time, 2),
        DatasetRows.Binding(22, VersionId, 82, BindingKinds.Bridge, null) with { SourceBindingId = TimeBindingId, BridgeId = 40 });

    /// <summary>The time binding's field, which names it.</summary>
    private readonly InMemoryTableGateway<FieldRecord> _fields = new(DatasetRows.ColumnField(2, VersionId, "asOf", "as_of", DataTypes.Date));

    /// <summary>The branch role of dimension 7100 and the fiscalPeriod role of dimension 7000.</summary>
    private readonly InMemoryTableGateway<DimensionRoleRecord> _roles = new(DimensionRows.Role(81, "branch", 7100), DimensionRows.Role(82, "fiscalPeriod", 7000));

    /// <summary>The time extension of the asOf binding.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(DatasetRows.TimeBinding(TimeBindingId));

    /// <summary>The draft version, whose default time binding is asOf.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _versions = new(DatasetRows.Version(VersionId, defaultTimeBindingId: TimeBindingId));

    /// <summary>A measure whose time policy requires the asOf binding.</summary>
    private readonly InMemoryTableGateway<MeasureRecord> _measures = new(DatasetRows.Measure(31, VersionId, "endingBalance", MeasureKinds.Aggregate));

    /// <summary>The time policy of endingBalance.</summary>
    private readonly InMemoryTableGateway<MeasureTimePolicyRecord> _timePolicies = new(DatasetRows.TimePolicy(31, TimeBindingId, 11));

    /// <summary>A binding with every blocker is kept, and the detail names each blocker.</summary>
    /// <returns>A task that completes when the delete is checked.</returns>
    [Fact]
    public async Task DeleteOfABindingInUse_IsRefusedNamingEveryReason()
    {
        await _bindings.UpdateAsync(_bindings.All[0] with { IsRequired = true }, TestContext.Current.CancellationToken);

        var detail = await Service().DeleteAsync(new BindingReference { VersionId = VersionId, BindingId = TimeBindingId }, TestContext.Current.CancellationToken);

        Assert.Equal(
            "Binding 'asOf' cannot be deleted: it is a required binding of the dataset; it is the source binding of bridge bindings 'fiscalPeriod'; " +
            "it is the required time binding of measures 'endingBalance'; it is the version's default time binding.",
            detail);
        Assert.Equal(2, _bindings.All.Count);
        Assert.Single(_timeBindings.All);
    }

    /// <summary>
    /// Saving the time binding as a direct binding of the branch role deletes its time extension and stores the role with its
    /// template, dimension 7100.
    /// </summary>
    /// <returns>A task that completes when the update is checked.</returns>
    [Fact]
    public async Task ChangingAwayFromTime_DeletesTheTimeExtensionAndTakesTheRolesDimension()
    {
        var input = new BindingInput
        {
            RoleId = 81,
            BindingKind = BindingKinds.Direct,
            SourceFieldId = 2,
            RaggedBehavior = RaggedBehaviors.Null,
            DefaultPeriodAlignment = PeriodAlignments.Observation,
            UnmappedBehavior = UnmappedBehaviors.Null,
            IsRequired = false,
            IsGroupable = true,
            IsFilterable = true,
            IsEnabled = true
        };

        var detail = await Service().UpdateAsync(new BindingReference { VersionId = VersionId, BindingId = TimeBindingId }, input, TestContext.Current.CancellationToken);

        Assert.Null(detail.TimeBinding);
        Assert.Equal("branch", detail.Role?.RoleKey);
        var stored = _bindings.All.Single(binding => binding.BindingId == TimeBindingId);
        Assert.Equal((BindingKinds.Direct, 81L, 7100L), (stored.BindingKind, stored.RoleId!.Value, stored.DimensionId));
        Assert.Empty(_timeBindings.All);
    }

    /// <summary>The service over the test tables.</summary>
    private BindingService Service()
    {
        return new BindingService(
            _bindings,
            _timeBindings,
            _roles,
            _versions,
            _timePolicies,
            _measures,
            new BindingNames(_roles, _fields),
            new SequentialIdGenerator(1000));
    }
}
