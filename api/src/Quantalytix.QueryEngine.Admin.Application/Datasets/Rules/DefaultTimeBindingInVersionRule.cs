using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>default_time_binding_in_version</c>: the default time binding is a time binding of the version.</summary>
public sealed class DefaultTimeBindingInVersionRule : DatasetRule
{
    /// <summary><c>default_time_binding_in_version</c>.</summary>
    protected override string Code => "default_time_binding_in_version";

    /// <summary>Reports a default time binding that is not one of the version's time bindings.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>At most one violation.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        if (snapshot.Version.DefaultTimeBindingId is not { } bindingId) yield break;
        if (snapshot.Bindings.Any(binding => binding.DimensionBindingId == bindingId) && snapshot.TimeBindings.Any(time => time.DimensionBindingId == bindingId)) yield break;
        yield return Violation(DatasetTabs.Settings, $"Default time binding {bindingId} is not a time binding of this version.");
    }
}
