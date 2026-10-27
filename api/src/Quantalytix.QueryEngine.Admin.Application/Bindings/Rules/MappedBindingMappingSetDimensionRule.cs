using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the mapping set of a mapped binding to target the binding's own dimension: the query engine maps source keys
/// through the mapping version of that set for the resolved version of the bound dimension, so a set of another dimension
/// would map onto members the binding cannot hold.
/// </summary>
public sealed class MappedBindingMappingSetDimensionRule(
    ITableGateway<DimensionBindingRecord> bindings,
    ITableGateway<MappingSetRecord> mappingSets,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "mapped_binding_mapping_set_dimension";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each mapped binding whose mapping set does not exist or targets another dimension.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per mapped binding with a foreign mapping set.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var mapped = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.BindingKind == BindingKinds.Mapped && binding.MappingSetId is not null)
            .ToList();
        var named = await names.OfAsync(mapped, cancellationToken);
        var sets = (await mappingSets.ListAsync(new { MappingSetId = mapped.Select(binding => binding.MappingSetId!.Value).Distinct().ToArray() }, cancellationToken))
            .ToDictionary(mappingSet => mappingSet.MappingSetId);

        var violations = new List<RuleViolation>();
        foreach (var binding in mapped.OrderBy(binding => named[binding.DimensionBindingId], StringComparer.Ordinal))
        {
            var mappingSetId = binding.MappingSetId!.Value;
            if (!sets.TryGetValue(mappingSetId, out var mappingSet))
            {
                violations.Add(Violation($"Mapped binding '{named[binding.DimensionBindingId]}' maps through mapping set {mappingSetId}, which does not exist."));
            }
            else if (mappingSet.DimensionId != binding.DimensionId)
            {
                violations.Add(Violation($"Mapped binding '{named[binding.DimensionBindingId]}' maps through mapping set '{mappingSet.MappingSetKey}', which targets another dimension than the binding."));
            }
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}
