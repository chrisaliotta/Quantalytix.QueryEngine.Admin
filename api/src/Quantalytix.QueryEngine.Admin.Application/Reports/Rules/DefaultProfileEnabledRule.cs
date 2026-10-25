using Quantalytix.QueryEngine.Admin.Application.Versioning;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>default_profile_enabled</c>: the report names a default profile, which is one of its enabled profiles, because a request that
/// names no profile runs under it.
/// </summary>
public sealed class DefaultProfileEnabledRule : IReportRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "default_profile_enabled";

    /// <summary>Reports a report that names no default profile, or one it does not have enabled.</summary>
    /// <param name="report">The report.</param>
    /// <param name="scope">Unused: profiles are the report's own.</param>
    /// <returns>One violation when the report has no enabled default profile.</returns>
    public IEnumerable<RuleViolation> Evaluate(ReportGraph report, ReportScope scope)
    {
        if (report.Report.DefaultProfileKey is not { } profileKey)
            yield return Violation($"Report '{report.ReportKey}' names no default profile.");
        else if (report.Profiles.FirstOrDefault(profile => profile.ProfileKey == profileKey) is not { IsEnabled: true })
            yield return Violation($"Report '{report.ReportKey}' defaults to profile '{profileKey}', which is no enabled profile of the report.");
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
