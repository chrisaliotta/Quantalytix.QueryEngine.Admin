using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>pinned_report_version_published</c>: a release pins only published report versions. The engine reads a release's reports
/// through <c>vw_qe_release_report</c>, which keeps published versions only, so any other pin would silently drop its report.
/// </summary>
public sealed class PinnedReportVersionPublishedRule(ReleaseReportReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "pinned_report_version_published";

    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every pinned report version that is not published.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unpublished pin.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var release = await reader.ReadAsync(versionId, cancellationToken);
        return
        [
            .. release.Reports.Where(report => report.Version.Status != PublicationStatuses.Published).Select(report => new RuleViolation
            {
                Rule = Code,
                Message = $"Report '{report.ReportKey}' pins v{report.Version.VersionNumber}, which is {report.Version.Status}; a release pins only published report versions."
            })
        ];
    }
}
