using Quantalytix.QueryEngine.Admin.Application.Datasets;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary><c>measure_key_unique</c>: no two measures of a version share a key (D4).</summary>
public sealed class MeasureKeyUniqueRule : MeasureRule
{
    /// <summary><c>measure_key_unique</c>.</summary>
    protected override string Code => "measure_key_unique";

    /// <summary>Reports every key more than one measure uses, at its second measure.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per duplicated key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        return ByKey(snapshot)
            .GroupBy(measure => measure.MeasureKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => Violation(group.ElementAt(1), $"Measure key '{group.Key}' is used by {group.Count()} measures of this version."));
    }
}
