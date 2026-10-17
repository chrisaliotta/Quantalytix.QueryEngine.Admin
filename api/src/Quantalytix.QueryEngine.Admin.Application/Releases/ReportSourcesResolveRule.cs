using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Releases;

/// <summary>
/// <c>report_sources_resolve</c>: every enabled source of a pinned report reads a dataset the release pins, and every key it
/// names resolves in that dataset version. Its report field is a member-grouped classification, pinned to one version, into the
/// report's dimension; its time field has a time binding; its measure is enabled; and its filter compares enabled, filterable
/// fields. The engine trusts these keys, so a key that does not resolve would fail the report at run time.
/// </summary>
public sealed class ReportSourcesResolveRule(ReleaseReportReader reader) : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_sources_resolve";

    /// <summary>The binding kinds that classify rows into canonical members.</summary>
    private static readonly string[] Classifying = [BindingKinds.Direct, BindingKinds.Mapped, BindingKinds.Bridge];

    /// <summary>The rule applies to semantic releases.</summary>
    public string Kind { get; } = VersionKinds.SemanticRelease;

    /// <summary>Reports every source key that does not resolve in the release.</summary>
    /// <param name="versionId">The release being validated.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolved key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var release = await reader.ReadAsync(versionId, cancellationToken);
        var violations = new List<RuleViolation>();
        foreach (var report in release.Reports)
        {
            foreach (var source in report.Sources.OrderBy(source => source.SourceKey, StringComparer.Ordinal))
            {
                var subject = $"Report '{report.ReportKey}' source '{source.SourceKey}'";
                if (!release.Datasets.TryGetValue(source.DatasetId, out var dataset))
                {
                    violations.Add(Violation($"{subject} reads dataset '{release.DatasetKeys[source.DatasetId]}', which the release does not pin."));
                    continue;
                }

                var binding = dataset.BindingOf(source.ReportFieldKey);
                if (binding is null || !Classifying.Contains(binding.BindingKind) || binding.GroupingMode != GroupingModes.Member
                    || binding.DimensionId != report.Version.ReportDimensionId || release.SelectedVersion(binding) is null)
                    violations.Add(Violation($"{subject}: report field '{source.ReportFieldKey}' of '{dataset.DatasetKey}' is no member-grouped, version-pinned classification into the report dimension."));
                if (dataset.Fields.GetValueOrDefault(source.TimeFieldKey) is not { } timeField
                    || !dataset.Bindings.Any(time => time.SourceFieldId == timeField.FieldId && dataset.TimeBindingIds.Contains(time.BindingId)))
                    violations.Add(Violation($"{subject}: time field '{source.TimeFieldKey}' of '{dataset.DatasetKey}' has no time binding."));
                if (dataset.Measures.GetValueOrDefault(source.MeasureKey) is not { IsEnabled: true })
                    violations.Add(Violation($"{subject}: measure '{source.MeasureKey}' is no enabled measure of '{dataset.DatasetKey}'."));
                if (source.SourceFilterJson is { } filter)
                {
                    foreach (var field in FilterNodes.Inspect(filter).Operands.Select(operand => operand.Id).Distinct(StringComparer.Ordinal)
                        .Where(key => dataset.Fields.GetValueOrDefault(key) is not { IsEnabled: true, IsFilterable: true }))
                        violations.Add(Violation($"{subject}: its filter compares '{field}', which is no enabled, filterable field of '{dataset.DatasetKey}'."));
                }
            }
        }
        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
