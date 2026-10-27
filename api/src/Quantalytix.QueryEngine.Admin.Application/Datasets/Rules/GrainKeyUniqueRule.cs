using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Datasets;

/// <summary><c>grain_key_unique</c>: no two grains of a version share a key (D4).</summary>
public sealed class GrainKeyUniqueRule : DatasetRule
{
    /// <summary><c>grain_key_unique</c>.</summary>
    protected override string Code => "grain_key_unique";

    /// <summary>Reports every key more than one grain uses, at its second grain.</summary>
    /// <param name="snapshot">The dataset version.</param>
    /// <returns>One violation per duplicated key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DatasetSnapshot snapshot)
    {
        return snapshot.Grains
            .GroupBy(grain => grain.GrainKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Violation(DatasetTabs.Grains, $"Grain key '{group.Key}' is used by {group.Count()} grains of this version.", group.ElementAt(1).DatasetGrainId, group.Key));
    }
}
