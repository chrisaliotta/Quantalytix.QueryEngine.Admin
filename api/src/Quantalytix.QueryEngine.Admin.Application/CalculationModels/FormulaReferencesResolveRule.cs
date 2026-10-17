using Quantalytix.QueryEngine.Admin.Application.Expressions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.CalculationModels;

/// <summary>
/// <c>formula_references_resolve</c>: every <c>[m.KEY]</c> of every calculated measure names a variable or calculated measure
/// of the version. The report orchestrator evaluates formulas against the version's keys alone, so a key that a later edit
/// renamed or deleted would have nothing to read.
/// </summary>
public sealed class FormulaReferencesResolveRule(
    ITableGateway<VariableRecord> variables,
    ITableGateway<CalculatedMeasureRecord> calculatedMeasures)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "formula_references_resolve";

    /// <summary>The kind the rule checks: calculation model versions.</summary>
    public string Kind { get; } = VersionKinds.CalculationModelVersion;

    /// <summary>Reports each formula reference that names no key of the version.</summary>
    /// <param name="versionId">The model version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unresolved reference.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var measures = await calculatedMeasures.ListAsync(new { ModelVersionId = versionId }, cancellationToken);
        var keys = (await variables.ListAsync(new { ModelVersionId = versionId }, cancellationToken)).Select(variable => variable.VariableKey)
            .Concat(measures.Select(measure => measure.MeasureKey))
            .ToHashSet(StringComparer.Ordinal);
        return
        [
            .. measures.OrderBy(measure => measure.MeasureKey, StringComparer.Ordinal).SelectMany(measure =>
                ExpressionInspector.Inspect(measure.ExpressionText).MeasureKeys.Where(key => !keys.Contains(key)).Select(key => new RuleViolation
                {
                    Rule = Code,
                    Message = $"Calculated measure '{measure.MeasureKey}' reads [m.{key}], which is no variable or calculated measure of this version."
                }))
        ];
    }
}
