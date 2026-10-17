using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>report_item_version_consistent</c>: all sources of a pinned report resolve the same report-item version, which has one
/// root and contains every member line's key and every variable's target member. The orchestrator aligns sources on report-item
/// nodes and evaluates formula lines at the root, so sources must share one tree whose members the report names.
/// </summary>
public sealed class ReportItemVersionRule(ReleaseReportReader reader, ITableGateway<DimensionMemberRecord> members) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_item_version_consistent";

    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every report whose sources disagree on, or whose names leave, the report-item tree.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var release = await reader.ReadAsync(versionId, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var report in release.Reports)
        {
            long[] versions =
            [
                .. report.Sources
                    .Where(source => release.Datasets.ContainsKey(source.DatasetId))
                    .Select(source => release.Datasets[source.DatasetId].BindingOf(source.ReportFieldKey))
                    .Where(binding => binding is not null && binding.DimensionId == report.Version.ReportDimensionId)
                    .Select(binding => release.SelectedVersion(binding!))
                    .Where(version => version is not null)
                    .Select(version => version!.Value)
                    .Distinct()
            ];
            if (versions.Length != 1)
            {
                if (versions.Length > 1)
                    violations.Add(Violation($"Report '{report.ReportKey}' sources resolve {versions.Length} report-item versions; they must share one."));
                continue;
            }

            var tree = await members.ListAsync(new { DimensionVersionId = versions[0] }, cancellationToken);
            var keys = tree.Select(member => member.MemberKey).ToHashSet(StringComparer.Ordinal);
            if (tree.Count(member => member.ParentMemberId is null) != 1)
                violations.Add(Violation($"Report '{report.ReportKey}' reads report-item version {versions[0]}, which has {tree.Count(member => member.ParentMemberId is null)} roots; it needs one."));
            foreach (var line in report.Lines.Where(line => line.ReportMemberKey is { } key && !keys.Contains(key)))
                violations.Add(Violation($"Report '{report.ReportKey}' line '{line.LineKey}' shows member '{line.ReportMemberKey}', which the report-item version lacks."));
            foreach (var variable in report.Variables.Where(variable => variable.TargetMemberKey is { } key && !keys.Contains(key)))
                violations.Add(Violation($"Report '{report.ReportKey}' variable '{variable.VariableKey}' targets member '{variable.TargetMemberKey}', which the report-item version lacks."));
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
