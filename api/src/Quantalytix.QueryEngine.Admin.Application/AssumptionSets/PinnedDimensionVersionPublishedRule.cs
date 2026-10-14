using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.AssumptionSets;

/// <summary>
/// <c>pinned_version_published</c>: an assumption set pins only published dimension versions, because a published set
/// is an immutable snapshot and the engine reads published dimension content only.
/// </summary>
public sealed class PinnedDimensionVersionPublishedRule(AssumptionPinReader reader, ITableGateway<DimensionVersionRecord> dimensionVersions)
    : IPublicationRule
{
    /// <summary>The rule applies to assumption sets.</summary>
    public string Kind { get; } = VersionKinds.AssumptionSet;

    /// <summary>Reports every pinned dimension version that is not published.</summary>
    /// <param name="versionId">The assumption set being published.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation per unpublished pin.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var pinned = await reader.ListAsync(versionId, cancellationToken);
        var versions = await dimensionVersions.ListAsync(new { DimensionVersionId = pinned.Select(dimension => dimension.Pin.DimensionVersionId).ToArray() }, cancellationToken);
        return
        [
            .. pinned
                .Join(versions, dimension => dimension.Pin.DimensionVersionId, version => version.DimensionVersionId, (dimension, version) => (dimension.DimensionKey, Version: version))
                .Where(pin => pin.Version.Status != PublicationStatuses.Published)
                .Select(pin => new RuleViolation
                {
                    Rule = "pinned_version_published",
                    Message = $"Dimension '{pin.DimensionKey}' is pinned to v{pin.Version.VersionNumber}, which is {pin.Version.Status}; an assumption set pins only published versions."
                })
        ];
    }
}
