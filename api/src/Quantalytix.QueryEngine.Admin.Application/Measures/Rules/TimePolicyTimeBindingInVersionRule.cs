using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>time_policy_time_binding_in_version</c>: a time policy's required time binding is a time binding of the version.</summary>
public sealed class TimePolicyTimeBindingInVersionRule : MeasureRule
{
    /// <summary><c>time_policy_time_binding_in_version</c>.</summary>
    protected override string Code => "time_policy_time_binding_in_version";

    /// <summary>Reports every required binding that is not a time binding of the version.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per such policy.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        var timeBindingIds = snapshot.TimeBindings.Select(time => time.DimensionBindingId).ToHashSet();
        var policies = snapshot.TimePolicies.ToDictionary(policy => policy.MeasureId);
        return ByKey(snapshot)
            .Where(measure => policies.TryGetValue(measure.MeasureId, out var policy) && policy.RequiredTimeBindingId is { } bindingId && !timeBindingIds.Contains(bindingId))
            .Select(measure => Violation(
                measure,
                $"The time policy of measure '{measure.MeasureKey}' requires binding {policies[measure.MeasureId].RequiredTimeBindingId}, which is not a time binding of this version."));
    }
}
