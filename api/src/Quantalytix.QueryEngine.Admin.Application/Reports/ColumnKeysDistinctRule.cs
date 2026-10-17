using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Reports;

/// <summary>
/// <c>column_keys_distinct</c>: the version has columns, and none is named like a calculation dimension of the model version,
/// because a report table lists its breakout dimensions and its columns side by side under their keys.
/// </summary>
public sealed class ColumnKeysDistinctRule(
    ITableGateway<ReportVersionRecord> versions,
    ITableGateway<ReportColumnRecord> columns,
    ITableGateway<CalculationDimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "column_keys_distinct";

    /// <summary>The kind the rule checks: report versions.</summary>
    public string Kind { get; } = VersionKinds.ReportVersion;

    /// <summary>Reports a version without columns, and each column named like a calculation dimension.</summary>
    /// <param name="versionId">The report version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The violations found.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { ReportVersionId = versionId }, cancellationToken);
        var versionColumns = await columns.ListAsync(new { ReportVersionId = versionId }, cancellationToken);
        if (versionColumns.Count == 0) return [new RuleViolation { Rule = Code, Message = "The report version has no columns, so it returns nothing." }];
        var dimensionKeys = (await dimensions.ListAsync(new { version.ModelVersionId }, cancellationToken)).Select(dimension => dimension.DimensionKey)
            .ToHashSet(StringComparer.Ordinal);
        return
        [
            .. versionColumns.Where(column => dimensionKeys.Contains(column.ColumnKey)).Select(column => new RuleViolation
            {
                Rule = Code, Message = $"Column '{column.ColumnKey}' is named like a calculation dimension of the model version."
            })
        ];
    }
}
