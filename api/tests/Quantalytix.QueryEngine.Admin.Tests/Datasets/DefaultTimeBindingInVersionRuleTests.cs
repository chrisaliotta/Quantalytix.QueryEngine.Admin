using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>default_time_binding_in_version</c> accepts no default or a time binding of the version, and reports a default
/// that is not one, because the engine aggregates time on the default when a measure names none.
/// </summary>
public sealed class DefaultTimeBindingInVersionRuleTests
{
    /// <summary>A time binding of the version.</summary>
    private const long TimeBindingId = 21;

    /// <summary>A binding of the version without a time axis.</summary>
    private const long MappedBindingId = 22;

    /// <summary>A binding id the version does not have.</summary>
    private const long OtherBindingId = 31;

    /// <summary>No default time binding, or a time binding of the version, passes.</summary>
    /// <param name="defaultTimeBindingId">The version's default time binding.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData(TimeBindingId)]
    public async Task AbsentOrOwnTimeBinding_Passes(long? defaultTimeBindingId)
    {
        Assert.Empty(await EvaluateAsync(defaultTimeBindingId));
    }

    /// <summary>A binding without a time axis, or an id the version does not have, is reported on the settings tab.</summary>
    /// <param name="defaultTimeBindingId">The version's default time binding.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(MappedBindingId)]
    [InlineData(OtherBindingId)]
    public async Task BindingThatIsNotOwnTimeBinding_IsReported(long defaultTimeBindingId)
    {
        var violation = Assert.Single(await EvaluateAsync(defaultTimeBindingId));

        Assert.Equal("default_time_binding_in_version", violation.Code);
        Assert.Equal(DatasetTabs.Settings, violation.Target!.Area);
    }

    /// <summary>Evaluates the rule on a version with a time and a mapped binding and the given default.</summary>
    private static Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long? defaultTimeBindingId)
    {
        var snapshot = DatasetRows.Snapshot(
            fields: [DatasetRows.ColumnField(2, 100, "asOf", "as_of"), DatasetRows.ColumnField(3, 100, "branch", "branch_code")],
            bindings: [DatasetRows.Binding(TimeBindingId, 100, null, BindingKinds.Time, 2), DatasetRows.Binding(MappedBindingId, 100, 81, BindingKinds.Mapped, 3)],
            timeBindings: [DatasetRows.TimeBinding(TimeBindingId)],
            roles: [Dimensions.DimensionRows.Role(81, "branch", 7000)],
            defaultTimeBindingId: defaultTimeBindingId);
        return new DefaultTimeBindingInVersionRule().EvaluateAsync(snapshot, TestContext.Current.CancellationToken);
    }
}
