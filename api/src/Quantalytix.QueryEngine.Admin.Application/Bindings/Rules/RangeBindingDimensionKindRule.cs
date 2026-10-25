using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires a range binding to bind a <c>range</c> dimension: a range binding assigns each scalar value through the range
/// bands of the resolved dimension version, and only range dimensions have bands.
/// </summary>
public sealed class RangeBindingDimensionKindRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<DimensionRecord> dimensions,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "range_binding_dimension_kind";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each range binding whose dimension does not exist or is not a range dimension.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per range binding of another dimension kind.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var ranged = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.BindingKind == BindingKinds.Range)
            .ToList();
        var named = await names.OfAsync(ranged, cancellationToken);
        var bound = (await dimensions.ListAsync(new { DimensionId = ranged.Select(binding => binding.DimensionId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId);

        var violations = new List<RuleViolation>();
        foreach (var binding in ranged.OrderBy(binding => named[binding.DimensionBindingId], StringComparer.Ordinal))
        {
            if (!bound.TryGetValue(binding.DimensionId, out var dimension))
            {
                violations.Add(Violation($"Range binding '{named[binding.DimensionBindingId]}' binds dimension {binding.DimensionId}, which does not exist."));
            }
            else if (dimension.DimensionKind != DimensionKinds.Range)
            {
                violations.Add(Violation(
                    $"Range binding '{named[binding.DimensionBindingId]}' binds '{dimension.DimensionKey}', a {dimension.DimensionKind} dimension; range bindings classify through the bands of a range dimension."));
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
