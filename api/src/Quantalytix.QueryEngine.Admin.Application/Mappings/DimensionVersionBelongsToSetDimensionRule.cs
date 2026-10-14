using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>dimension_version_belongs_to_set_dimension</c>: the pinned dimension version is a version of the set's dimension,
/// because a mapped binding resolves its mapping version from the dimension version of the dimension it binds.
/// </summary>
public sealed class DimensionVersionBelongsToSetDimensionRule(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<MappingSetRecord> mappingSets,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_version_belongs_to_set_dimension";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports a pinned dimension version of another dimension.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the dimensions differ; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var mappingSet = await mappingSets.GetAsync(new { version.MappingSetId }, cancellationToken);
        var pinned = await dimensionVersions.GetAsync(new { version.DimensionVersionId }, cancellationToken);
        if (pinned.DimensionId == mappingSet.DimensionId) return [];

        var keys = (await dimensions.ListAsync(new { DimensionId = new[] { pinned.DimensionId, mappingSet.DimensionId } }, cancellationToken))
            .ToDictionary(dimension => dimension.DimensionId, dimension => dimension.DimensionKey);
        var message = $"The pinned dimension version v{pinned.VersionNumber} belongs to dimension '{keys[pinned.DimensionId]}', "
            + $"but mapping set '{mappingSet.MappingSetKey}' maps onto '{keys[mappingSet.DimensionId]}'.";
        return [new RuleViolation { Rule = Code, Message = message }];
    }
}
