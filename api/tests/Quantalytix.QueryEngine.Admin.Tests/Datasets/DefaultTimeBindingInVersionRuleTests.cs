using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;
using Quantalytix.QueryEngine.Admin.Tests.Support;
using Xunit;

namespace Quantalytix.QueryEngine.Admin.Tests.Datasets;

/// <summary>
/// Confirms that <c>default_time_binding_in_version</c> accepts a version without a default time binding or with one that
/// is a time binding of the same version, and reports a default that has no time extension or belongs to another version.
/// Queries without their own time axis fall back to the default, so it must be a time axis of this very version.
/// </summary>
public sealed class DefaultTimeBindingInVersionRuleTests
{
    /// <summary>The version being validated.</summary>
    private const long VersionId = 100;

    /// <summary>A time binding of the version.</summary>
    private const long TimeBindingId = 21;

    /// <summary>A binding of the version without a time extension.</summary>
    private const long MappedBindingId = 22;

    /// <summary>A time binding of another version of the dataset.</summary>
    private const long OtherVersionTimeBindingId = 31;

    /// <summary>The bindings of the version and of version 200.</summary>
    private readonly InMemoryTableGateway<DimensionBindingRecord> _bindings = new(
        DatasetRows.Binding(TimeBindingId, VersionId, null, BindingKinds.Time, 2),
        DatasetRows.Binding(MappedBindingId, VersionId, 81, BindingKinds.Mapped, 3),
        DatasetRows.Binding(OtherVersionTimeBindingId, 200, null, BindingKinds.Time, 12));

    /// <summary>The time extensions of both time bindings.</summary>
    private readonly InMemoryTableGateway<TimeBindingRecord> _timeBindings = new(
        DatasetRows.TimeBinding(TimeBindingId),
        DatasetRows.TimeBinding(OtherVersionTimeBindingId));

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

    /// <summary>A binding without a time extension, or a time binding of another version, is reported.</summary>
    /// <param name="defaultTimeBindingId">The version's default time binding.</param>
    /// <returns>A task that completes when the rule is checked.</returns>
    [Theory]
    [InlineData(MappedBindingId)]
    [InlineData(OtherVersionTimeBindingId)]
    public async Task BindingThatIsNotOwnTimeBinding_IsReported(long defaultTimeBindingId)
    {
        var violation = Assert.Single(await EvaluateAsync(defaultTimeBindingId));

        Assert.Equal(DefaultTimeBindingInVersionRule.Code, violation.Code);
    }

    /// <summary>Evaluates the rule on the version with the given default time binding.</summary>
    private Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long? defaultTimeBindingId)
    {
        var versions = new InMemoryTableGateway<DatasetVersionRecord>(DatasetRows.Version(VersionId, defaultTimeBindingId));
        var rule = new DefaultTimeBindingInVersionRule(versions, _bindings, _timeBindings);
        return rule.EvaluateAsync(VersionId, TestContext.Current.CancellationToken);
    }
}
