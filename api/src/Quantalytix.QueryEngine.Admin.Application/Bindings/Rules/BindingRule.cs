using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>A dataset rule about bindings, whose violations target the binding on the bindings tab by its name.</summary>
public abstract class BindingRule : DatasetRule
{
    /// <summary>The violations of a snapshot, with each binding's name.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>The violations.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        return Evaluate(snapshot, DatasetBindingNames.Of(snapshot));
    }

    /// <summary>The violations of a snapshot.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>The violations.</returns>
    protected abstract IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot, IReadOnlyDictionary<long, string> names);

    /// <summary>A violation about a binding.</summary>
    /// <param name="binding">The binding.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <param name="message">What is wrong.</param>
    /// <returns>The violation.</returns>
    protected RuleViolation Violation(DimensionBindingRecord binding, IReadOnlyDictionary<long, string> names, string message)
    {
        return Violation(DatasetTabs.Bindings, message, binding.DimensionBindingId, names[binding.DimensionBindingId]);
    }

    /// <summary>The bindings in name order.</summary>
    /// <param name="bindings">Some bindings.</param>
    /// <param name="names">Each binding's name, by id.</param>
    /// <returns>The bindings by name.</returns>
    protected static IEnumerable<DimensionBindingRecord> ByName(IEnumerable<DimensionBindingRecord> bindings, IReadOnlyDictionary<long, string> names)
    {
        return bindings.OrderBy(binding => names[binding.DimensionBindingId], StringComparer.Ordinal);
    }
}
