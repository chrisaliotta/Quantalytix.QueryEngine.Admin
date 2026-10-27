using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary><c>level_key_unique</c>: no two levels of a version share a key, because groupings name levels by key.</summary>
public sealed class LevelKeyUniqueRule : DimensionRule
{
    /// <summary>Gate: the live version is read as it is saved.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>level_key_unique</c>.</summary>
    protected override string Code => "level_key_unique";

    /// <summary>Reports every key more than one level uses, at its second level.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per duplicated key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        return snapshot.Levels
            .GroupBy(level => level.LevelKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => Violation(DimensionTabs.Levels, $"Level key '{group.Key}' is used by {group.Count()} levels.", group.ElementAt(1).DimensionLevelId));
    }
}
