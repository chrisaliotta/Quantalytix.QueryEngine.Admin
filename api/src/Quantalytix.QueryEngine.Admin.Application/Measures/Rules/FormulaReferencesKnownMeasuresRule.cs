using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Measures;

/// <summary>
/// Requires every <c>[m.key]</c> a formula references to name a measure of the same dataset version (schema section 13):
/// the dependency derivation skips keys that name no measure, and the query engine could not compute the formula.
/// </summary>
public sealed class FormulaReferencesKnownMeasuresRule(ITableGateway<MeasureRecord> measures) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "formula_references_known_measures";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each measure key a formula references that no measure of the version has.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One violation per unknown reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var versionMeasures = await measures.ListAsync(new { DatasetVersionId = versionId }, cancellationToken);
        var keys = versionMeasures.Select(measure => measure.MeasureKey).ToHashSet(StringComparer.Ordinal);
        return
        [
            .. versionMeasures
                .Where(measure => measure.MeasureKind == MeasureKinds.Formula)
                .OrderBy(formula => formula.MeasureKey, StringComparer.Ordinal)
                .SelectMany(formula => ExpressionInspector.Inspect(formula.ExpressionText!).MeasureKeys
                    .Where(key => !keys.Contains(key))
                    .Select(key => new RuleViolation
                    {
                        Code = Code,
                        Message = $"Formula measure '{formula.MeasureKey}' references measure '{key}', which is not a measure of this version."
                    }))
        ];
    }
}
