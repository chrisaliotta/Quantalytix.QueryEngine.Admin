using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the grouping level, grouping ancestor and unknown member keys of a binding to name a level or member of the
/// published version of its dimension, which the active assumption set pins and the engine resolves the keys in. A key the
/// version lacks would group or route nothing.
/// </summary>
public sealed class GroupingReferencesInVersionRule(ITableGateway<DimensionBindingRecord> bindings, PublishedDimensionReader dimensions) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "grouping_references_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>
    /// Reports each key the published version of the binding's dimension lacks, and each binding with keys whose dimension
    /// has no published version.
    /// </summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing key, or one per binding whose dimension has no published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var referencing = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.GroupingLevelKey is not null || binding.GroupingAncestorMemberKey is not null || binding.UnknownMemberKey is not null)
            .OrderBy(binding => binding.DisplayName, StringComparer.Ordinal)
            .ToList();
        var published = new Dictionary<long, DimensionKeys?>();
        foreach (var dimensionId in referencing.Select(binding => binding.DimensionId).Distinct())
            published[dimensionId] = await dimensions.ReadAsync(dimensionId, cancellationToken);

        var violations = new List<RuleViolation>();
        foreach (var binding in referencing)
        {
            var name = binding.DisplayName;
            if (published[binding.DimensionId] is not { } keys)
            {
                violations.Add(Violation($"Binding '{name}' names levels or members of its dimension, which has no published version."));
                continue;
            }

            var version = $"v{keys.Version.VersionNumber}, the published version of its dimension";
            if (binding.GroupingLevelKey is { } level && !keys.LevelKeys.Contains(level))
                violations.Add(Violation($"Binding '{name}' groups at level '{level}', which is not a level of {version}."));
            if (binding.GroupingAncestorMemberKey is { } ancestor && !keys.MemberKeys.Contains(ancestor))
                violations.Add(Violation($"Binding '{name}' groups under ancestor member '{ancestor}', which is not a member of {version}."));
            if (binding.UnknownMemberKey is { } unknown && !keys.MemberKeys.Contains(unknown))
                violations.Add(Violation($"Binding '{name}' routes unmapped rows to member '{unknown}', which is not a member of {version}."));
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Rule = Code, Message = message };
    }
}
