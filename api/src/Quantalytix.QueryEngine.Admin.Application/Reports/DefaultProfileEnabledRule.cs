using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>default_profile_enabled</c>: a report version names a default profile, and it is an enabled profile of the model version,
/// because a request that names no profile runs under it. Report versions and calculation model versions both run the rule.
/// </summary>
/// <param name="kind">The kind the rule is registered for.</param>
/// <param name="reader">Reads the reports with the model version they are checked against.</param>
public sealed class DefaultProfileEnabledRule(string kind, ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "default_profile_enabled";

    /// <summary>The kind the rule checks: report versions or calculation model versions.</summary>
    public string Kind { get; } = kind;

    /// <summary>Reports each report that names no default profile, or one the model version does not have enabled.</summary>
    /// <param name="versionId">The report or model version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per report without an enabled default profile.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            if (report.Version.DefaultProfileKey is not { } profileKey)
                violations.Add(Violation($"Report '{report.ReportKey}' names no default profile."));
            else if (report.Profiles.FirstOrDefault(profile => profile.ProfileKey == profileKey) is not { IsEnabled: true })
                violations.Add(Violation($"Report '{report.ReportKey}' defaults to profile '{profileKey}', which is no enabled profile of the model version."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
