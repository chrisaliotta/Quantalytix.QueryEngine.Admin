using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// <c>pinned_version_belongs_to_dimension</c>: every pin names a version of its own dimension (schema section 2), because
/// the persisted version is authoritative when the set is applied to that dimension.
/// </summary>
public sealed class PinnedVersionBelongsToDimensionRule(AssumptionPinReader reader, ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule applies to assumption sets.</summary>
    public string Kind { get; } = VersionKinds.AssumptionSet;

    /// <summary>Reports every pin whose dimension version is not one of its dimension's versions.</summary>
    /// <param name="versionId">The assumption set being published.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per offending pin.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var pinned = await reader.ListAsync(versionId, cancellationToken);
        var versions = await dimensionVersions.ListAsync(new { DimensionVersionId = pinned.Select(dimension => dimension.Pin.DimensionVersionId).ToArray() }, cancellationToken);
        var owned = versions.Select(version => (version.DimensionId, version.DimensionVersionId)).ToHashSet();
        return
        [
            .. pinned
                .Where(dimension => !owned.Contains((dimension.Pin.DimensionId, dimension.Pin.DimensionVersionId)))
                .Select(dimension => new RuleViolation
                {
                    Rule = "pinned_version_belongs_to_dimension",
                    Message = $"Dimension '{dimension.DimensionKey}' is pinned to dimension version {dimension.Pin.DimensionVersionId}, which is not one of its versions."
                })
        ];
    }
}
