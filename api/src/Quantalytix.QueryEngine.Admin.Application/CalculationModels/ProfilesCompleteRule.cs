using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>profiles_complete</c>: every enabled profile picks an enabled source for every variable of the version, because a report
/// run under the profile reads each variable it needs from the profile's pick; a missing or disabled pick leaves the variable
/// without data.
/// </summary>
public sealed class ProfilesCompleteRule(
    ITableGateway<VariableRecord> variables,
    ITableGateway<VariableSourceRecord> sources,
    ITableGateway<SourceProfileRecord> profiles,
    ITableGateway<SourceProfileVariableRecord> picks)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "profiles_complete";

    /// <summary>The kind the rule checks: calculation model versions.</summary>
    public string Kind { get; } = VersionKinds.CalculationModelVersion;

    /// <summary>Reports each enabled profile's missing or disabled picks.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per variable a profile cannot fill.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var owner = new { ModelVersionId = versionId };
        var versionVariables = (await variables.ListAsync(owner, cancellationToken)).OrderBy(variable => variable.VariableKey, StringComparer.Ordinal).ToArray();
        var enabledSources = (await sources.ListAsync(owner, cancellationToken)).Where(source => source.IsEnabled)
            .Select(source => source.VariableSourceId).ToHashSet();
        var enabledProfiles = (await profiles.ListAsync(owner, cancellationToken)).Where(profile => profile.IsEnabled)
            .OrderBy(profile => profile.ProfileKey, StringComparer.Ordinal).ToArray();
        var picked = (await picks.ListAsync(new { SourceProfileId = enabledProfiles.Select(profile => profile.SourceProfileId).ToArray() }, cancellationToken))
            .ToDictionary(pick => (pick.SourceProfileId, pick.VariableId), pick => pick.VariableSourceId);
        var violations = new List<RuleViolation>();
        foreach (var profile in enabledProfiles)
        {
            foreach (var variable in versionVariables)
            {
                if (!picked.TryGetValue((profile.SourceProfileId, variable.VariableId), out var sourceId))
                    violations.Add(new RuleViolation { Rule = Code, Message = $"Profile '{profile.ProfileKey}' picks no source for variable '{variable.VariableKey}'." });
                else if (!enabledSources.Contains(sourceId))
                    violations.Add(new RuleViolation { Rule = Code, Message = $"Profile '{profile.ProfileKey}' picks a disabled source for variable '{variable.VariableKey}'." });
            }
        }
        return violations;
    }
}
