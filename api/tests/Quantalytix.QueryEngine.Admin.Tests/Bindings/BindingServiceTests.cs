using Quantalytix.QueryEngine.Admin.Application.Bindings;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Datasets;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Bindings;

/// <summary>
/// Confirms the two binding writes that change more than the binding row: a delete refuses a binding that is required, that
/// a bridge binding remaps, that a measure's time policy requires or that is the version's default time binding, naming
/// every reason and deleting nothing; and saving a binding that stops being a time binding deletes its time extension, so
/// no extension outlives its time binding.
/// </summary>
public sealed class BindingServiceTests
{
    /// <summary>The draft version holding the bindings.</summary>
    private const long VersionId = 100;

    /// <summary>The time binding under test.</summary>
    private const long TimeBindingId = 21;

    /// <summary>The version's bindings: the As of time binding, and a bridge binding that remaps it.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(TimeBindingId, VersionId, "As of", BindingKinds.Time, 2),
        DatasetRows.Binding(22, VersionId, "Fiscal period", BindingKinds.Bridge, null) with { SourceBindingId = TimeBindingId, BridgeVersionId = 41 });

    /// <summary>The time extension of the As of binding.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(DatasetRows.TimeBinding(TimeBindingId));

    /// <summary>The draft version, whose default time binding is As of.</summary>
    private readonly InMemoryTableGateway<DatasetVersionRecord> _versions = new(DatasetRows.Version(VersionId, defaultTimeBindingId: TimeBindingId));

    /// <summary>A measure whose time policy requires the As of binding.</summary>
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
            "Binding 'As of' cannot be deleted: it is a required binding of the dataset; it is the source binding of bridge bindings 'Fiscal period'; " +
            "it is the required time binding of measures 'endingBalance'; it is the version's default time binding.",
            detail);
        Assert.Equal(2, _bindings.All.Count);
        Assert.Single(_timeBindings.All);
    }

    /// <summary>Saving the time binding as another kind deletes its time extension.</summary>
    /// <returns>A task that completes when the update is checked.</returns>
    [Fact]
    public async Task ChangingAwayFromTime_DeletesTheTimeExtension()
    {
        var input = new BindingInput
        {
            DisplayName = "As of",
            DimensionId = 7000,
            BindingKind = BindingKinds.Direct,
            SourceFieldId = 2,
            GroupingMode = GroupingModes.Member,
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
        Assert.Equal(BindingKinds.Direct, _bindings.All.Single(binding => binding.BindingId == TimeBindingId).BindingKind);
        Assert.Empty(_timeBindings.All);
    }

    /// <summary>The service over the test tables.</summary>
    private BindingService Service()
    {
        return new BindingService(
            _bindings,
            _timeBindings,
            new InMemoryTableGateway<BridgeVersionRecord>(),
            _versions,
            _timePolicies,
            _measures,
            new SequentialIdGenerator(1000));
    }
}
