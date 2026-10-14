using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// <c>pinned_version_belongs_to_dimension</c>: a binding that pins a dimension version pins a published version of its own
/// dimension, because the query engine reads only published dimension versions and resolves the pin among the versions of
/// the bound dimension.
/// </summary>
public sealed class BindingPinnedVersionRule(ITableGateway<DimensionBindingRecord> bindings, ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "pinned_version_belongs_to_dimension";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>Reports each pinned version that belongs to another dimension, does not exist, or is not published.</summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per binding with an unusable pin.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var pinning = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.PinnedDimensionVersionId is not null)
            .OrderBy(binding => binding.DisplayName, StringComparer.Ordinal)
            .ToList();
        var pinned = (await dimensionVersions.ListAsync(
                new { DimensionVersionId = pinning.Select(binding => binding.PinnedDimensionVersionId!.Value).Distinct().ToArray() },
                cancellationToken))
            .ToDictionary(version => version.DimensionVersionId);

        var violations = new List<RuleViolation>();
        foreach (var binding in pinning)
        {
            var pinnedId = binding.PinnedDimensionVersionId!.Value;
            if (!pinned.TryGetValue(pinnedId, out var version) || version.DimensionId != binding.DimensionId)
            {
                violations.Add(Violation($"Binding '{binding.DisplayName}' pins dimension version {pinnedId}, which is not a version of its dimension."));
            }
            else if (version.Status != PublicationStatuses.Published)
            {
                violations.Add(Violation($"Binding '{binding.DisplayName}' pins v{version.VersionNumber} of its dimension, which is {version.Status}; a pinned version must be published."));
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
