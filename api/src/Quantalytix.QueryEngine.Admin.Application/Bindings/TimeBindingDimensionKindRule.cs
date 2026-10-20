using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires a time binding to bind a <c>time</c> dimension and to have its <c>qe_time_binding</c> extension (schema
/// section 12): the query engine builds a time axis only from a binding's extension, and the canonical time dimension gives
/// that axis its identity.
/// </summary>
public sealed class TimeBindingDimensionKindRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<TimeBindingRecord> timeBindings,
    ITableGateway<DimensionRecord> dimensions,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "time_binding_dimension_kind";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each time binding whose dimension is missing or not a time dimension, and each one without its extension.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>Up to two violations per time binding.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var timed = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.BindingKind == BindingKinds.Time)
            .ToList();
        var named = await names.OfAsync(timed, cancellationToken);
        var extended = (await timeBindings.ListAsync(new { BindingId = timed.Select(binding => binding.BindingId).ToArray() }, cancellationToken))
            .Select(extension => extension.BindingId)
            .ToHashSet();
        var bound = (await dimensions.ListAsync(new { DimensionId = timed.Select(binding => binding.DimensionId).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId);

        var violations = new List<RuleViolation>();
        foreach (var binding in timed.OrderBy(binding => named[binding.BindingId], StringComparer.Ordinal))
        {
            var name = named[binding.BindingId];
            if (!bound.TryGetValue(binding.DimensionId, out var dimension))
            {
                violations.Add(Violation($"Time binding '{name}' binds dimension {binding.DimensionId}, which does not exist."));
            }
            else if (dimension.DimensionKind != DimensionKinds.Time)
            {
                violations.Add(Violation(
                    $"Time binding '{name}' binds '{dimension.DimensionKey}', a {dimension.DimensionKind} dimension; time bindings bind a time dimension."));
            }

            if (!extended.Contains(binding.BindingId))
            {
                violations.Add(Violation($"Time binding '{name}' has no time extension."));
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
