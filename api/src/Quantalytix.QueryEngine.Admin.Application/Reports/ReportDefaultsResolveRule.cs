using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>report_defaults_resolve</c>: the default filter compares enabled, filterable calculation dimensions of the model version,
/// and the default pivot breaks out by enabled, groupable ones, because every run of the report applies them.
/// </summary>
public sealed class ReportDefaultsResolveRule(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<CalculationDimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "report_defaults_resolve";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports each default dimension that the model version cannot serve.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unserved dimension.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        var available = (await dimensions.ListAsync(new { version.ModelVersionId }, cancellationToken))
            .Where(dimension => dimension.IsEnabled).ToDictionary(dimension => dimension.DimensionKey, StringComparer.Ordinal);
        return
        [
            .. ReportDefaults.FilterDimensions(version.DefaultFilterJson)
                .Where(key => available.GetValueOrDefault(key) is not { IsFilterable: true })
                .Select(key => new RuleViolation { Rule = Code, Message = $"The default filter compares '{key}', which is no filterable calculation dimension of the model version." }),
            .. ReportDefaults.PivotDimensions(version.DefaultPivotJson)
                .Where(key => available.GetValueOrDefault(key) is not { IsGroupable: true })
                .Select(key => new RuleViolation { Rule = Code, Message = $"The default pivot breaks out by '{key}', which is no groupable calculation dimension of the model version." })
        ];
    }
}
