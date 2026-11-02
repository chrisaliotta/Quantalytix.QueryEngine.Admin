using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>dimension_consumers_resolve</c> (M10): every level and member key an object of the version's snapshot names in the dimension
/// exists in the version, the one they resolve keys in.
/// </summary>
public sealed class DimensionConsumersResolveRule : DimensionRule
{
    /// <summary>Gate: consumers read the version as soon as it is saved.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>dimension_consumers_resolve</c>.</summary>
    protected override string Code => "dimension_consumers_resolve";

    /// <summary>Reports every key an object of the snapshot names that the version lacks.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per missing key reference.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        var levelKeys = snapshot.Levels.Select(level => level.LevelKey).ToHashSet(StringComparer.Ordinal);
        var memberKeys = snapshot.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        return snapshot.Surroundings.NamedKeys
            .Where(reference => !(reference.IsLevel ? levelKeys : memberKeys).Contains(reference.Key))
            .Select(reference => Violation(
                DimensionTabs.Usage,
                $"{reference.Consumer} names {(reference.IsLevel ? "level" : "member")} '{reference.Key}', which the dimension's version lacks."));
    }
}
