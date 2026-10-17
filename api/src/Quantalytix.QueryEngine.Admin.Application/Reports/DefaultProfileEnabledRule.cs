using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>default_profile_enabled</c>: a report version names a default profile, and the profile is enabled, because a request
/// that names no profile runs under it.
/// </summary>
public sealed class DefaultProfileEnabledRule(ITableGateway<ReportVersionRecord> versions, ITableGateway<SourceProfileRecord> profiles)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "default_profile_enabled";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a missing or disabled default profile.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when no enabled default profile is named.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        if (version.DefaultSourceProfileId is not { } profileId)
            return [new RuleViolation { Rule = Code, Message = "The report version names no default profile." }];
        var profile = await profiles.GetAsync(new { SourceProfileId = profileId }, cancellationToken);
        if (profile.IsEnabled) return [];
        return [new RuleViolation { Rule = Code, Message = $"The default profile '{profile.ProfileKey}' is disabled." }];
    }
}
