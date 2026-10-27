using Quantalytix.QueryEngine.Admin.Application.Dimensions;
using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Bindings;

/// <summary>
/// Requires the unknown member key of a binding to name a member of the published version of its dimension, which the active
/// assumption set pins and the engine resolves the key in. A key the version lacks would route unmapped rows nowhere.
/// </summary>
public sealed class UnknownMembersInVersionRule(
    ITableGateway<DimensionBindingRecord> bindings,
    PublishedDimensionReader dimensions,
    BindingNames names) : IPublicationRule
{
    /// <summary>The rule's violation code.</summary>
    public const string Code = "unknown_members_in_version";

    /// <summary>The kind this rule checks: dataset versions.</summary>
    public string Kind { get; } = VersionKinds.DatasetVersion;

    /// <summary>
    /// Reports each unknown member key the published version of the binding's dimension lacks, and each binding with one whose
    /// dimension has no published version.
    /// </summary>
    /// <param name="versionId">The dataset version leaving draft.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per missing key, or one per binding whose dimension has no published version.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var routing = (await bindings.ListAsync(new { DatasetVersionId = versionId }, cancellationToken))
            .Where(binding => binding.UnknownMemberKey is not null)
            .ToList();
        var named = await names.OfAsync(routing, cancellationToken);
        var published = new Dictionary<long, DimensionKeys?>();
        foreach (var dimensionId in routing.Select(binding => binding.DimensionId).Distinct())
            published[dimensionId] = await dimensions.ReadAsync(dimensionId, cancellationToken);

        var violations = new List<RuleViolation>();
        foreach (var binding in routing.OrderBy(binding => named[binding.DimensionBindingId], StringComparer.Ordinal))
        {
            var name = named[binding.DimensionBindingId];
            if (published[binding.DimensionId] is not { } keys)
                violations.Add(Violation($"Binding '{name}' routes unmapped rows to a member of its dimension, which has no published version."));
            else if (!keys.MemberKeys.Contains(binding.UnknownMemberKey!))
                violations.Add(Violation(
                    $"Binding '{name}' routes unmapped rows to member '{binding.UnknownMemberKey}', which is not a member of v{keys.Version.VersionNumber}, the published version of its dimension."));
        }

        return violations;
    }

    /// <summary>A violation of this rule.</summary>
    private static RuleViolation Violation(string message)
    {
        return new RuleViolation { Code = Code, Message = message };
    }
}
