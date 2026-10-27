using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>dimension_consumers_resolve</c> (M10): every level and member key a live object names in the dimension exists in the live
/// version, the one they resolve keys in. A frozen version is read by no live object, so it is not checked.
/// </summary>
public sealed class DimensionConsumersResolveRule : DimensionRule
{
    /// <summary>Gate: consumers read the live version as soon as it is saved.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>dimension_consumers_resolve</c>.</summary>
    protected override string Code => "dimension_consumers_resolve";

    /// <summary>Reports every key a live object names that the version lacks.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per missing key reference.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        if (snapshot.Version.Status == PublicationStatuses.Retired) return [];
        var levelKeys = snapshot.Levels.Select(level => level.LevelKey).ToHashSet(StringComparer.Ordinal);
        var memberKeys = snapshot.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        return snapshot.Surroundings.NamedKeys
            .Where(reference => !(reference.IsLevel ? levelKeys : memberKeys).Contains(reference.Key))
            .Select(reference => Violation(
                DimensionTabs.Usage,
                $"{reference.Consumer} names {(reference.IsLevel ? "level" : "member")} '{reference.Key}', which v{snapshot.Version.VersionNumber} lacks."));
    }
}
