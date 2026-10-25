using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires measure keys to be unique within a dataset version: a request names measures by key, formulas reference them by
/// key, and the query engine loads a version's measures into a dictionary keyed by it.
/// </summary>
public sealed class MeasureKeyUniqueRule(ITableGateway<MeasureRecord> measures) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "measure_key_unique";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each measure key that more than one measure of the version uses.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per repeated key.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var rows = await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        return
        [
            .. rows
                .GroupBy(measure => measure.MeasureKey, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new RuleViolation { Rule = Code, Message = $"Measure key '{group.Key}' is used by {group.Count()} measures of this version." })
        ];
    }
}
