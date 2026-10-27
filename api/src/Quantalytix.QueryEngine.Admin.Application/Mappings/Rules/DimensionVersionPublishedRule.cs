using Quantalytix.QueryEngine.Admin.Application.Metadata;
using Quantalytix.QueryEngine.Admin.Application.Versioning;
using Quantalytix.QueryEngine.Admin.Application.Vocabulary;

namespace Quantalytix.QueryEngine.Admin.Application.Mappings;

/// <summary>
/// <c>dimension_version_published</c>: the mapping set's dimension has a published version, because the active assumption set
/// pins it and the engine resolves the entries' member keys in it.
/// </summary>
public sealed class DimensionVersionPublishedRule(
    ITableGateway<MappingVersionRecord> versions,
    ITableGateway<MappingSetRecord> sets,
    ITableGateway<DimensionVersionRecord> dimensionVersions,
    ITableGateway<DimensionRecord> dimensions)
    : IPublicationRule
{
    /// <summary>The rule code violations carry.</summary>
    public const string Code = "dimension_version_published";

    /// <summary>The kind the rule checks: mapping versions.</summary>
    public string Kind { get; } = VersionKinds.MappingVersion;

    /// <summary>Reports a set dimension that has no published version.</summary>
    /// <param name="versionId">The mapping version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>One violation when the dimension has no published version; otherwise none.</returns>
    public async Task<IReadOnlyList<RuleViolation>> EvaluateAsync(long versionId, CancellationToken cancellationToken)
    {
        var version = await versions.GetAsync(new { MappingVersionId = versionId }, cancellationToken);
        var set = await sets.GetAsync(new { version.MappingSetId }, cancellationToken);
        var published = await dimensionVersions.ListAsync(new { set.DimensionId, Status = PublicationStatuses.Published }, cancellationToken);
        if (published.Count == 1) return [];
        var dimension = await dimensions.GetAsync(new { set.DimensionId }, cancellationToken);
        return [new RuleViolation { Code = Code, Message = $"Dimension '{dimension.DimensionKey}' has no published version for the entries to resolve in; publish one first." }];
    }
}
