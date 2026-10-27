using Quantalytix.QueryEngine.Admin.Application.Changes;
using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Dimensions;

/// <summary>
/// <c>profile_required_keys_present</c> (M11): under the <c>extend</c> policy a system dimension keeps every member and role its
/// profile requires.
/// </summary>
public sealed class ProfileRequiredKeysPresentRule : DimensionRule
{
    /// <summary>Gate: the profile's module reads the required keys.</summary>
    public override RuleStage Stage => RuleStage.Gate;

    /// <summary><c>profile_required_keys_present</c>.</summary>
    protected override string Code => "profile_required_keys_present";

    /// <summary>Reports every required member key the version lacks and every required role key the dimension lacks.</summary>
    /// <param name="snapshot">The dimension version.</param>
    /// <returns>One violation per missing key.</returns>
    protected override IEnumerable<RuleViolation> Evaluate(DimensionSnapshot snapshot)
    {
        if (snapshot.EditPolicy != DimensionEditPolicies.Extend) return [];
        var profile = snapshot.Profile!;
        var memberKeys = snapshot.Members.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
        var roleKeys = snapshot.Roles.Select(role => role.DimensionRoleKey).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. profile.RequiredMemberKeys.Where(key => !memberKeys.Contains(key))
                .Select(key => Violation(DimensionTabs.Members, $"Required member '{key}' is missing ({profile.DisplayName} profile).")),
            .. profile.RequiredRoleKeys.Where(key => !roleKeys.Contains(key))
                .Select(key => Violation(DimensionTabs.Roles, $"Required role '{key}' is missing ({profile.DisplayName} profile)."))
        ];
    }
}
