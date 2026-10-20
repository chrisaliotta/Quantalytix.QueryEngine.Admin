using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>default_profile_enabled</c>: a report version names a default profile, and it is one of its enabled profiles, because a request
/// that names no profile runs under it.
/// </summary>
/// <param name="reader">Reads the report version.</param>
public sealed class DefaultProfileEnabledRule(ReportGraphReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "default_profile_enabled";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a report version that names no default profile, or one it does not have enabled.</summary>
    /// <param name="versionId">The report version being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the version has no enabled default profile.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        foreach (var report in await reader.ReportsAsync(Kind, versionId, cancellationToken))
        {
            if (report.Version.DefaultProfileKey is not { } profileKey)
                violations.Add(Violation($"Report '{report.ReportKey}' names no default profile."));
            else if (report.Profiles.FirstOrDefault(profile => profile.ProfileKey == profileKey) is not { IsEnabled: true })
                violations.Add(Violation($"Report '{report.ReportKey}' defaults to profile '{profileKey}', which is no enabled profile of the report version."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
